# ReminNote.Pet — 桌宠骨架（切片 2）

> 状态：**独立可运行的桌宠骨架（Mock 触发），不接入真实数据**，完全独立于主程序 `D:\Anime`。
> 构建通过并已做启动冒烟验证。

## 这是什么

一个独立的 WPF 桌面程序，用来验证「提醒桌宠」的**窗口形态、动画机制、提醒气泡与动作、托盘/收纳**。
通知数据目前为 **Mock**，不连真实 Reminder 数据库。未来对接主程序时，按 `docs/10_HANDOFF.md`
把数据源替换为 `IReminderFeed / IReminderQueryService / IReminderCommandService`（见 `docs/06_INTERFACE_STUBS.md`）。

## 运行方式

环境：Windows + .NET 10 SDK。本项目**不引入任何第三方 NuGet 包**。

```powershell
# 构建（用已安装 SDK 的路径；下例为默认安装位置）
& 'C:\Program Files\dotnet\dotnet.exe' build 'D:\桌宠\ReminNote.Pet\ReminNote.Pet.csproj'

# 运行（Debug 输出）
& 'D:\桌宠\ReminNote.Pet\bin\Debug\net10.0-windows\ReminNote.Pet.exe'
```

也可以在 Visual Studio / Rider 打开 `ReminNote.Pet.csproj` 直接运行。

## 能演示什么

- **常驻悬浮窗**：无边框、**透明背景**、置顶、隐藏任务栏项；可**拖拽**，松手**吸附屏幕边缘**。
- **形象**：Q 版 DeepSeek「鲸鱼娘/大肥鱼」**透明 PNG**（`Assets/Sprite/` 下 `pet_idle / pet_idle_blink / pet_alert / pet_tap / pet_eat / pet_sleep`）。待机 = 主图+眨眼交替；提醒 = 惊讶；点按 = 捧白米饭。
- **Mock 提醒**：顶部工具条「任务提醒」/「动漫提醒」→ 弹出**提醒气泡**（标题/正文/语义/优先级 + 置顶标记）。
- **动作**：气泡内「完成 / 稍后 / 忽略 / 打开主程序」（当前为占位，未来经 Agent 命令回写）。
- **收纳与托盘**：点「收纳」或关窗 → 隐藏到系统托盘（`NotifyIcon`），托盘菜单可显示/触发/退出。
- **单实例**：重复启动只唤起已有实例（Mutex + 命名管道），不重复弹窗。
- **位置持久化**：窗口位置与收纳态存到 `%LOCALAPPDATA%\ReminNote.Pet\pet_state.json`（纯呈现状态）。

## 项目结构

```text
D:\桌宠\ReminNote.Pet\
  ReminNote.Pet.csproj      # net10.0-windows, UseWPF + UseWindowsForms(托盘), 零外部包
  app.manifest              # 普通用户权限清单
  App.xaml / App.xaml.cs    # 启动、单实例协调、异常入口
  PetWindow.xaml(.cs)       # 悬浮窗 UI + 拖动/吸附/托盘/Mock/动画推进
  ViewModels/
    ObservableObject.cs     # INotifyPropertyChanged 基类
    PetViewModel.cs         # 桌宠状态机 + Mock 提醒数据
  Animation/
    ISpriteSource.cs        # 帧源接口（可替换：将来换真实精灵图无需改驱动）
    PlaceholderSpriteSource.cs  # 程序化占位角色帧
  Startup/
    PetSingleInstanceCoordinator.cs  # Mutex + 命名管道单实例/激活
```

## 关键设计点（与文档的对应）

| 特性 | 说明 | 对应文档 |
|------|------|----------|
| 动画可替换 | `ISpriteSource` 接口；占位实现可换成 PNG 精灵图切片 | `docs/07_ASSET_SPEC.md` |
| 通道 | 桌宠是提醒的一个**呈现通道**，与 Toast/托盘并存 | `docs/03_DATAFLOW.md` |
| 数据源(Mock) | 当前用 `MockReminder` 占位，未来接 `IReminderFeed` | `docs/03_DATAFLOW.md` |
| 动作 | `完成/稍后/忽略/打开主程序` 当前为占位，未来经 Agent 命令 | `docs/04_REMINDER_MODEL.md` |
| 收纳/托盘 | 关窗=收纳，不退出；托盘承载退出 | `docs/08_WINDOW_UX.md` |

## 未来对接点（不做，指路）

- 把 `PetViewModel` 的 Mock 数据替换为 `IReminderFeed.PollAsync()` 的只读查询结果。
- 把气泡动作接到 `IReminderCommandService`（经 `Agent` 落库，幂等键 + revision）。
- 通知健康登记（`IPetNotificationHealth`）。见 `docs/10_HANDOFF.md` checklist。
