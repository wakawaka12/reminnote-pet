# 03 · 数据流与通道健康

## 1. 一句话数据流

```
Agent 的 Reminder Scheduler 到点触发
   → 生成 ReminderInstance（UNREAD，携带 purpose / priority / pin）
   → Pet 感知到 UNREAD 提醒
   → 进入「提醒态」（动画 + 气泡 + 动作按钮）
   → 用户点 DONE / SNOOZE / IGNORE / 打开主程序
   → Pet 向 Agent 下发对应命令
   → Agent 写库（Instance 生命周期迁移）
```

## 2. 触发感知：两种方案（对外提供一致接口）

本模块不对调用方暴露「轮询还是推送」，只暴露 `I ReminderFeed` 语义。设备实现二选一：

### 方案 A：只读轮询（推荐，改动小，P3 数据面足够）
- Pet 参照 `ReminNote.Widget` 的 `DispatcherTimer` 轻量刷新（Widget 已用 2s 周期）。
- 每周期查询「当前 profile 下的 `UNREAD` ReminderInstance 集合」，与本地已呈现集合做差集，
  新增的进入提醒态。
- 优点：无需新增 Agent→客户端推送帧；复用既有只读投影。
- 代价：提醒到达有最长一个刷新周期的延迟（2s 内可接受，可调）。

### 方案 B：Agent 事件推送（更实时，P3 之后再做）
- 主程序 hello 已声明能力 `changes.get_since`（Change Journal），可扩展按
  `ReminderInstance` 类型增量下发到订阅客户端。
- 需新增 Agent→Client 的事件帧 + 订阅生命周期（连接、心跳、断线重连、去重）。
- 优点：秒级、省轮询。
- 代价：IPC 契约更重；本模块先做 A，B 作为后续演进，不阻塞切片 1–3。

> 对外接口契约见 `docs/06_INTERFACE_STUBS.md` 的 `IReminderFeed` / `IReminderQueryService`。

## 3. 写回动作：一律经 Agent 命令

| 用户动作 | 语义 | 对应的 Agent 命令类 |
|----------|------|---------------------|
| 完成 | `DONE` | `RecordTaskResult`（Task 结果）+ 使未来 Schedule 失效（主程序 `#14` 自动处理） |
| 稍后提醒 | `SNOOZE` | `ReminderSnooze`（派生临时 schedule，**不改 Rule**，见主程序 `#13`） |
| 忽略 | `IGNORE` | `ReminderIgnore`（仅解析**当前 Instance**，不静默关闭未来规则） |
| 看完（ANIME） | `WATCHED` | `ReminderResolve`（purpose 相关） |
| 稍后看（ANIME） | `WATCH_LATER` | `ReminderResolve` |
| 去主程序处理 | 打开主程序 | 唤起 Main（`MainActivation` 管道） |

要点：

- 每个命令带**幂等键 + revision**，断线重发只重试一次（对齐 `AgentTaskClient` 现有做法）。
- 命令**不改变** `ReminderRule`；`SNOOZE` 只造派生 schedule；`IGNORE` 只解析当前 Instance。
- 命令由 Agent 处理并落库，Pet **永不**直写。

## 4. 生命周期映射

主程序 `#16`：`UNREAD → READ → RESOLVED`。

- 桌宠**展示**某提醒 → 可标记 `READ`（等于读过了）。
- 关闭气泡不等于解析 → 仍可能是 `READ` 未 `RESOLVED`。
- `RESOLVED` 只能由领域动作达成（`DONE / SNOOZE / IGNORE / WATCHED / WATCH_LATER / SKIP`）。

桌宠展示层只需关心：**「未读待呈现」→「已读/已处理」**，其余状态机由主程序/Agent 维护。

## 5. 通道健康（为什么桌宠要与 Toast 并存）

主程序 `#21` 要求区分：
> 提醒没触发  vs  提醒触发了但某通道不可用

- 桌宠是**一个通道**，与 `Windows Toast / Tray / Widget Alert / 声音 / 唤醒定时器` 并列。
- 若系统 Toast 被禁用，**不应**丢失提醒——桌宠（或其它通道）仍能呈现。
- 通知健康需能表示桌宠通道的可用性：
  - `PET_AVAILABLE`：桌宠进程运行、能读到 UNREAD、能显示。
  - `PET_AVAILABLE_UI_BLOCKED`：桌宠运行但被用户收纳/静音/聚焦抢占。
  - `PET_UNAVAILABLE`：桌宠进程未运行 / 崩溃。
- 历史应如实地记录 `TRIGGERED · PET BLOCKED`，而**不是**假装没触发。
- 主程序 P3 验收要求「即使 Toast 不可用，提醒状态仍有效」——桌宠可承担该职责的一部分。

## 6. 优先级与重复

主程序 `#17`：`priority ∈ {LOW, NORMAL, HIGH}`，`PIN` 独立于优先级。

| 场景 | 桌宠表现 |
|------|----------|
| LOW / NORMAL | 后到先计、可合并为「有 N 条待提醒」的摘要气泡 |
| HIGH | 可单独强提醒、重复（interval + maxCount） |
| PIN + HIGH | 更强重复；桌宠置顶、打扰更明显 |

重复提醒应尽量**更新同一**气泡/桌宠，而不是刷屏（对齐 `#17`）。

## 7. 安静时段（Quiet Hours）与恢复

- 安静时段内：**调度照常、状态照常演进**，只改变**呈现**（桌宠可安静收纳、暂停动画音效）。
- 恢复后：普通提醒可汇总；`HIGH / PIN+HIGH` 可单独上浮。
- 电脑睡眠/关机后恢复（`#19`）：按 **purpose 而非仅时间**判断是否仍有意义：
  - `TASK_PRE_START` 已错过很久 → 可能过时，不再弹「即将开始」。
  - `TASK_RANGE_END` 结果提示 → 恢复后仍可能有用。
  - `ANIME_AIRING` 已播出 → 转 `WATCH LATER`，不弹「即将播出」。
- 桌宠在恢复后应**汇总**以 `purpose` 过滤后的有效提醒，而非把 30 条过期提醒全弹一遍。
