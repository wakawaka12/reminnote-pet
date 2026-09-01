# 04 · Reminder 领域模型对接

> 本文描述桌宠**如何映射**主程序已冻结的 Reminder 领域模型。本模块**不定义**领域模型，
> 只定义「消费与回写」的边界。模型本身遵循主程序 `ReminNote_MASTER_DEVELOPMENT_PLAN.md` `#11–#17`。

## 1. 三层结构（主程序已冻结）

```text
ReminderRule       业务真值（为什么、怎么提醒）          ← 不可被桌宠改写
   ↓
ReminderSchedule   可重建的执行态（TriggerAtUtc / OccurrenceId / SchemaRevision）
   ↓
ReminderInstance   已发生的事实（触发历史 + 生命周期状态） ← 桌宠主要消费这一层
```

桌宠**只读** `ReminderInstance` 层（以及必要的 `ReminderRule`/Task 关联信息用于展示），
并**回写**生命周期动作，**绝不**修改 `ReminderRule`。Snooze 由命令派生临时 schedule。

## 2. 桌宠消费的字段（仅供呈现/过滤）

| 逻辑字段 | 用途 | 说明 |
|----------|------|------|
| `InstanceId` | 提醒唯一标识 | 用于去重、标记 `READ/RESOLVED` |
| `PurposeKind` | 语义 | `TASK_PRE_START / TASK_START / TASK_RANGE_END / TASK_CUSTOM / ANIME_PRE_AIRING / ANIME_AIRING / ANIME_CUSTOM`（枚举名以主程序为准） |
| `Priority` | 优先级 | `LOW / NORMAL / HIGH` |
| `IsPinned` | 是否置顶 | 独立于优先级，可与 `HIGH` 组合 |
| `Title / Body` | 文案 | 走资源键/格式化边界，不硬编码 |
| `TaskId / TaskTimeRange` | 关联 | 用于跳转主程序、结果动作 |
| `InstanceState` | 生命周期 | `UNREAD / READ / RESOLVED` |
| `Occurrence / ScheduleRevision` | 版本 | 用于区分被替换的旧提醒 |

## 3. 生命周期映射（主程序 `#16`）

```
UNREAD ──展示──► READ ──领域动作──► RESOLVED
   │              │
   └─ 桌宠默认不自动改状态；由用户动作驱动
```

- **展示某提醒** → 可发 `ReminderMarkRead`，对应 `READ`。
- **关闭气泡** → 只是 `READ`，**不等于** `RESOLVED`（对齐「关 Toast ≠ 解决提醒」）。
- **只有领域动作**才能 `RESOLVED`：`DONE / SNOOZE / IGNORE / WATCHED / WATCH_LATER / SKIP`。

## 4. 命令回写语义（每条都是幂等命令）

| 命令 | 生命周迁移 | 关键不变式 |
|------|-----------|-----------|
| `ReminderSnooze` | 当前 `Instance` → `RESOLVED`（触发态），派生**临时** schedule | 不改写 `ReminderRule`；保留原 purpose，`cause=SNOOZE` |
| `ReminderIgnore` | 当前 `Instance` → `RESOLVED` | **只解析当前** `Instance`；不静默关闭未来规则 |
| `ReminderMarkRead` | `UNREAD → READ` | 触发历史不变，仅读状态 |
| `RecordTaskResult`(`DONE`) | 关联 Task 完成 | 主程序自动使该 Instance 的未来 pending Schedule 失效（`#14`）；不改历史 |
| `ReminderResolve`(`WATCHED / WATCH_LATER / SKIP`) | `Instance → RESOLVED` | 用于 Anime/自定义 purpose |

> 桌宠侧无需复刻 `#14` 的「完成→取消未来提醒」逻辑——那是 Agent 的职责。桌宠只需调用
> `RecordTaskResult` / 存在结果命令，Agent 自动落实。

## 5. 关键不变式（桌宠必须遵守）

1. **永不直写** DbContext / SQL / EF entity。一切写经 Agent 命令。
2. **永不改写 `ReminderRule`**。Snooze/Ignore 只作用于 Instance 或派生 schedule。
3. **不删历史**。只做 `READ/RESOLVED` 状态迁移，不删除 `ReminderInstance`。
4. **purpose 驱动恢复判断**（`#19`）：桌宠恢复/汇总时必须按 purpose 过滤，而非仅按时间戳。
5. **Agent 是唯一写入者**：桌宠进程内不得存在对业务库的写路径。

## 6. 持久化边界

- 业务 SQLite（`.devdata/reminnote.sqlite`）由主程序/Agent 管理；表 `reminder_rule`、
  `reminder_schedule`、`reminder_instance`（主程序 P3 实装，命名以主程序为准）。
- 桌宠**可选**维护自己的本地状态文件（如 `.devdata/pet_state.json`）：仅存「已呈现提醒的
  去重集合、桌宠位置、收纳态、音量/动画开关」等**纯呈现状态**，与业务数据完全隔离，
  不写入业务库。该文件必须与主程序共用 `.devdata` 目录约定或使用独立命名空间，避免冲突：
  建议 `remnote.pet.v1.json`，并遵循「状态可安全删除/重建」原则（丢失不丢业务数据）。
