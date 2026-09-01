# 06 · 接口草案（对接主线：主程序需暴露/实现）

> 本文是「留好接口」的核心。给出桌宠与主程序之间**应有的 C# 接口契约**。
> 拟放于命名空间 `ReminNote.Core.Reminders`（建议，以主程序实际为准）。
> 这些接口**由主程序（Agent/Infrastructure 侧）实现**，桌宠只依赖接口层，不触碰实现。
>
> 标记约定：`[EXISTING]` 主程序已有（复用）；`[NEW]` 主程序需新增/暴露；`[PET-ONLY]` 本模块私有。

## 0. 原则

- 接口应**小、面向语义**（读查询 / 命令 / 呈现反馈 / 健康）。
- 桌宠不依赖任何主程序实现类型，只依赖上述接口 + 不可变 DTO。
- 所有异步方法带 `CancellationToken`；命令方法带幂等键与 revision（见 §5）。

## 1. 领域枚举与 DTO

```csharp
namespace ReminNote.Core.Reminders;

// [EXISTING] 主程序冻结的语义；枚举名以主程序为准
public enum ReminderPurposeKind
{
    TaskPreStart,
    TaskStart,
    TaskRangeEnd,
    TaskCustom,
    AnimePreAiring,
    AnimeAiring,
    AnimeCustom,
}

// [EXISTING] 优先级
public enum ReminderPriority { Low, Normal, High }

// [EXISTING] 生命周期
public enum ReminderInstanceState { Unread, Read, Resolved }

// [EXISTING] 用户/领域解析动作
public enum ReminderResolveAction
{
    Done,
    Snooze,
    Ignore,
    Watched,
    WatchLater,
    Skip,
}

// [NEW] 桌面展示所需的只读快照（不可变）
public sealed record ReminderSnapshot
{
    public required Guid InstanceId { get; init; }
    public required ReminderPurposeKind Purpose { get; init; }
    public required ReminderPriority Priority { get; init; }
    public required bool IsPinned { get; init; }
    public required ReminderInstanceState State { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public Guid? TaskId { get; init; }
    public string? TaskTimeRange { get; init; }
    public string? OccText { get; init; }   // 用于去重/排序的可选语义文本
}
```

## 2. 只读查询服务 [NEW]

```csharp
public interface IReminderQueryService
{
    // 触发感知（方案 A 轮询：取当前 profile 未读提醒）
    ValueTask<IReadOnlyList<ReminderSnapshot>> ListUnreadAsync(
        CancellationToken cancellationToken = default);

    // 按 Id 取单个（用于刷新详情）
    ValueTask<ReminderSnapshot?> FindAsync(Guid instanceId,
        CancellationToken cancellationToken = default);
}
```

## 3. 命令服务（写）[NEW]

```csharp
public interface IReminderCommandService
{
    // 稍后提醒（派生临时 schedule，不改 Rule）
    ValueTask<Guid> SnoozeAsync(Guid instanceId, TimeSpan duration,
        CancellationToken cancellationToken = default);

    // 忽略当前 Instance（不静默关闭未来规则）
    ValueTask ResolveIgnoreAsync(Guid instanceId,
        CancellationToken cancellationToken = default);

    // Read 标记（展示过）
    ValueTask MarkReadAsync(Guid instanceId,
        CancellationToken cancellationToken = default);

    // 通用的领域解析（Done/Watched/WatchLater/Skip）
    ValueTask ResolveAsync(Guid instanceId, ReminderResolveAction action,
        CancellationToken cancellationToken = default);
}
```

> 写路径统一由 `AgentTaskClient`（或其扩展）转发到 business 管道：带幂等键 + current revision，
> 由 Agent 落库。桌宠侧不直接触碰 `IReminderCommandService` 实现。

## 4. 触发感知的统一视角 [NEW]

桌宠调用方不区分「轮询 / 推送」，统一读此接口：

```csharp
public interface IReminderFeed : IAsyncDisposable
{
    // 拉取增量：返回自 lastSeenConsecutiveAnchor 以来新增/变化的未读提醒
    ValueTask<IReadOnlyList<ReminderSnapshot>> PollAsync(
        CancellationToken cancellationToken = default);

    // 可选：推送订阅（方案 B 演进）。未实现时可抛 NotSupportedException 降级为纯轮询。
    IAsyncEnumerable<ReminderSnapshot> SubscribeAsync(CancellationToken cancellationToken = default);
}
```

- 默认实现 = 轮询（每次 `PollAsync` → `IReminderQueryService.ListUnreadAsync` + 本地去重）。
- 若主程序日后提供推送，可替换 `SubscribeAsync` 实现，**接口不变**。

## 5. 命令幂等与 revision（[EXISTING]，复用）

沿用 `AgentTaskClient` 的既有约定：

```csharp
// [EXISTING] 示意，主程序已有
public sealed record ReminderCommandPayload(
    Guid ReminderInstanceId,
    string IdempotencyKey,
    long ExpectedRevision);
```

- `IdempotencyKey`：`ProtocolIds.NewIdempotencyKey()`（每命令生成一次）。
- `ExpectedRevision`：写前读取 `revision_state`（`P25ReadOnlyConnectionFactory` 读取）。
- 断线仅对同一幂等键**重试一次**；超时返回稳定 busy，不静默覆盖。

## 6. 通道健康 [NEW]

```csharp
public enum PetNotificationStatus { Available, UiBlocked, Unavailable }

public sealed record ChannelHealthEntry
{
    public required string Channel { get; init; }      // "pet"
    public required PetNotificationStatus Status { get; init; }
    public required DateTimeOffset LastHeartbeatUtc { get; init; }
}

public interface IPetNotificationHealth
{
    ValueTask ReportAsync(ChannelHealthEntry entry, CancellationToken cancellationToken = default);
}
```

## 7. （可选）桌宠内部接口 [PET-ONLY]

这些属于本模块内部，未来独立成项目 `ReminNote.Pet` 时使用，不要求主程序暴露：

```csharp
namespace ReminNote.Pet;

// 待机/提醒态视图模型（示意）
public interface IPetViewModel { ... }        // 状态迁移 + 命令（DONE/SNOOZE/IGNORE）
public interface IPetAnimationDriver { ... }  // 精灵帧推进、事件回调
public interface IPetPlacementStore { ... }   // 位置/收纳态本地持久化（与业务库隔离）
```

> 该部分仅作占位，帮助未来分模块；主程序对接只需要 §1–§6。

## 8. 与主程序既有接口的关系

| 本模块接口 | 建议由主程序哪里实现/暴露 |
|------------|--------------------------|
| `IReminderQueryService` | Agent（P3 Reminder 数据面）经只读投影返回 |
| `IReminderCommandService` | `AgentTaskClient` 扩展（business 管道） |
| `IReminderFeed` | `AgentTaskClient`（默认轮询实现，可换推送） |
| `IPetNotificationHealth` | Agent 侧登记，或主程序 `NotificationHealth` 聚合 |
| `TodayQuery` / `TaskQuery` | [EXISTING] 直接复用 `ITodayQueryService` / `ITaskQueryService` |

> 详细对接步骤见 `docs/10_HANDOFF.md`。
