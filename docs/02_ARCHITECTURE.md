# 02 · 架构

## 1. 总原则

桌宠遵守主程序既有架构约束：

- **Agent 是唯一业务写入者**。桌宠写操作一律经 Agent 命令；读操作走同一 profile 的只读投影。
- **Core 不依赖 WPF / EF / Serilog / Windows API**。本模块的 UI 呈现与「读改写边界」分离。
- **独立进程**。桌宠常驻、轻量，不能因主程序 Main 崩溃而消失；也不给 Main 增加常驻负担。

## 2. 分层视图

```text
┌──────────────────────────────────────────────────────┐
│  ReminNote 主程序                                     │
│   Agent (唯一写入者)                                   │
│     · Reminder Scheduler (#18)                          │
│     · ReminderInstance 持久化                           │
│     · 只读投影 (WAL)      ◄────┐                         │
└──────────────────────────┬────┼────────────────────────┘
        ┌──────────────────┴────┴───────────────┐
        │  IPC 面                                │
        │  · business 命名管道  (命令, 幂等键+rev)  │
        │  · 只读投影         (WAL SQLite 只读)    │
        └───────────────────┬──────────────────┘
                            │
┌───────────────────────────┴───────────────────────────┐
│  本模块：ReminNote.Pet (兼容层/客户端, 独立进程)         │
│   PetViewModel  →  PetWindow (无边框透明悬浮)           │
│   (只读查询)      (Agent 命令)                          │
│   └──────────────┴──────────────┐                     │
│   Assets/  精灵图资源            │                     │
└────────────────────────────────┼─────────────────────┘
                                 │  只消费 ReminderInstance + 回写动作
```

## 3. 进程边界

桌宠是一个独立 WPF 进程，对应主程序里 `ReminNote.Widget` 的同类地位，但语义不同：

- Widget：主程序的**信息面板**（Today/Anime 内容 + 提醒抽屉，被动刷新）。
- Pet：**提醒载体**（常态待机 + 提醒动画 + 动作交互），强调「到场感」。

桌宠可复用的 Widget 技术底座（本模块只引用其形态，不依赖 Widget 进程）：

| 复用项 | 来源 | 说明 |
|--------|------|------|
| 单实例协调器 | `WidgetSingleInstanceCoordinator` | Mutex + 命名管道去重/唤起，Pet 用 `Local\ReminNote.Pet.xxx` |
| 无边框透明悬浮窗 | `WidgetWindow.xaml` | `WindowStyle=None / AllowsTransparency / WindowChrome` |
| 应用边界客户端 | `AgentTaskClient` | 写入经 business 管道，读取经 WAL 只读投影 |
| 只读查询服务 | `ReadOnlyTaskServices` / `P25ReadOnlyConnectionFactory` | 复用「同一 profile 只读」的读取方式 |
| 触发模拟 | `SimulateAlertCommand` 思路 | 切片 2 用 Mock/手动触发跑通，不接真库 |

## 4. 提议的项目结构

主程序新增一个项目（对接阶段由主程序按 `10_HANDOFF.md` 落地；本模块先给出目录契约）：

```text
src/windows/ReminNote.Pet/
  ReminNote.Pet.csproj            # 参照 ReminNote.Widget，net10.0-windows
  App.xaml / App.xaml.cs          # 启动、单实例、刷新定时器
  PetWindow.xaml / .cs            # 无边框透明 Topmost 悬浮窗（待机/提醒态容器）
  ViewModels/
    PetViewModel.cs               # 待机态/提醒态建模 + DONE/SNOOZE/IGNORE 命令
    ReminderAlertViewModel.cs     # 单个提醒的气泡/动作状态
  Startup/
    PetSingleInstanceCoordinator.cs # 复用 Widget 那套 Mutex+管道
    PetStartupOptions.cs          # 命令行参数（repo-root / profile / instance id）
  Assets/                         # 精灵图(SpriteSheet) + 待机/提醒/点按帧
    Sprite/<name>.png
    Sprite/<name>.json            # 帧元数据（尺寸/帧序/帧率）
  Resources/
    PetResources.xaml             # 桌宠专用样式（使用 Design System 资源键）
```

## 5. 依赖边界（对齐主程序）

| 依赖 | 要求 |
|------|------|
| 主程序 `ReminNote.Core` | 只依赖协议常量、领域类型与**只读**查询接口；不触碰 EF/WPF |
| 主程序 `ReminNote.Infrastructure` | 仅经 `AgentTaskClient` / `ReadOnlyTaskServices` 这类**边界**使用，不直连 DbContext |
| 主程序 `ReminNote.Windows` 资源 | 通过 `.csproj` Link 复用 Design System 与 `UiText`，**不复制**（对齐 `ARCHITECTURE.md` P0-07） |
| 本模块不新增第三方运行时依赖 | 动画用 WPF `Storyboard` + 精灵图；不引入 Lottie / 其它 UI 库 |

## 6. 技术选型

| 方面 | 选择 | 理由 |
|------|------|------|
| 框架 | WPF + `net10.0-windows` | 与主程序一致 |
| MVVM | `CommunityToolkit.Mvvm` | 与主程序一致 |
| 动画 | 精灵图序列帧 + `Storyboard`/`DispatcherTimer` 帧推进 | 开源友好、零依赖、可控帧率 |
| 读改写 | `AgentTaskClient`（命令） + `ReadOnlyTaskServices`（只读） | 复用已有边界，Agent 唯一写入者 |
| 单实例 | Mutex + 命名管道激活 | 复用 Widget 已验证实现 |
| 常驻 | 系统托盘 + 可关闭到托盘 | 不被误解为退出；关窗=收纳到托盘 |
