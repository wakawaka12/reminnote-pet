// interfaces/ReminNote.Reminders.IServices.cs
//
// 【待对接接口草案 · 不参与任何项目编译】
// 用途：桌宠与主程序之间的服务接口契约。实现由主程序（Agent/Infrastructure 侧）承担。
// 对接时按 docs/10_HANDOFF.md 拷贝进主程序；桌宠只依赖本接口 + 只读 DTO，不触碰实现。
//
// 标记：[EXISTING]=主程序已有(复用)  [NEW]=主程序需新增/暴露  [PET-ONLY]=本模块私有

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReminNote.Core.Reminders
{
    // [NEW] 只读查询：触发感知（方案 A 轮询）
    public interface IReminderQueryService
    {
        ValueTask<IReadOnlyList<ReminderSnapshot>> ListUnreadAsync(
            CancellationToken cancellationToken = default);

        ValueTask<ReminderSnapshot?> FindAsync(
            Guid instanceId,
            CancellationToken cancellationToken = default);
    }

    // [NEW] 命令服务（写）：一切写经 Agent 命令 + 幂等键 + revision
    public interface IReminderCommandService
    {
        ValueTask<Guid> SnoozeAsync(
            Guid instanceId,
            TimeSpan duration,
            CancellationToken cancellationToken = default);

        ValueTask ResolveIgnoreAsync(
            Guid instanceId,
            CancellationToken cancellationToken = default);

        ValueTask MarkReadAsync(
            Guid instanceId,
            CancellationToken cancellationToken = default);

        ValueTask ResolveAsync(
            Guid instanceId,
            ReminderResolveAction action,
            CancellationToken cancellationToken = default);
    }

    // [NEW] 触发感知统一视角：桌宠不区分轮询/推送
    public interface IReminderFeed : IAsyncDisposable
    {
        ValueTask<IReadOnlyList<ReminderSnapshot>> PollAsync(
            CancellationToken cancellationToken = default);

        // 可选：推送订阅（方案 B 演进）。未实现时抛 NotSupportedException，降级为纯轮询。
        IAsyncEnumerable<ReminderSnapshot> SubscribeAsync(
            CancellationToken cancellationToken = default);
    }

    // [NEW] 通道健康（[EXISTING 概念复用]：主程序已规划通知健康，#21）
    public interface IPetNotificationHealth
    {
        ValueTask ReportAsync(
            ChannelHealthEntry entry,
            CancellationToken cancellationToken = default);
    }
}
