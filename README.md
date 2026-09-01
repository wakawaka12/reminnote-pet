# 🐋 ReminNote 提醒桌宠(开源版)

> DeepSeek「鲸鱼娘·大肥鱼」风格桌宠:**待机陪伴 + 贴边跑酷 + 提醒气泡**,WPF/.NET 10 实现。
> 社区二创作品,**仅供个人学习娱乐、禁止商用**;详见 [LICENSE.md](LICENSE.md) 与 [NOTICE.md](NOTICE.md)。

## 亮点
- **贴边跑酷**:角色贴上屏幕边缘即开始绕屏奔跑(底边正跑/顶边倒挂/两侧爬墙),16 帧跑步动画 + 随机台词气泡(「我不是吃白饭的!」「摸鱼摸鱼～」…);
- **提醒气泡**:完成/稍后/忽略/打开主程序(骨架期 Mock 数据,接口契约见 `docs/` 与 `interfaces/`);
- **单实例 / 托盘 / 位置持久化 / 无障碍细节** 齐全;
- **素材管线**:GPT 生成精灵帧 → `tools\` 预处理(抠图/对齐/切格)→ 一行命令接入新素材。

## 运行(源码)
环境:Windows + .NET 10 SDK。

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build 'ReminNote.Pet\ReminNote.Pet.csproj'
& '.\ReminNote.Pet\bin\Debug\net10.0-windows\ReminNote.Pet.exe'
```

## 直接运行发布包
下载 `Releases` 的 `ReminNote.Pet-win-x64.zip`(需已装 .NET 10 桌面运行时),解压后双击 `ReminNote.Pet.exe`。

## 素材管线(换图)
```powershell
# 原始白底图放 D:\桌宠\pet_assets_raw\(命名含 drag/hidden_top/.../run),然后:
$env:PET_BASE='D:\桌宠'; & '.\ReminNote.Pet\tools\preprocess_sprites.ps1'
# 多帧精灵集(行条幅):pet/run 等,命名 *_sheet.png 放入即可
```

## 许可
- 代码:**MIT**(LICENSE.md)
- 美术/形象/梗:社区二创 + AI 生成,**禁止商用,非官方作品**(NOTICE.md)

---

# ReminNote 提醒桌宠 — 独立设计模块(原设计文档)

> 状态：**设计中，尚无对接主程序**。本目录是一套「待对接」的完整设计文档 + 接口契约 +
> 一个**可独立运行**的桌宠骨架（切片 2，Mock 触发），用于把 ReminNote（主程序仓库位于 `D:\Anime`）
> 的提醒功能做成**桌面宠物**形态。

## 这是什么

这是 ReminNote「提醒桌宠」的独立设计与接口包。它**不改动、不编译、不依赖**主程序仓库，
所有内容封闭在 `D:\桌宠` 下。目标是先把方案、接口、资产规格、对接说明写清楚，
等主程序的 Reminder 数据与 Agent 命令就绪后，再按 `docs/10_HANDOFF.md` 一次性对接。

## 目录导航

| 路径 | 内容 |
|------|------|
| `docs/01_OVERVIEW.md` | 定位、目标、非目标、已确认基线 |
| `docs/02_ARCHITECTURE.md` | 架构分层、Pet 进程、模块划分、技术选型 |
| `docs/03_DATAFLOW.md` | 数据流、触发/回写、通道健康 |
| `docs/04_REMINDER_MODEL.md` | 与主程序 Reminder 领域模型的对接映射 |
| `docs/05_AGENT_IPC_CONTRACT.md` | 与 Agent 的 IPC 对接契约（客户端种类/管道/只读查询/命令/事件） |
| `docs/06_INTERFACE_STUBS.md` | 主程序需暴露的接口与类签名（C# 草案） |
| `docs/07_ASSET_SPEC.md` | 精灵图序列帧资产规格 |
| `docs/08_WINDOW_UX.md` | 悬浮窗交互与无障碍规格 |
| `docs/09_MILESTONES.md` | 切片/里程碑/验收 |
| `docs/10_HANDOFF.md` | 对接主程序的改动点清单与操作说明 |
| `interfaces/` | 待对接的接口草案（.cs，**仅供查看，不参与任何编译**） |
| `ReminNote.Pet/` | **切片 2：可独立运行的桌宠骨架**（Mock 触发；构建通过 + 冒烟验证过），见其 `README.md` |

## 与主程序的关系（边界声明）

- **只读参考**：本模块引用主程序已有的类名、命名空间、协议常量，仅为对齐接口，不复制实现。
- **零侵入**：`D:\桌宠` 不在主程序任何 `.sln` / `.csproj` 的编译范围内，本目录文件不会进入主程序构建。
- **接口契约以 `interfaces/` 与 `docs/06_INTERFACE_STUBS.md` 为准**：对接时由主程序按 `10_HANDOFF.md` 实现/暴露。
- **不在未征得同意前修改 `D:\Anime`**：一切主程序改动只在对接阶段进行。

## 关键先行结论（用户已确认）

| 决策点 | 结论 |
|--------|------|
| 桌宠形态 | **常驻桌面，可拖动/收纳**（平时待在角落，点击展开小面板 + Reminder Drawer） |
| 动画实现 | **精灵图序列帧（SpriteSheet）** + WPF `Storyboard`，零额外运行时依赖 |
| 与系统通知关系 | **多通道并存**（Toast / Tray / Widget Alert / 桌宠并列；Toast 被禁用时桌宠仍可提醒） |

## 权威上游依据

本模块的设计约束全部来源于主程序仓库文档：
`ReminNote_MASTER_DEVELOPMENT_PLAN.md`（Reminder 架构 `#11–#22`）、`ARCHITECTURE.md`、
`PRODUCT_RULES.md`。核心边界：**Agent 是唯一写入者**；`Rule` 是业务真值，`Schedule` 可重建，
`Instance` 是历史事实；Snooze 只派生临时 schedule；Core 不依赖 WPF/EF/Serilog/Windows API。
