# 09 · 里程碑与验收

> 遵循主程序规则：每个 Slice **小而可验证**，完成后报告改动、构建/测试结果、手工测试步骤与已知限制；
> **没有用户明确指示不自动进入下一个 Slice**。本模块先于主程序 P3 数据面可独立开发到某一步，
> 再对接真实 Reminder。

## 切片 0 · 资产与素材准备（可先行）

- [ ] 产出 `pet_idle / pet_alert / pet_tap` 精灵图 + `Meta/*.json` 元数据（规格见 `07_ASSET_SPEC.md`）。
- [ ] 生成角色主题母版，可后期换角色。

验收：导入后能在静态预览正确切片、透明背景无残留。

## 切片 1 · 数据前提（依赖主程序 P3 Reminder 数据面）

*（本模块只约定接口，不实现主程序侧）*

- [ ] 主程序提供 `IReminderQueryService.ListUnreadAsync`（只读投影）。
- [ ] 主程序提供 `IReminderCommandService`（Snooze / Ignore / MarkRead / Resolve）。
- [ ] `ProtocolClientKinds` 支持 `Pet`；`AgentTaskClient`（或新客户端）允许桌面命令子集。

验收：在测试壳中能列出 UNREAD、能下发命令且 `Agent` 落库（对照主程序既有 xUnit 验收方式）。

## 切片 2 · 桌宠进程骨架（可独立，不接真库）

- [ ] 新建 `ReminNote.Pet` 独立其项目；无边框透明置顶悬浮窗 + 单实例协调器 + 托盘。
- [ ] 待机态（`pet_idle`）可显示、拖拽、边缘吸附；关窗收纳到托盘。
- [ ] 用 **Mock/手动触发**（参照主程序 `SimulateAlertCommand` 思路）跑通「提醒态 + 动画 + 气泡 + 动作按钮」
     与「动作回写经命令边界（Mock 回调）」。
- [ ] 无障碍：`AutomationProperties` + `AccessibleFocusBrush` + 减少动效/高对比降级。

验收：手工触发能在提醒态正确显示动画与气泡；点击按钮产生命令（Mock 记录）；收纳/还原正常；高低 DPI 正常。

## 切片 3 · 接入真实 ReminderInstance

- [ ] 接入 `IReminderFeed`（默认轮询 `PollAsync`）：读 UNREAD → 去重 → 呈现提醒态。
- [ ] 用户动作经 `IReminderCommandService` 回写 `Agent`（Done/Snooze/Ignore/Resolve）。
- [ ] 处理生命周期 `UNREAD→READ`（展示即标记，可在设置中开关）。

验收：真实 P3 数据面下，提醒到点后桌宠出现动画与气泡；动作正确回写且 `Agent` 落库；
完成某一 Task 后其关联未来提醒不再弹出（Agent 负责）。

## 切片 4 · 打磨与通道健康

- [ ] 优先级/重复（HIGH / PIN+HIGH 强提醒）；安静时段呈现抑制；睡眠/恢复按 `purpose` 汇总。
- [ ] 多通道并存：登记桌宠到通知健康；Toast 禁用时桌宠仍提醒；如实记录 `PET BLOCKED`。
- [ ] `UiText` 文案资源键 + 本地化边界；本地状态文件与业务库隔离、可安全重建。

验收：Toast 禁用场景下报告 `PET BLOCKED` 但 remind truth 仍在；长期运行稳定。

## 里程碑汇总

| 里程碑 | 内容 | 依赖 |
|--------|------|------|
| M0 | 资产 + 素材 | 无 |
| M1 | 数据契约 | 主程序 P3 Reminder 数据面 |
| M2 | 桌宠进程骨架 + Mock | M0 |
| M3 | 接真实 Reminder | M1 + M2 |
| M4 | 打磨 + 通道健康 | M3 |

> 注意：M2 可先于 M1 独立进行（用 Mock），从而不阻塞主程序 P3 的开发。
> 真正对接在主程序 P3 数据面就绪后（见 `10_HANDOFF.md`）。
