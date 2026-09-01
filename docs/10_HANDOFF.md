# 10 · 对接主程序操作说明（Handoff）

> 本文是**未来真正对接时**的施工说明。当前阶段**不执行**其中任何对 `D:\Anime` 的修改——
> 一切主程序改动须在用户明确同意后进行。本模块现阶段仅交付文档与接口草案。

## 1. 对接时机

- 主程序 **P3 Reminder 数据面**就绪（能产出 `ReminderInstance`，并提供只读查询与命令）。
- 或用户明确要求先用 Mock 提前试运行桌宠（切片 2）。

## 2. 需在主程序仓库做的改动（清单）

### 2.1 新增项目
- 新建 `src/windows/ReminNote.Pet/`，`ReminNote.Pet.csproj` 以 `ReminNote.Widget.csproj` 为模板
  （`net10.0-windows`、WPF、`UseWPF`、引用 Core/Infrastructure/Agent 边界、Link 共享 Design System 与 `UiText`）。
- 将新项目加入 `ReminNote.sln`。

### 2.2 协议层（`ReminNote.Core.Protocol`）
- `ProtocolClientKinds` 增加 `Pet = "pet"`。
- `ProtocolOperations` 增加 Reminder 命令常量（`ReminderSnooze / ReminderIgnore / ReminderMarkRead / ReminderResolve`）。
- `ProtocolPipeNames` 增加 `PetActivation(profileScope, petInstanceId)`。
- （可选）`ProtocolLimits` 增加桌宠相关超时/负载上限。

### 2.3 只读与命令边界（Agent / Infrastructure）
- 实现 `IReminderQueryService`（只读投影返回 UNREAD `ReminderSnapshot[]`）。
- 在 `AgentTaskClient`（或其子集客户端）实现 `IReminderCommandService` / `IReminderFeed`（默认轮询）。
- 实现 `IPetNotificationHealth`（通道健康登记）。
- `AgentTaskClient` 构造校验扩展为 `Main | Widget | Pet`。

### 2.4 提醒 → 事件/查询接线
- 接通 `ReminderInstance` 的只读投影，使 `ListUnreadAsync` 能按 profile 返回。
- 保证写入命令经 business 管道 + 幂等键 + revision 落库。

### 2.5 测试
- 主程序既有 xUnit v3 验收方式；新增 Reminder 命令/查询契约测试。
- 桌面交互部分为手工验收（Window 相关无法纯 xUnit 覆盖）。

## 3. 接口拷贝方式

- 对接时把 `interfaces/` 下的接口草案拷贝进主程序对应命名空间，或按 `docs/06_INTERFACE_STUBS.md` 的签名实现。
- **不要**把 `D:\桌宠` 下 `.cs` 直接纳入主程序项目的编译项——除非逐项核对命名空间与引用后手动整理。
- 建议：接口进 `ReminNote.Core.Reminders`，实现进 `ReminNote.Infrastructure` / `ReminNote.Agent`，与主程序分层一致。

## 4. 避免影响主程序的注意事项

1. **不改 `D:\Anime` 直到用户同意**（本阶段严格遵守）。
2. `ReminNote.Core` 保持不依赖 WPF / EF / Serilog / Windows API——桌宠 UI 只在 `ReminNote.Pet`。
3. 桌宠**不**复制 Design System / `UiText`，用 Link 复用，避免与主程序资源字典漂移。
4. 桌宠**不**直连 DbContext / SQL / 实体——只经 `IReminderQueryService`（读）与
   `IReminderCommandService`（写）；写由 Agent 串行化。
5. 不加第三方运行时依赖（动画用精灵图 + WPF `Storyboard`）。
6. 本地状态文件（位置/收纳/音量）与业务库隔离、可安全删除重建，不与业务数据耦合。

## 5. 兼容与降级

- 若主程序暂未提供 `[NEW]` 命令/查询，桌宠进入**兼容模式**：仅展示，交互退化为「打开主程序」，
  不阻塞主程序开发。
- 若系统 Toast 被禁用：桌宠仍提醒，并如实上报 `PET BLOCKED` 于通知健康。

## 6. 尚未确定的开放项

| 项 | 当前建议 | 待定理由 |
|----|----------|----------|
| 桌宠角色 | 先做 1 个 | 后期可换角色（资源字典/主题） |
| 是否默认开启 | 默认常驻，可在设置关闭 | 需产品确认 |
| 提醒音效 | 订阅可选 | 需产品确认 |
| 打开主程序方式 | 唤起 Main（`MainActivation`） | 复用既有管道 |
| `ReminderInstance` 表名/字段 | 以主程序 P3 实装为准 | 本模块只依赖只读 DTO |

## 7. 一键对接 checklist（未来执行）

- [ ] 确认主程序 P3 数据面已就绪
- [ ] 新增 `ReminNote.Pet` 项目并加入 sln
- [ ] 协议层常量扩展（ClientKinds/Operations/PipeNames）
- [ ] 实现 / 暴露 `IReminderQueryService`、`IReminderCommandService`、`IReminderFeed`、`IPetNotificationHealth`
- [ ] 接通 Reminder 只读投影 + Agent 命令落库
- [ ] 桌宠读改写全部经边界（无直连 DbContext）
- [ ] 通知健康登记桌面通道
- [ ] 手测：Toast 禁用仍提醒、高低 DPI、收纳还原、减少动效、高对比
- [ ] 结果：构建/测试通过，手测清单与已知限制归档
