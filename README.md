# 🐋 大肥鱼桌宠(Whale Pet)

> DeepSeek「鲸鱼娘 · 大肥鱼」风格的**桌面宠物游戏**,WPF / .NET 10 实现。
> **完全独立运行、独立分发**——不依赖任何主程序或应用。
> 社区二创作品,**仅供个人学习与娱乐,禁止商用**(见 [LICENSE.md](LICENSE.md) / [NOTICE.md](NOTICE.md))。

## ✨ 亮点

- **贴边跑酷**:角色贴上屏幕边缘,立即变成绕屏奔跑的游戏角色——
  底边正常跑、**顶边倒挂(头朝下)**、两侧贴墙爬,到角自动转弯;
- **16 帧逐帧动画**:AI 生成精灵帧 + 机械切片,60fps 平滑移动、帧播放 16fps;
- **随机台词气泡**:「我不是吃白饭的!」「摸鱼摸鱼～」「要努力才有饭吃!」等大肥鱼社区梗随机冒出;
- **完整桌面体验**:拖拽、屏幕边缘吸附、托盘收纳、单实例、位置记忆;
- **内置互动演示**:顶部工具条可随时触发演示;未来可无缝接入真实数据流(接口契约见 `docs/`、`interfaces/`)。

## 🚀 快速开始

环境:Windows + .NET 10 SDK

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build '.\ReminNote.Pet\ReminNote.Pet.csproj'
& '.\ReminNote.Pet\bin\Debug\net10.0-windows\ReminNote.Pet.exe'
```

> ℹ️ 项目目录/工程名沿用了创建时的历史命名(`ReminNote.Pet`),**项目本体是完全独立的桌宠,与任何笔记/记事本应用无关**;未来可整体重命名。

## 📦 直接运行发布包

下载 Releases 中的 `ReminNote.Pet-win-x64.zip`(需已装 .NET 10 桌面运行时),解压后双击 `ReminNote.Pet.exe`。

## 🎨 素材管线(自定义形象)

全部素材由 AI 图像生成 + 脚本预处理,换形象只需两步:

```powershell
# 1) 原始白底图放入 pet_assets_raw\(文件名含 drag / hidden_top / run 等关键字)
# 2) 执行预处理(抠图 / 接触边对齐 / 白边去污):
$env:PET_BASE='D:\桌宠'; & '.\ReminNote.Pet\tools\preprocess_sprites.ps1'
```

- 多帧精灵集(行条幅):命名 `*_sheet.png` 放入 `Assets\Sprite\` 即自动逐帧播放(列数在 `PetWindow` 配置);
- 单帧扩展:同一角色更多姿态(待机 / 提醒 / 点按 / 拖动 / 跑酷)同法接入。

## ⚡ 玩法一览

| 操作 | 结果 |
|------|------|
| 拖动角色 | 跟随鼠标移动,松手吸附边缘 |
| 贴住屏幕边缘 | 进入**跑酷模式**:绕屏奔跑 + 台词气泡 |
| 跑酷中按鼠标 | 停止,回到待机 |
| 收纳 / 托盘 | 隐藏到系统托盘,托盘可唤回 |

## 📄 许可

- **代码**:MIT License(见 [LICENSE.md](LICENSE.md));
- **美术与梗**:社区二创 + AI 生成,**禁止商用、非 DeepSeek 官方作品**(见 [NOTICE.md](NOTICE.md))。
