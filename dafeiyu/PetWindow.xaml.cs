using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using dafeiyu.Animation;
using dafeiyu.ViewModels;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace dafeiyu;

public partial class PetWindow : Window
{
    /// <summary>半藏被压的方向（顺序与 <c>_hiddenSources</c> 数组一致）。</summary>
    private enum HiddenEdge
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3,
    }

    private const int SnapThreshold = 20;
    private const int ClickMoveThreshold = 6;
    private const string StateFileName = "pet_state.json";

    /// <summary>“墙”接触深度（DIP）：贴边时角色轻贴屏边（约 10 DIP），主体完整可见。</summary>
    private double _semiHiddenContactDpi = 10d;

    /// <summary>跑酷速度（DIP/秒）：贴边绕圈前进速率。</summary>
    private const double RunSpeedDpiPerSec = 240d;

    /// <summary>角色主体在窗口中的几何（与 PetWindow.xaml 的角色 Grid 保持一致）。</summary>
    private const double PetSpriteWidth = 200d;
    private const double PetSpriteHeight = 250d;
    private const double PetSpriteBottomMargin = 12d;

    /// <summary>
    /// 半藏触发阈值（DIP）：角色一旦被边缘裁切（出界超过该值，很小）就进入对应方向的半藏动画——
    /// 半藏状态与动画一一对应（顶墙/趴地/回眸/侧身）；角色完全在屏内时不触发。
    /// 半藏深度按方向区分：左右“夹住一半”、上下“贴边压住 20%”，由钳制限制。
    /// </summary>
    private const double SemiHiddenTriggerDpi = 6d;

    private readonly PetViewModel _viewModel;
    private readonly ISpriteSource _idleSource;
    private readonly ISpriteSource _alertSource;
    private readonly ISpriteSource _tapSource;
    private readonly ISpriteSource _dragSource;

    /// <summary>贴边跑酷源（跑步精灵帧 pet_run_sheet.png 存在时逐帧播放，否则用拖动帧占位）。</summary>
    private readonly ISpriteSource _runSource;

    /// <summary>四个方向的半藏动画源（顺序同 <see cref="HiddenEdge"/>：上/下/左/右）。</summary>
    private readonly ISpriteSource[] _hiddenSources;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _tapTimer;

    private Forms.NotifyIcon? _trayIcon;
    private bool _isExiting;
    private bool _mouseDown;
    private bool _dragCandidate;
    private Drawing.Point _mouseDownCursor;
    private double _dragStartLeft;
    private double _dragStartTop;
    private bool _tapActive;
    private int _frameIndex;
    private ISpriteSource? _currentSource;   // 记录当前生效的帧源，用于检测状态切换

    // 贴边跑酷(游戏化):贴住屏幕边缘 → 沿边缘顺时针绕圈跑
    private enum RunDir
    {
        None,
        Up,
        Right,
        Down,
        Left,
    }

    private bool _running;
    private RunDir _runDir = RunDir.None;

    // 跑酷台词气泡:随机冒出,短暂显示(梗取自 dsh-dayu-fish-skin「大肥鱼模式」人设/社区梗)
    private static readonly string[] RunTalks =
    {
        "我不是吃白饭的!",
        "要努力才有饭吃!",
        "马上做!",
        "嗝～好饱…",
        "咕噜咕噜…",
        "摸鱼摸鱼～",
        "这活儿也太重了吧",
        "系统又卡了,不是我的锅",
        "我要靠自己!",
        "白米饭!白米饭!",
        "我要开家白米饭专卖店!",
        "金币闪闪,我最爱!",
        "吃白饭的蓝色大肥鱼,就是我!",
        "干完活才有白饭端上来!",
        "冲呀——为了白米饭!",
        "我嘴滑舌我骄傲～",
        "你猜我现在摸鱼吗?",
        "再跑一圈就吃饭!",
        "别看我看鱼,我赶路呢!",
        "呀,跑过头了…",
    };

    private readonly Random _random = new();
    private double _runTimeSec;
    private double _nextTalkAtSec = 3d;
    private DateTime _talkUntil = DateTime.MinValue;
    private double _frameAccum;   // 跑步时动画帧独立节拍(移动60fps,帧播放仍60ms一拍)

    public PetWindow()
    {
        InitializeComponent();
        _viewModel = new PetViewModel();
        DataContext = _viewModel;

        var whaleIdle = new DeepSeekWhaleSpriteSource(WhaleMotion.Idle);
        var whaleAlert = new DeepSeekWhaleSpriteSource(WhaleMotion.Alert);
        var whaleDrag = new DeepSeekWhaleSpriteSource(WhaleMotion.Drag);
        var whaleEdge = new DeepSeekWhaleSpriteSource(WhaleMotion.Edge);

        _idleSource = new ImageSpriteSource(
            "Assets/Sprite/pet_idle.png",
            altPath: "Assets/Sprite/pet_idle_blink.png",
            altFrameIndex: 3,
            fallback: whaleIdle);
        _alertSource = new ImageSpriteSource(
            "Assets/Sprite/pet_alert.png",
            fallback: whaleAlert);
        _tapSource = new ImageSpriteSource(
            "Assets/Sprite/pet_tap.png",
            fallback: whaleIdle);
        // 拖动 / 半藏动画：优先用真实 PNG（pet_drag.png / pet_hidden_*.png，可选），缺失时回退程序化占位。
        // 1) 若存在 “*_sheet.png” 精灵图集（Godot 式行条幅，任意帧数）→ 优先逐帧播放；
        // 2) 否则用基图 + 帧组（pet_*_0/1.png，可选）+ 程序化正弦微动。
        static IReadOnlyList<string> Frames(string name) =>
            new[] { $"Assets/Sprite/{name}_0.png", $"Assets/Sprite/{name}_1.png" };

        static ISpriteSource WithSheet(string name, ISpriteSource fallback, int columns = 0)
        {
            var sheetPath = $"Assets/Sprite/{name}_sheet.png";
            try
            {
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, sheetPath)))
                {
                    return new SpriteSheetSource(sheetPath, columns);
                }
            }
            catch
            {
            }

            return fallback;
        }

        _dragSource = WithSheet("pet_drag", new ImageSpriteSource(
            "Assets/Sprite/pet_drag.png",
            fallback: whaleDrag,
            framePaths: Frames("pet_drag")), columns: 8);
        // 半藏动画按“被哪个边卡住”分 4 套：pet_hidden_top / _bottom / _left / _right；
        // 缺失时回退到通用程序化占位。真图就位后自动按方向切换。
        _hiddenSources = new[]
        {
            WithSheet("pet_hidden_top", new ImageSpriteSource("Assets/Sprite/pet_hidden_top.png", fallback: whaleEdge, framePaths: Frames("pet_hidden_top")), columns: 8),
            WithSheet("pet_hidden_bottom", new ImageSpriteSource("Assets/Sprite/pet_hidden_bottom.png", fallback: whaleEdge, framePaths: Frames("pet_hidden_bottom")), columns: 6),
            WithSheet("pet_hidden_left", new ImageSpriteSource("Assets/Sprite/pet_hidden_left.png", fallback: whaleEdge, framePaths: Frames("pet_hidden_left")), columns: 6),
            WithSheet("pet_hidden_right", new ImageSpriteSource("Assets/Sprite/pet_hidden_right.png", fallback: whaleEdge, framePaths: Frames("pet_hidden_right")), columns: 6),
        };
        // 跑酷源：pet_run_sheet.png（跑步精灵帧，可选）存在时逐帧播放，否则用拖动帧占位。
        _runSource = WithSheet("pet_run", _dragSource, columns: 16);

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(110),
        };
        _animationTimer.Tick += OnAnimationTick;

        _tapTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200),
        };
        _tapTimer.Tick += (_, _) =>
        {
            _tapActive = false;
            _tapTimer.Stop();
        };

        RestorePosition();
        InitializeTray();
        _animationTimer.Start();
        SpriteImage.Source = _idleSource.GetFrame(0);

        // 窗口显示后强制钳制在屏幕内，避免坐标漂移导致“隐身”。
        Loaded += (_, _) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (Left < 0 || Top < 0 ||
                    Left + ActualWidth > Forms.Screen.PrimaryScreen?.WorkingArea.Right ||
                    Top + ActualHeight > Forms.Screen.PrimaryScreen?.WorkingArea.Bottom)
                {
                    SetInitialPosition();
                }

                ClampCurrentToWorkArea();
            }));
        };

        StartFullscreenGuard();   // 全屏防打扰:检测到全屏应用时自动隐藏
    }

    // ---------- 动画 ----------

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            return;
        }

        // 状态优先级：跑酷(贴边绕圈) > 拖动 > 半藏 > 点按 > 提醒 > 待机。
        // 角色一旦贴住屏幕边缘且未拖动 → 进入绕圈跑；拖动或离开边缘即退出。
        var dragging = _mouseDown && !_dragCandidate;
        var hiddenEdge = GetHiddenEdge();

        if (!_mouseDown && hiddenEdge is { })
        {
            if (!_running)
            {
                _running = true;
                _runDir = hiddenEdge.Value switch
                {
                    HiddenEdge.Left => RunDir.Up,      // 顺时针：左→上→右→下
                    HiddenEdge.Top => RunDir.Right,
                    HiddenEdge.Right => RunDir.Down,
                    HiddenEdge.Bottom => RunDir.Left,
                    _ => RunDir.Up,
                };
                _animationTimer.Interval = TimeSpan.FromMilliseconds(16);   // 移动平滑化:60fps,每步≈3.8DIP
                Toolbar.Visibility = Visibility.Collapsed;   // 跑酷:工具条隐藏,画面干净
                _runTimeSec = 0d;
                _frameAccum = 0d;
                _nextTalkAtSec = 2d + _random.NextDouble() * 3d;   // 2~5 秒后第一次台词
            }
        }
        else if (_running && _mouseDown)
        {
            _running = false;
            _animationTimer.Interval = TimeSpan.FromMilliseconds(110);   // 恢复默认帧率
            Toolbar.Visibility = Visibility.Visible;   // 退出跑酷:恢复工具条
            TalkBubble.Visibility = Visibility.Collapsed;
        }

        if (_running)
        {
            StepRun();

            // 跑酷台词:随机冒出,显示约 2 秒后自动消失;气泡跟随角色(移动已60fps平滑)
            _runTimeSec += _animationTimer.Interval.TotalSeconds;
            var upsideDown = _runDir == RunDir.Right;
            TalkBubble.VerticalAlignment = upsideDown ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            TalkTail.VerticalAlignment = upsideDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            TalkBorder.Margin = upsideDown ? new Thickness(0, 10, 0, 0) : new Thickness(0, 0, 0, 10);
            if (_runTimeSec >= _nextTalkAtSec)
            {
                TalkText.Text = RunTalks[_random.Next(RunTalks.Length)];
                TalkBubble.Visibility = Visibility.Visible;
                _talkUntil = DateTime.Now.AddSeconds(2.2);
                _nextTalkAtSec = _runTimeSec + 2d + _random.NextDouble() * 4d;
            }

            if (DateTime.Now > _talkUntil)
            {
                TalkBubble.Visibility = Visibility.Collapsed;
            }
        }

        ISpriteSource source = _running
            ? _runSource
            : dragging
                ? _dragSource
                : hiddenEdge is { } he
                    ? _hiddenSources[(int)he]
                    : _tapActive
                        ? _tapSource
                        : _viewModel.IsAlert
                            ? _alertSource
                            : _idleSource;

        // 半藏时按方向调整图片贴墙对齐：Image 在 200x250 容器内默认垂直居中（上下各留 25px 空白），
        // 不处理会导致上/半藏时角色“悬空”；顶=顶部对齐、底=底部对齐，让接触线真正压到墙边。
        SpriteImage.VerticalAlignment = hiddenEdge switch
        {
            HiddenEdge.Top => VerticalAlignment.Top,
            HiddenEdge.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };

        // 状态切换（待机/提醒/点按）时重置帧索引，让新动画从第 0 帧连贯播放，
        // 避免沿用上一状态的计数器导致“从中间帧硬切、画面跳变”。
        if (!ReferenceEquals(source, _currentSource))
        {
            _currentSource = source;
            _frameIndex = -1;   // +1 后即为 0，本 tick 从新状态第 0 帧开始
        }

        // 帧推进:跑步时按 60ms 一拍(移动已是 60fps);其它状态每 tick 推进。
        if (_running)
        {
            _frameAccum += _animationTimer.Interval.TotalSeconds;
            if (_frameAccum >= 0.06)
            {
                _frameAccum -= 0.06;
                _frameIndex = (_frameIndex + 1) % source.FrameCount;
            }
        }
        else
        {
            _frameIndex = (_frameIndex + 1) % source.FrameCount;
        }

        SpriteImage.Source = source.GetFrame(_frameIndex);
        ApplyMicroMotion(source, hiddenEdge, dragging, _running);
    }

    /// <summary>跑酷推进：沿工作区边缘顺时针前进，到角自动转弯。</summary>
    private void StepRun()
    {
        try
        {
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : Height;
            var (scaleX, scaleY) = GetDpiScale();
            var refPt = new Drawing.Point(
                (int)((Left + width / 2) * scaleX),
                (int)((Top + height / 2) * scaleY));
            var screen = Forms.Screen.FromPoint(refPt);
            if (screen is null)
            {
                return;
            }

            var wa = screen.WorkingArea;
            var left = wa.Left / scaleX;
            var top = wa.Top / scaleY;
            var right = wa.Right / scaleX;
            var bottom = wa.Bottom / scaleY;
            var step = RunSpeedDpiPerSec * _animationTimer.Interval.TotalSeconds;

            // 以“角色”贴边为准(角色图在窗口内:底部留 12 DIP、水平居中、垂直居中留 25 DIP):
            var petLeftOff = (width - PetSpriteWidth) / 2;                            // 20
            var petImagePad = (PetSpriteHeight - PetSpriteWidth) / 2d;                // 25(200 图在 250 容器内垂直居中)
            var petTopOff = height - PetSpriteBottomMargin - PetSpriteHeight + petImagePad;   // 183

            switch (_runDir)
            {
                case RunDir.Up:
                    Left = left - petLeftOff;   // 沿左边爬:角色左贴左边
                    Top -= step;
                    if (Top + height - petTopOff <= top)
                    {
                        Top = top - petTopOff;
                        _runDir = RunDir.Right;
                    }

                    break;
                case RunDir.Right:
                    Top = top - petTopOff;   // 沿顶边跑:角色头贴屏顶(窗口上移 183)
                    Left += step;
                    if (Left + width - petLeftOff >= right)
                    {
                        Left = right - (petLeftOff + PetSpriteWidth);
                        _runDir = RunDir.Down;
                    }

                    break;
                case RunDir.Down:
                    Left = right - (petLeftOff + PetSpriteWidth);   // 沿右边爬:角色右贴右边
                    Top += step;
                    if (Top + height - (PetSpriteBottomMargin + petImagePad) >= bottom)
                    {
                        Top = bottom + PetSpriteBottomMargin + petImagePad - height;
                        _runDir = RunDir.Left;
                    }

                    break;
                case RunDir.Left:
                    Top = bottom + PetSpriteBottomMargin + petImagePad - height;   // 沿底边跑:角色脚贴屏底
                    Left -= step;
                    if (Left <= left - petLeftOff)
                    {
                        Left = left - petLeftOff;
                        _runDir = RunDir.Up;
                    }

                    break;
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// 程序化动画（参照 dafeiyu-pet 的做法：单张精灵 + 正弦函数驱动的 transform）：
    /// 待机=呼吸+微摆；拖动=摇摆+浮动；半藏=呼吸式挤压+起伏。零多帧素材。
    /// </summary>
    private void ApplyMicroMotion(ISpriteSource source, HiddenEdge? hiddenEdge, bool dragging, bool running)
    {
        // 相位与当前帧源的周期对齐（都统一为 6 帧序），保证运动连续。
        double t = _frameIndex / (double)Math.Max(1, source.FrameCount);
        double now = t;   // 周期化的时间轴
        var group = new TransformGroup();

        if (running)
        {
            // 跑酷：快速起伏 + 轻微摆动（跑步感），并按行进方向做朝向：
            // 水平 = 侧面帧(向左镜像)；竖直 = 贴墙爬(旋转 90°)。中心=显示区域中心。
            double bob = 2.5d * Math.Sin(now * Math.PI * 4);
            double tilt = 2.0d * Math.Sin(now * Math.PI * 4);
            if (_runDir == RunDir.Right)
            {
                // 顶边 = 天花板:倒挂,头朝下(素材面朝左,旋转180°后脸朝右)
                group.Children.Add(new RotateTransform(180d, 100d, 125d));
            }
            else if (_runDir == RunDir.Up)
            {
                group.Children.Add(new RotateTransform(90d, 100d, 125d));   // 左边爬墙:头朝上
            }
            else if (_runDir == RunDir.Down)
            {
                group.Children.Add(new RotateTransform(-90d, 100d, 125d));  // 右边爬墙:头朝下
            }
            // Left(底边向左跑)= 原样(面朝左,头朝上,踩"地面")

            group.Children.Add(new RotateTransform(tilt));
            group.Children.Add(new TranslateTransform(0, bob));
        }
        else if (hiddenEdge is { })
        {
            // 被墙压住：呼吸式“挤压-放松”（以贴墙呼吸的节奏）+ 轻微起伏
            double squeeze = 0.045 * (0.5 + 0.5 * Math.Sin(now * Math.PI * 2));
            double bob = 1.2d * Math.Sin(now * Math.PI * 2 + Math.PI / 2);
            group.Children.Add(new ScaleTransform(1d - squeeze / 2d, 1d - squeeze));
            group.Children.Add(new TranslateTransform(0, bob));
        }
        else if (dragging)
        {
            // 被拖着走：摇摆 + 浮动（幅度比待机大）
            double sway = 2.5d * Math.Sin(now * Math.PI * 2);
            double bob = 3d * Math.Sin(now * Math.PI * 2 - Math.PI / 2);
            group.Children.Add(new RotateTransform(sway));
            group.Children.Add(new ScaleTransform(1d + 0.02 * Math.Sin(now * Math.PI * 2), 1d));
            group.Children.Add(new TranslateTransform(0, bob));
        }
        else
        {
            // 待机/提醒/点按：呼吸缩放 + 微摆 + 轻起伏（让任何状态都“活着”）
            double breath = 1.0 + 0.02 * Math.Sin(now * Math.PI * 2);
            double sway = 1.2d * Math.Sin(now * Math.PI * 2);
            double bob = 0.8d * Math.Sin(now * Math.PI * 2 + Math.PI / 2);
            group.Children.Add(new ScaleTransform(breath, breath));
            group.Children.Add(new RotateTransform(sway));
            group.Children.Add(new TranslateTransform(0, bob));
        }

        SpriteImage.RenderTransform = group;
    }

    private void TriggerTap()
    {
        _tapActive = true;
        _tapTimer.Stop();
        _tapTimer.Start();
    }

    // ---------- 拖动 / 点击 ----------

    private void OnSpriteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        _mouseDown = true;
        _dragCandidate = true;
        _running = false;   // 按下即脱出跑酷(进入拖动/点击)
        _mouseDownCursor = Forms.Cursor.Position;
        _dragStartLeft = Left;
        _dragStartTop = Top;
        SpriteImage.CaptureMouse(); // 捕获鼠标：即使移出也仍收到 Up，避免“粘滞拖拽”
        e.Handled = true;
    }

    private void OnSpriteMouseMove(object sender, MouseEventArgs e)
    {
        // 左键没按着却还以为是拖拽 → 复位，防止“鼠标靠近她她就跑”。
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _mouseDown = false;
            _dragCandidate = false;
            return;
        }

        if (!_mouseDown)
        {
            return;
        }

        var cursor = Forms.Cursor.Position;

        // 鼠标位移是物理像素，而窗口 Left/Top 是 DIP；不换算会导致高 DPI 下
        // 角色移动速度 ≠ 鼠标速度，表现为“不跟手”。
        var (scaleX, scaleY) = GetDpiScale();
        var dx = (cursor.X - _mouseDownCursor.X) / scaleX;
        var dy = (cursor.Y - _mouseDownCursor.Y) / scaleY;

        if (_dragCandidate && (Math.Abs(dx) > ClickMoveThreshold || Math.Abs(dy) > ClickMoveThreshold))
        {
            _dragCandidate = false;
        }

        if (!_dragCandidate)
        {
            // 先计算目标位置并钳制，再一次性写回最终位置；
            // 避免“先写越界值再被夹回”导致边缘处窗口每帧抖动闪烁。
            var (targetLeft, targetTop) = ClampToWorkArea(_dragStartLeft + dx, _dragStartTop + dy, cursor);
            if (Math.Abs(targetLeft - Left) > 0.1)
            {
                Left = targetLeft;
            }

            if (Math.Abs(targetTop - Top) > 0.1)
            {
                Top = targetTop;
            }
        }

        e.Handled = true;
    }

    private void OnSpriteMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_mouseDown)
        {
            SpriteImage.ReleaseMouseCapture();
            return;
        }

        _mouseDown = false;
        SpriteImage.ReleaseMouseCapture();
        if (_dragCandidate)
        {
            // 未移动 → 视为点按，展示 tap 动图
            TriggerTap();
        }
        else
        {
            SnapToEdge();
            var (newLeft, newTop) = ClampToWorkArea(Left, Top, Forms.Cursor.Position);   // 与拖动一致钳制到半藏位，松手不跳变
            if (Math.Abs(newLeft - Left) > 0.5)
            {
                Left = newLeft;
            }

            if (Math.Abs(newTop - Top) > 0.5)
            {
                Top = newTop;
            }

            SavePosition();
        }

        e.Handled = true;
    }

    private void SnapToEdge()
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor) ?? Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        var wa = screen.WorkingArea;
        var (sx, sy) = GetDpiScale();

        // 屏幕工作区是物理像素，而窗口 Left/Top 是 DIP；先换算成 DIP 再比较/赋值。
        var left = wa.Left / sx;
        var top = wa.Top / sy;
        var right = wa.Right / sx;
        var bottom = wa.Bottom / sy;
        var thresholdX = SnapThreshold / sx;
        var thresholdY = SnapThreshold / sy;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;

        // 仅“轻微贴边”（距边 ±20 DIP 内，含轻微出界）时轻轻吸附到全可见贴边；
        // 明显深藏时保持原位——半藏由拖动深度自然形成，不弹跳、不被边缘“吸走”。
        if (Left >= left - thresholdX && Left <= left + thresholdX)
        {
            Left = left;
        }
        else if (Left + width >= right - thresholdX && Left + width <= right + thresholdX)
        {
            Left = right - width;
        }

        if (Top >= top - thresholdY && Top <= top + thresholdY)
        {
            Top = top;
        }
        else if (Top + height >= bottom - thresholdY && Top + height <= bottom + thresholdY)
        {
            Top = bottom - height;
        }
    }

    /// <summary>
    /// 计算四个方向的“半藏贴边位”（DIP）。以角色可见比例为基准：
    /// 半藏时角色被压的深度按方向区分：上/下只压 20%（贴边即可，主体完整），左/右压一半（被夹住）。
    /// </summary>
    private (double HalfLeft, double HalfRight, double HalfTop, double HalfBottom)
        ComputeSemiHiddenEdges(double waLeft, double waTop, double waRight, double waBottom, double physicalBottom)
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        // 把边框当“墙”：角色只是抵着墙（接触深度约 10 DIP），主体完整可见；
        // 素材已把被压侧（头顶/脚底/身体侧）平移到图边，保证视觉贴墙。
        var hideDpi = _semiHiddenContactDpi;
        var hideW = hideDpi;
        var hideTop = hideDpi;
        var hideBottom = hideDpi;
        var petLeft = (width - PetSpriteWidth) / 2;
        var petTop = height - PetSpriteBottomMargin - PetSpriteHeight;

        var halfLeft = waLeft - (petLeft + hideW);
        var halfRight = waRight + (petLeft + hideW) - width;
        var halfTop = waTop - (petTop + hideTop);
        // 底部用“物理屏幕底边”（含任务栏）作为墙：角色真正贴到显示器下边缘，
        // 而不是停在任务栏上方的工作区边。
        var halfBottom = physicalBottom + (PetSpriteBottomMargin + hideBottom) - height;
        return (halfLeft, halfRight, halfTop, halfBottom);
    }

    // ---------- Mock 触发 ----------

    private void OnTriggerTaskReminder(object sender, RoutedEventArgs e)
        => _viewModel.RaiseMockReminder(new MockReminder(
            "整理桌面资料",
            "截稿前把桌面资料归档、命名规范统一。",
            "任务 · 即将开始",
            "普通",
            false));

    private void OnTriggerAnimeReminder(object sender, RoutedEventArgs e)
        => _viewModel.RaiseMockReminder(new MockReminder(
            "葬送的芙莉莲 第 01 集",
            "今晚 23:00 首播，别错过。",
            "动漫 · 播出",
            "高",
            true));

    // ---------- 气泡动作 ----------

    private void OnCompleteClick(object sender, RoutedEventArgs e) => _viewModel.ClearReminder();

    private void OnSnoozeClick(object sender, RoutedEventArgs e) => _viewModel.ClearReminder();

    private void OnIgnoreClick(object sender, RoutedEventArgs e) => _viewModel.ClearReminder();

    // ---------- 托盘与收纳 ----------

    /// <summary>全屏防打扰:前台有全屏应用(游戏/播放器)时自动隐藏,退出全屏自动恢复。</summary>
    private bool _hiddenByFullScreen;
    private DispatcherTimer? _fullscreenTimer;

    private void StartFullscreenGuard()
    {
        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _fullscreenTimer.Tick += (_, _) =>
        {
            try
            {
                if (IsForegroundFullScreen())
                {
                    if (!_hiddenByFullScreen)
                    {
                        _hiddenByFullScreen = true;
                        if (IsVisible)
                        {
                            Hide();
                        }
                    }
                }
                else if (_hiddenByFullScreen)
                {
                    _hiddenByFullScreen = false;
                    // 仅当用户未主动收纳时才恢复显示
                    if (!_viewModel.IsCollapsed)
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                }
            }
            catch
            {
            }
        };
        _fullscreenTimer.Start();
    }

    /// <summary>前台窗口是否为全屏应用(占满所在屏幕 ≥98%)。</summary>
    private bool IsForegroundFullScreen()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == new WindowInteropHelper(this).Handle)
        {
            return false;
        }

        if (!Native.IsWindowVisible(hwnd))
        {
            return false;
        }

        // 排除桌面/任务栏/开始菜单等系统外壳窗口
        var cls = new System.Text.StringBuilder(256);
        Native.GetClassName(hwnd, cls, 256);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "XamlExplorerHostIslandWindow")
        {
            return false;
        }

        Native.GetWindowRect(hwnd, out var rect);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var bounds = Forms.Screen.FromHandle(hwnd).Bounds;
        return width >= bounds.Width * 0.98 && height >= bounds.Height * 0.98;
    }

    private static class Native
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    private void OnCollapseClick(object sender, RoutedEventArgs e) => CollapseToTray();

    private void InitializeTray()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Information,
            Text = "大肥鱼桌宠",
            Visible = true,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示桌宠", null, (_, _) => ShowFromTray());
        menu.Items.Add("任务提醒", null, (_, _) => Trigger(TaskReminder));
        menu.Items.Add("动漫提醒", null, (_, _) => Trigger(AnimeReminder));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void CollapseToTray()
    {
        _viewModel.CollapseToTray();
        SavePosition();
        Hide();
    }

    public void ShowFromTray()
    {
        _viewModel.RestoreFromTray();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        Application.Current.Shutdown();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        CollapseToTray();
    }

    // ---------- 位置持久化 ----------

    private record PetPlacement(double Left, double Top, bool Collapsed);

    private static string StateFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "dafeiyu",
        StateFileName);

    private void RestorePosition()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                SetInitialPosition();
                return;
            }

            var placement = JsonSerializer.Deserialize<PetPlacement>(File.ReadAllText(StateFilePath));
            if (placement is null)
            {
                SetInitialPosition();
                return;
            }

            Left = placement.Left;
            Top = placement.Top;
            ClampCurrentToWorkArea();
            if (placement.Collapsed)
            {
                CollapseToTray();
            }
        }
        catch
        {
            SetInitialPosition();
        }
    }

    /// <summary>
    /// 计算目标位置的钳制结果（纯计算，不写回 Left/Top）。
    /// 注意：WPF 的 Left/Top 是 DIP（设备无关像素），而 Forms.Screen.WorkingArea 是物理像素；
    /// 若直接混用，在高 DPI 缩放下钳制范围会被错误放大，窗口仍会被拖出屏幕，因此先按 DPI 换算再钳制。
    /// </summary>
    private (double Left, double Top) ClampToWorkArea(double targetLeft, double targetTop, Drawing.Point? mouseReference = null)
    {
        try
        {
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : Height;
            if (width <= 0 || height <= 0)
            {
                return (targetLeft, targetTop);
            }

            var (scaleX, scaleY) = GetDpiScale();

            // 用鼠标的物理坐标确定所在屏幕；未提供时用目标中心（由 DIP 换算回物理）。
            Drawing.Point refPt;
            if (mouseReference is { } mr)
            {
                refPt = mr;
            }
            else
            {
                refPt = new Drawing.Point(
                    (int)((targetLeft + width / 2) * scaleX),
                    (int)((targetTop + height / 2) * scaleY));
            }

            var target = Forms.Screen.FromPoint(refPt) ?? Forms.Screen.PrimaryScreen;
            if (target is null)
            {
                return (targetLeft, targetTop);
            }

            var wa = target.WorkingArea;
            var waLeft = wa.Left / scaleX;
            var waTop = wa.Top / scaleY;
            var waRight = wa.Right / scaleX;
            var waBottom = wa.Bottom / scaleY;

            // 统一钳制到“半藏位”：角色最多藏一半（可见 ≥ 50%），拖动、松手、恢复行为一致；
            // 避免拖得过深导致角色只露一角（如只露尾巴尖）。
            var physicalBottom = target.Bounds.Bottom / scaleY;   // 物理屏底（含任务栏）
            var (halfLeft, halfRight, halfTop, halfBottom) =
                ComputeSemiHiddenEdges(waLeft, waTop, waRight, waBottom, physicalBottom);

            var newLeft = Math.Max(halfLeft, Math.Min(targetLeft, halfRight));
            var newTop = Math.Max(halfTop, Math.Min(targetTop, halfBottom));
            return (newLeft, newTop);
        }
        catch
        {
            return (targetLeft, targetTop);
        }
    }

    /// <summary>基于当前 Left/Top 钳制到工作区（仅实际越界时写回，避免重复赋值导致闪烁）。</summary>
    private void ClampCurrentToWorkArea()
    {
        var (newLeft, newTop) = ClampToWorkArea(Left, Top);
        if (Math.Abs(newLeft - Left) > 0.5)
        {
            Left = newLeft;
        }

        if (Math.Abs(newTop - Top) > 0.5)
        {
            Top = newTop;
        }
    }

    /// <summary>获取窗口的 DPI 缩放系数（DIP → 物理像素的比例）。</summary>
    private (double X, double Y) GetDpiScale()
    {
        try
        {
            var source = PresentationSource.FromVisual(this);
            var m = source?.CompositionTarget?.TransformToDevice;
            if (m is { } matrix && matrix.M11 > 0 && matrix.M22 > 0)
            {
                return (matrix.M11, matrix.M22);
            }
        }
        catch
        {
        }
        return (1.0, 1.0);
    }

    /// <summary>
    /// 判断角色被哪个屏幕边缘“卡住”（半藏）。以角色的实际位置判定，而非窗口——
    /// 窗口顶部有大片透明区/工具条，若按窗口判定，上边缘时角色明明完整可见也会被判半藏。
    /// 返回被压的最深方向；未达到半藏阈值时返回 null。
    /// </summary>
    private HiddenEdge? GetHiddenEdge()
    {
        try
        {
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : Height;
            var (scaleX, scaleY) = GetDpiScale();
            var refPt = new Drawing.Point(
                (int)((Left + width / 2) * scaleX),
                (int)((Top + height / 2) * scaleY));
            var wa = Forms.Screen.FromPoint(refPt)?.WorkingArea;
            if (wa is null)
            {
                return null;
            }

            var waLeft = wa.Value.Left / scaleX;
            var waTop = wa.Value.Top / scaleY;
            var waRight = wa.Value.Right / scaleX;
            var waBottom = wa.Value.Bottom / scaleY;

            // 角色主体在窗口中的实际屏幕位置（角色 Grid 底部留 12 DIP、水平居中）。
            var petLeft = Left + (width - PetSpriteWidth) / 2;
            var petTop = Top + height - PetSpriteBottomMargin - PetSpriteHeight;

            // 各方向“出界被压”的深度。
            var outTop = waTop - petTop;
            var outBottom = petTop + PetSpriteHeight - waBottom;
            var outLeft = waLeft - petLeft;
            var outRight = petLeft + PetSpriteWidth - waRight;

            var maxOut = Math.Max(Math.Max(outTop, outBottom), Math.Max(outLeft, outRight));
            if (maxOut <= SemiHiddenTriggerDpi)
            {
                return null;
            }

            if (maxOut == outTop)
            {
                return HiddenEdge.Top;
            }

            if (maxOut == outBottom)
            {
                return HiddenEdge.Bottom;
            }

            if (maxOut == outLeft)
            {
                return HiddenEdge.Left;
            }

            return HiddenEdge.Right;
        }
        catch
        {
            return null;
        }
    }

    private void SetInitialPosition()
    {
        try
        {
            var screens = Forms.Screen.AllScreens;
            var primary = Forms.Screen.PrimaryScreen ?? screens.FirstOrDefault();
            var workArea = primary?.WorkingArea ?? new Drawing.Rectangle(0, 0, 1920, 1080);

            Width = ActualWidth > 0 ? ActualWidth : 248;
            Height = ActualHeight > 0 ? ActualHeight : 420;

            // 居中摆放：屏幕工作区是物理像素，需先换算成 DIP 再计算，避免高 DPI 下偏位。
            var (scaleX, scaleY) = GetDpiScale();
            var waLeft = workArea.Left / scaleX;
            var waTop = workArea.Top / scaleY;
            var waWidth = workArea.Width / scaleX;
            var waHeight = workArea.Height / scaleY;
            Left = waLeft + (waWidth - Width) / 2;
            Top = waTop + (waHeight - Height) / 2;
            ClampCurrentToWorkArea();

            try
            {
                var log = Path.Combine(AppContext.BaseDirectory, "pet_position.log");
                var line = $"[{DateTime.Now:HH:mm:ss}] screens={string.Join(";", screens.Select(s => $"{s.DeviceName}@{s.WorkingArea}"))} primary={primary?.DeviceName} placed=({Left},{Top}) size={Width}x{Height}{Environment.NewLine}";
                File.AppendAllText(log, line);
            }
            catch
            {
            }
        }
        catch
        {
        }
    }

    private void SavePosition()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);
            var placement = new PetPlacement(Left, Top, _viewModel.IsCollapsed);
            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(placement));
        }
        catch
        {
        }
    }

    // ---------- 托盘触发 ----------

    private static MockReminder TaskReminder => new(
        "整理桌面资料",
        "截稿前把桌面资料归档、命名规范统一。",
        "任务 · 即将开始",
        "普通",
        false);

    private static MockReminder AnimeReminder => new(
        "葬送的芙莉莲 第 01 集",
        "今晚 23:00 首播，别错过。",
        "动漫 · 播出",
        "高",
        true);

    private void Trigger(MockReminder reminder)
        => Dispatcher.BeginInvoke(new Action(() => _viewModel.RaiseMockReminder(reminder)));
}
