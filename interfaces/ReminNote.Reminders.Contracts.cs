// interfaces/ReminNote.Reminders.Contracts.cs
//
// 【待对接接口草案 · 不参与任何项目编译】
// 用途：给 ReminNote 提醒桌宠与主程序之间的对接提供准确的领域契约骨架。
// 对接时由主程序按 docs/10_HANDOFF.md 拷贝/实现，进命名空间 ReminNote.Core.Reminders。
//
// 标记：[EXISTING]=主程序已有(复用)  [NEW]=主程序需新增/暴露  [PET-ONLY]=本模块私有

using System;

namespace ReminNote.Core.Reminders
{
    // [EXISTING] 主程序冻结的提醒语义；枚举名以主程序实际为准
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
    public enum ReminderPriority
    {
        Low,
        Normal,
        High,
    }

    // [EXISTING] 生命周期
    public enum ReminderInstanceState
    {
        Unread,
        Read,
        Resolved,
    }

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
        public string? OccText { get; init; }
    }

    // [NEW] 通道健康
    public enum PetNotificationStatus
    {
        Available,
        UiBlocked,
        Unavailable,
    }

    // [NEW] 通道健康条目
    public sealed record ChannelHealthEntry
    {
        public required string Channel { get; init; }           // "pet"
        public required PetNotificationStatus Status { get; init; }
        public required DateTimeOffset LastHeartbeatUtc { get; init; }
    }

    // [EXISTING 需扩展] 客户端种类（Main / Widget / Pet）
    public static class ProtocolClientKinds
    {
        public const string Main = "main";
        public const string Widget = "widget";
        public const string Pet = "pet";   // [NEW]
    }

    // [EXISTING 需扩展] 协议操作
    public static class ProtocolOperations
    {
        public const string SessionHello = "session.hello";
        public const string TaskCreate = "task.create";
        public const string TaskUpdatePlan = "task.update_plan";
        public const string TaskRecordResult = "task.record_result";
        public const string TaskDelete = "task.delete";
        public const string TaskReorder = "task.reorder";
        public const string TaskContinue = "task.continue";

        // [NEW] Reminder 命令
        public const string ReminderSnooze = "reminder.snooze";
        public const string ReminderIgnore = "reminder.ignore";
        public const string ReminderMarkRead = "reminder.mark_read";
        public const string ReminderResolve = "reminder.resolve";
    }

    // [NEW] 命令幂等 + revision（复用 AgentTaskClient 既有约定）
    public sealed record ReminderCommandPayload(
        Guid ReminderInstanceId,
        string IdempotencyKey,
        long ExpectedRevision);
}
