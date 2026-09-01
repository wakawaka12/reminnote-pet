# 01 · 概述

## 1. 为什么做「桌宠」

ReminNote 的提醒目前规划为多通道呈现（Windows Toast / Tray / Widget Alert / 声音 / 唤醒定时器，
见主程序 `#21 Notification channels`）。「桌宠」希望把最重要的提醒做得**更亲切、更在场、更符合桌面常驻
软件的直觉**——一个待在桌角的角色，在被提醒时主动跳出来，并承载 `DONE / SNOOZE / IGNORE` 等
轻量交互，而不是只弹一条系统通知。

## 2. 目标

- 提供一个**常驻桌面、可拖动、可收纳**的提醒载体（桌宠）。
- 在 Reminder 触发时，以**动画 + 气泡**的形式把提醒呈现在用户面前。
- 让用户直接在桌宠上完成主要提醒动作：`完成 / 稍后提醒 / 忽略 / 去主程序处理`。
- **严格遵守主程序现有架构**：桌宠是「呈现通道/交互载体」，绝不是新的领域模型。

## 3. 非目标（明确不做）

- 不新增 Reminder 领域模型——沿用主程序冻结的 `ReminderRule / ReminderSchedule / ReminderInstance` 三层。
- 不做 Reminder 调度（调度由主程序 Agent 拥有，见 `#18 Scheduler`）。
- 不直连数据库 / EF entity / SQL——桌宠只能经「只读查询边界」读、经「Agent 命令」写。
- 不引入联网、同步、Anime 网络、插件系统。
- 不替代主程序 Main 应用；桌宠是**独立常驻进程**，不依赖 Main 存活。
- 不重复实现 Design System——通过 Link 复用主程序 `ReminNote.Windows` 的 Design System 与 `UiText` 资源。

## 4. 用户已确认的基线

| 决策点 | 结论 | 影响 |
|--------|------|------|
| 桌宠形态 | 常驻桌面，可拖动/收纳 | 需要一个常驻无边框悬浮窗 + 待机/提醒双态 |
| 动画实现 | 精灵图序列帧 + WPF `Storyboard` | 需要资产规格（`07_ASSET_SPEC.md`），零额外运行时依赖 |
| 与系统通知关系 | 多通道并存 | 桌宠是**一个通道**，不替代 Toast；需登记进通知健康 |

## 5. 与主程序的时间线关系

主程序 Reminder 系统属于 **P3（Production Reminder loop）** 范围。因此：

- 本模块的**切片 1（数据前提）**依赖主程序已提供 Reminder 只读查询 + Agent 命令契约；
  这部分在主程序 P3 内实现，本模块只预订其接口。
- 桌宠进程本身可**提前**用 Mock / 手动触发开发（切片 2），不阻塞。
- 真正「接入真实 ReminderInstance」在切片 3，需在 P3 数据面就绪后进行。

## 6. 术语

| 术语 | 含义 |
|------|------|
| 主程序 | ReminNote 仓库（`D:\Anime`），本模块的对接对象 |
| 桌宠 / Pet | 本模块的常驻悬浮窗口与角色 |
| ReminderInstance | 已发生提醒的事实记录（`UNREAD → READ → RESOLVED`） |
| purpose | 提醒语义（如 `TASK_PRE_START` / `ANIME_AIRING`），决定恢复/安静时段行为 |
| Agent | 主程序唯一业务写入者，桌宠的命令端 |
| 通道健康 | 记录提醒是否触发、各呈现通道是否可用（Toast 禁用 ≠ 提醒丢失） |
