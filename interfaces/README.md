# interfaces/ — 待对接的接口草案

> **重要**：本目录下的 `.cs` 仅为「待对接的接口契约草案」，**不参与任何项目编译**。
> 它们不是 `D:\Anime` 主程序的一部分，也不在 `D:\Anime` 任何 `.sln/.csproj` 内。
> 用途：给未来对接提供**准确的接口骨架**，对接时由主程序按 `docs/10_HANDOFF.md` 拷贝/实现。

## 文件

| 文件 | 内容 |
|------|------|
| `ReminNote.Reminders.Contracts.cs` | 领域枚举 + `ReminderSnapshot` DTO + 客户端种类/操作/管道常量 |
| `ReminNote.Reminders.IServices.cs` | `IReminderQueryService` / `IReminderCommandService` / `IReminderFeed` / `IPetNotificationHealth` |

详细说明见 `docs/06_INTERFACE_STUBS.md` 与 `docs/05_AGENT_IPC_CONTRACT.md`。

## 约定

- 命名空间：`ReminNote.Core.Reminders`（建议；以主程序实际为准）。
- 标记：`[EXISTING]` = 主程序已有（复用）；`[NEW]` = 主程序需新增/暴露；`[PET-ONLY]` = 本模块私有。
- 本目录只用于**接口对齐**，不包含任何实现。实现侧代码在主程序 `ReminNote.Infrastructure` / `ReminNote.Agent`。
