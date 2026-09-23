# 🐋 大肥鱼桌宠(Whale Pet)

> DeepSeek「鲸鱼娘 · 大肥鱼」风格的**桌面宠物游戏** —— 平时安静陪伴,贴上屏幕边缘就绕屏奔跑。
> Windows · C# · .NET 10 · WPF · **零第三方依赖** · 独立运行、独立分发。

**平台** Windows 10/11 ｜ **运行时** .NET 10 Desktop ｜ **代码许可** [MIT](LICENSE) ｜ **素材许可** [CC BY-NC 4.0](ASSETS-LICENSE.md)

---

## ✨ 功能亮点

| | |
|---|---|
| 🏃 **贴边跑酷** | 角色贴上屏幕任意边缘即进入跑酷:沿边缘**顺时针绕屏奔跑**,到角自动转弯;底边正常跑、**顶边倒挂(头朝下)**、两侧贴墙爬 |
| 🎞️ **16 帧精灵动画** | 自研 SpriteSheet 切片驱动,按列逐帧播放(16fps),配合 **60fps 平滑移动** |
| 💬 **随机台词气泡** | 跑动中随机冒出「我不是吃白饭的!」「摸鱼摸鱼～」等社区梗,气泡跟随角色、方向自适应 |
| 🔔 **提醒气泡** | 完成 / 稍后 / 忽略 三个动作按钮,可随时触发演示 |
| 🖥️ **全屏防打扰** | 检测到全屏应用(游戏 / 播放器)时**自动隐藏**,退出全屏自动恢复,打游戏时不打扰 |
| 🖱️ **桌面体验** | 无边框透明置顶窗、拖拽移动、屏幕边缘吸附、托盘收纳、单实例互斥、位置记忆 |
| 🎨 **素材管线** | AI 生成帧 → 脚本自动抠图 / 对齐 / 去白边 / 切格,一条命令换形象 |

## 🎮 玩法

| 操作 | 效果 |
|------|------|
| 拖动角色 | 跟随鼠标移动,松手后吸附屏幕边缘 |
| 贴住屏幕边缘 | 进入**跑酷模式**:绕屏奔跑 + 随机台词 |
| 跑酷中按住鼠标 | 停下,回到待机 |
| 顶部「收纳」/ 关闭窗口 | 隐藏到系统托盘(托盘可唤回 / 退出) |
| 打开全屏游戏 | 自动隐藏,退出全屏后自动回来 |

## 🚀 快速开始(源码运行)

环境:Windows + [.NET 10 SDK](https://dotnet.microsoft.com/download)

```powershell
dotnet build .\dafeiyu\dafeiyu.csproj
.\dafeiyu\bin\Debug\net10.0-windows\dafeiyu.exe
```

> ℹ️ 工程名 `dafeiyu` 是「大肥鱼」的拼音。

## 📦 直接运行(发布包)

前往本仓库 **Releases** 下载 `dafeiyu-win-x64.zip`(需已安装 .NET 10 桌面运行时),
解压后双击 `dafeiyu.exe` 即可,桌面右下角会出现大肥鱼。

自行打包:

```powershell
dotnet publish .\dafeiyu\dafeiyu.csproj -c Release -o .\release\dafeiyu
# 再把 dafeiyu\Assets 复制到发布目录,压缩成 zip 即可
```

## 🎨 素材管线(换成你自己的角色)

素材完全由脚本处理,不依赖任何图像库:

```powershell
# 1) 原始白底图放入 pet_assets_raw\
#    文件名含关键字即可: drag / hidden_top / hidden_bottom / hidden_left / hidden_right / run
# 2) 执行预处理(白底抠图 → 接触边对齐 → 白边去污):
$env:PET_BASE='<你的项目根目录>'; .\dafeiyu\tools\preprocess_sprites.ps1
```

- **单帧姿态**:输出 `Assets\Sprite\pet_*.png`;
- **多帧精灵表**:命名 `*_sheet.png` 放入 `Assets\Sprite\` 即自动逐帧播放(帧数在 `PetWindow` 中配置);
- 附带的 `tools\` 还提供行条幅切分(`split_strip.ps1`)与精灵表重排(`fix_sheet_strip.ps1`)。

## 🧩 项目结构

```text
dafeiyu/
├── dafeiyu.csproj                 # net10.0-windows / WPF + WinForms(托盘) / 零外部包
├── App.xaml(.cs)                  # 启动、单实例协调、全局异常日志
├── PetWindow.xaml(.cs)            # 悬浮窗 UI、拖拽吸附、跑酷状态机、全屏检测
├── ViewModels/                    # 状态机与演示数据
├── Animation/                     # 帧源:ISpriteSource / ImageSpriteSource / SpriteSheetSource / 程序化占位
├── Startup/                       # Mutex + 命名管道单实例协调器
├── tools/                         # 素材管线脚本(抠图 / 切分 / 重排)
└── Assets/Sprite/                 # 精灵素材(角色帧 + 精灵表)
```

## 🔧 技术要点

- **帧源可插拔**:统一 `ISpriteSource` 接口,静态图、精灵表、程序化绘制三种实现可无缝替换;
- **精灵表切片**:`CroppedBitmap` 按 `cols` 切格,支持任意帧数,零图像库依赖;
- **动画架构**:真帧动画(16 帧跑酷)+ 程序化正弦驱动(呼吸 / 挤压 / 摇摆)叠加,兼顾细腻与省资源;
- **高 DPI 适配**:鼠标位移与窗口坐标严格区分**物理像素 / DIP** 并换算,避免高 DPI 下拖拽"不跟手";
- **贴边对齐**:按角色接触边(头顶 / 脚底 / 身侧)补偿窗口内偏移,保证四条边都严丝合缝;
- **全屏防打扰**:Win32 `GetForegroundWindow` + 窗口矩形占屏比判定,并排除桌面 / 任务栏等系统外壳类名;
- **单实例**:`Mutex` + 命名管道,重复启动唤起已有实例而非新开窗口。

## 📄 许可

本项目采用**双许可**:

| 内容 | 许可 | 说明 |
|------|------|------|
| **源代码**(`dafeiyu/**` 除素材外) | [MIT](LICENSE) | 自由使用、修改、分发,保留版权声明即可 |
| **美术素材 / 角色形象 / 台词**(`dafeiyu/Assets/Sprite/*`) | [CC BY-NC 4.0](ASSETS-LICENSE.md) | 可自由分享、演绎,**禁止商业用途**,须署名 |

角色形象为社区二创 + AI 生成,**非 DeepSeek 官方作品**,「DeepSeek」商标归其权利人所有。

## 🙏 致谢

素材与梗的来源、参考的开源社区项目见 [NOTICE.md](NOTICE.md)。感谢「吃白饭的蓝色大肥鱼」社区创作者们 🐟🍚
