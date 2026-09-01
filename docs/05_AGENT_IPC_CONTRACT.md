# 05 · Agent IPC 对接契约

> 本文描述桌宠**与主程序 Agent 交互**的 IPC 契约。当前契约基于主程序 `P2.5` 已落地的
> named-pipe 传输与幂等命令模型（`AgentTaskClient` / `ProtocolTransportProfileContractAdapter` /
> `ProtocolPipeNames`）。**新增项以「[NEW]」标注**，其余为复用既有定义。

## 1. 传输基础（复用 P2.5）

- 传输：命名管道（Named Pipe），`NamedPipeTransportClient` / `NamedPipeTransportEndpoint`。
- 端点：`Business`（命令）与 `ReadOnlyProjection`（只读投影），按 `profile` 隔离。
- 会话：连接后发送 `SessionHello`，声明能力；帧带 `RequestId` 关联请求/响应。
- 错误：`ProtocolResponse.Error`（`code` / `retryable` / `humanMessage`）；`AgentCommandException`。
- 幂等：写入带**幂等键 + current revision**；断线对同一幂等键**只重试一次**。

## 2. 客户端种类扩展 [NEW]

主程序 `ProtocolClientKinds` 目前只允许 `Main` / `Widget`（见 `AgentTaskClient` 构造校验）。
桌宠接入需新增：

```csharp
public static class ProtocolClientKinds
{
    public const string Main   = "main";
    public const string Widget = "widget";
    public const string Pet    = "pet";   // [NEW] 桌宠客户端
}
```

`AgentTaskClient` 构造校验需扩展为 `Main | Widget | Pet`；或者提供只允许 Pet 使用只读+
写命令子集的新客户端。桌宠**不**需要全部 Main 命令，建议按 `docs/06_INTERFACE_STUBS.md`
拆分暴露（具体接口签名以 `06` 与 `interfaces/` 为准）：

- `IReminderQueryService`（[NEW] 只读：`ListUnreadAsync` / `FindAsync`，见 §3）
- `IReminderCommandService`（[NEW] 写命令：`Snooze / Ignore / MarkRead / Resolve`，见 §4）
- `IReminderFeed`（[NEW] 触发感知统一视角，见 §5）
- `ITodayQueryService` / `ITaskQueryService`（[EXISTING] 只读，Pet 可选用于面板）

## 3. 只读查询（走 WAL projection）

沿用「同一 profile 的只读 SQLite 投影」方式（`ReadOnlyTaskServices` / `P25ReadOnlyConnectionFactory`）。
桌宠需要的只读查询：

| 查询 | 返回 | 用途 |
|------|------|------|
| `ListUnreadReminders(profile)` **[NEW]** | `ReminderInstance[]`（`UNREAD`） | 触发感知（方案 A 轮询） |
| `ListTodayTasks()` | `TaskSnapshot[]` | 桌宠面板（可选，复用现有 `TodayQuery`） |
| `ListUpcomingAnime()` | 追番/播出 | 桌宠面板（可选） |

> 只读查询不经过命令管道，直接读 WAL 投影；确保桌宠读到的提醒与 Agent 写库一致（WAL 快照）。

## 4. Agent 命令（写）[NEW 组]

在既有 `ProtocolOperations` 基础上新增提醒命令：

```csharp
public static class ProtocolOperations
{
    // 既有
    public const string SessionHello       = "session.hello";
    public const string TaskCreate         = "task.create";
    public const string TaskUpdatePlan     = "task.update_plan";
    public const string TaskRecordResult   = "task.record_result";
    public const string TaskDelete         = "task.delete";
    public const string TaskReorder        = "task.reorder";
    public const string TaskContinue       = "task.continue";

    // [NEW] Reminder 命令
    public const string ReminderSnooze     = "reminder.snooze";
    public const string ReminderIgnore     = "reminder.ignore";
    public const string ReminderMarkRead   = "reminder.mark_read";
    public const string ReminderResolve    = "reminder.resolve";
}
```

每个命令的 payload 建议（以 `ProtocolJson.CreateXxxPayload` 形如现有命令）：

| 命令 | 关键 payload 字段 | 返回 |
|------|------------------|------|
| `ReminderSnooze` | `reminderInstanceId`、`duration`(或 `targetAtUtc`)、`cause=SNOOZE` | 新派生 schedule 的 `scheduleId` |
| `ReminderIgnore` | `reminderInstanceId` | `resolved=true` |
| `ReminderMarkRead` | `reminderInstanceId` | `state=READ` |
| `ReminderResolve` | `reminderInstanceId`、`action`(`WATCHED/WATCH_LATER/SKIP/IGNORE`) | `state=RESOLVED` |

约定：所有写命令**幂等**、带 **revision**、失败不静默（超时返回稳定 busy，不覆盖原数据）。

## 5. 触发感知事件推送 [NEW，远期可选]

方案 A（轮询）不需要此节。方案 B 需要 Agent→Pet 的事件流：

- 订阅：`session.subscribe`（Pet 订阅 `ReminderInstance` 变化）。
- 推送帧：`event.reminder_triggered`，`{ instanceId, purpose, priority, payload, revision }`。
- 生命周期：连接期心跳、断线重连后补拉 `changes.get_since`（既有能力）做差集、事件幂等去重。
- 与既有 `changes.get_since`（Change Journal）复用，无需新存储。

> 本模块切片 1–3 采用方案 A，事件推送作为后续演进，不阻塞主流程。

## 6. 命名管道与激活 [NEW]

参照 `ProtocolPipeNames`，桌宠需要：

```csharp
public static string PetActivation(string profileScope, string petInstanceId) =>
    $"ReminNote.Pet.Activation.{profileScope}.{instanceHash}";
```

单实例：`Local\ReminNote.Pet.{profileScope}.{instanceHash}` Mutex + `PetActivation` 管道，
复用 `WidgetSingleInstanceCoordinator` 的实现逻辑（去重、唤起主窗口、关窗回托盘）。

## 7. 通知健康上报

参照主程序 `#21`。桌宠作为通道，向主程序/Agent 上报其可用性：

```csharp
// [NEW] 通道健康
PetNotificationStatus { Available, UIBlocked, Unavailable }
ChannelHealthEntry { Channel = "pet", Status, LastHeartbeatUtc }
```

- 历史仍由 Agent 记录（`TRIGGERED · PET BLOCKED`），桌宠不伪造触发。
- 主程序 P3 验收要求「Toast 不可用时提醒仍有效」→ 桌宠健康需如实上报，供主程序兜底。

## 8. 协议版本与兼容

- 参考 `ProtocolVersion.Current`；桌宠 hello 声明其支持能力子集。
- 若主程序未提供某 `[NEW]` 命令/查询，桌宠必须**降级**：进入「只展示、仅唤起主程序、不出桌面动作」
  的兼容模式；不阻塞主程序 P3 数据面未就绪时的独立开发（切片 2 用 Mock）。
