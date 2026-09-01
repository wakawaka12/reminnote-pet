using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;

namespace ReminNote.Pet.Animation;

/// <summary>鲸鱼娘动效类型。</summary>
public enum WhaleMotion
{
    Idle,
    Alert,
    Drag,
    Edge,
}

/// <summary>
/// DeepSeek「蓝色大肥鱼 / 鲸鱼娘」风格的精灵源（程序化占位）。
/// 依据 DeepSeek 蓝鲸 logo 与社区「蓝色大肥鱼」形象（蓝背白腹、圆润、萌眼、腮红、
/// 爱吃白米饭、傲娇）程序化绘制。此为近似占位；未来可换成官方 PNG 精灵图（ISpriteSource 驱动不变）。
/// 各动效：
/// - Idle：摆尾/眨眼/呼吸（待机）；
/// - Alert：惊讶张大嘴 + 冒泡泡 + 更明显弹跳（提醒）；
/// - Drag：瞪圆眼 + 微张的嘴 + 大摆尾（被拖着走，惊慌挣扎）；
/// - Edge：身体被压扁 + 半闭眼委屈 + 尾巴小幅摆动（半藏/被屏幕边缘卡住）。
/// </summary>
public sealed class DeepSeekWhaleSpriteSource : ISpriteSource
{
    private const double Canvas = 120d;
    private const int FramesPerGroup = 6;

    private readonly WhaleMotion _motion;

    // DeepSeek 蓝鲸配色
    private static readonly Color BackBlue = Color.FromRgb(0x2B, 0x3A, 0x6E);   // 深蓝背
    private static readonly Color MidBlue = Color.FromRgb(0x58, 0x78, 0xC6);   // 中间过渡蓝
    private static readonly Color BodyWhite = Color.FromRgb(0xF6, 0xF9, 0xFF); // 腹/脸白
    private static readonly Color EyeDark = Color.FromRgb(0x1E, 0x27, 0x44);
    private static readonly Color MouthDark = Color.FromRgb(0x3A, 0x41, 0x59);
    private static readonly Color Blush = Color.FromRgb(0xF7, 0xB6, 0xC8);
    private static readonly Color Bubble = Color.FromArgb(0xB0, 0xA9, 0xC3, 0xEF);

    public DeepSeekWhaleSpriteSource(WhaleMotion motion)
    {
        _motion = motion;
    }

    public int FrameCount => FramesPerGroup;

    public ImageSource GetFrame(int index)
    {
        index = ((index % FramesPerGroup) + FramesPerGroup) % FramesPerGroup;
        double t = index / (double)FramesPerGroup;
        double swing = Math.Sin(t * Math.PI * 2);

        // 身体浮动幅度：拖动/提醒更明显；半藏被卡住几乎不动。
        double depth = _motion switch
        {
            WhaleMotion.Alert or WhaleMotion.Drag => 5d,
            WhaleMotion.Edge => 1d,
            _ => 2d,
        };
        double bodyY = swing * depth;

        // 尾巴摆动角度：拖动挣扎最大，半藏被挤住最小。
        double tailAngle = swing * (_motion switch
        {
            WhaleMotion.Alert => 14d,
            WhaleMotion.Drag => 18d,
            WhaleMotion.Edge => 4d,
            _ => 9d,
        });

        // 半藏被压扁：身体/白腹纵向半径缩小。
        bool edge = _motion == WhaleMotion.Edge;
        double bodyRy = edge ? 22d : 32d;
        double bellyRy = edge ? 14d : 20d;

        var root = new DrawingGroup();
        var backBrush = new SolidColorBrush(BackBlue); backBrush.Freeze();
        var whiteBrush = new SolidColorBrush(BodyWhite); whiteBrush.Freeze();
        var eyeBrush = new SolidColorBrush(EyeDark); eyeBrush.Freeze();
        var mouthBrush = new SolidColorBrush(MouthDark); mouthBrush.Freeze();
        var blushBrush = new SolidColorBrush(Blush); blushBrush.Freeze();
        var bubbleBrush = new SolidColorBrush(Bubble); bubbleBrush.Freeze();
        var midBrush = new SolidColorBrush(MidBlue); midBrush.Freeze();

        // ---- 身体（蓝背）----
        root.Children.Add(new GeometryDrawing(
            backBrush, null,
            new EllipseGeometry(new Point(58, 60 + bodyY), 44, bodyRy)));

        // ---- 白腹/白脸 ----
        root.Children.Add(new GeometryDrawing(
            whiteBrush, null,
            new EllipseGeometry(new Point(66, 72 + bodyY), 32, bellyRy)));

        // ---- 胸鳍（两片，深蓝）----
        root.Children.Add(new GeometryDrawing(
            backBrush, null,
            new EllipseGeometry(new Point(40, 82 + bodyY), 10, 5)));
        root.Children.Add(new GeometryDrawing(
            backBrush, null,
            new EllipseGeometry(new Point(68, 86 + bodyY), 10, 5)));

        // ---- 背鳍（上三角，深蓝）----
        root.Children.Add(new GeometryDrawing(
            backBrush, null,
            TailTriangle(new Point(54, 28 + bodyY), new Point(62, 12 + bodyY), new Point(70, 29 + bodyY))));

        // ---- 尾鳍（左侧，深蓝，带摆动）----
        var tailGeometry = TailGeometry(bodyY);
        tailGeometry.Transform = new RotateTransform(tailAngle, 20, 60 + bodyY);
        root.Children.Add(new GeometryDrawing(backBrush, null, tailGeometry));

        // ---- 眼睛：待机眨眼 / 提醒瞪大 / 拖动瞪圆 / 半藏半闭委屈 ----
        double eyeHalfHeight = _motion switch
        {
            WhaleMotion.Alert => 6d,
            WhaleMotion.Drag => 6.5d,
            WhaleMotion.Edge => 1.6d,
            _ => index == 3 ? 1.6d : 5d,
        };

        var eyeL = new EllipseGeometry(new Point(72, 58 + bodyY), 6, eyeHalfHeight);
        var eyeR = new EllipseGeometry(new Point(87, 58 + bodyY), 6, eyeHalfHeight);
        root.Children.Add(new GeometryDrawing(eyeBrush, null, eyeL));
        root.Children.Add(new GeometryDrawing(eyeBrush, null, eyeR));

        // 高光
        var hlBrush = whiteBrush;
        if (eyeHalfHeight > 2.5d)
        {
            root.Children.Add(new GeometryDrawing(
                hlBrush, null,
                new EllipseGeometry(new Point(74, 56 + bodyY), 1.8, 1.8)));
            root.Children.Add(new GeometryDrawing(
                hlBrush, null,
                new EllipseGeometry(new Point(89, 56 + bodyY), 1.8, 1.8)));
        }

        // ---- 腮红 ----
        root.Children.Add(new GeometryDrawing(
            blushBrush, null,
            new EllipseGeometry(new Point(66, 70 + bodyY), 4, 2.4)));
        root.Children.Add(new GeometryDrawing(
            blushBrush, null,
            new EllipseGeometry(new Point(96, 70 + bodyY), 4, 2.4)));

        // ---- 嘴 ----
        if (_motion == WhaleMotion.Alert)
        {
            // 惊讶张嘴
            root.Children.Add(new GeometryDrawing(
                mouthBrush, null,
                new EllipseGeometry(new Point(81, 74 + bodyY), 6.5, 8)));
        }
        else if (_motion == WhaleMotion.Drag)
        {
            // 微张的惊嘴（惊恐）
            root.Children.Add(new GeometryDrawing(
                mouthBrush, null,
                new EllipseGeometry(new Point(81, 75 + bodyY), 4, 5)));
        }
        else if (_motion == WhaleMotion.Edge)
        {
            // 委屈下弯嘴（倒微笑）
            var frown = new StreamGeometry();
            using (var ctx = frown.Open())
            {
                ctx.BeginFigure(new Point(74, 77 + bodyY), false, false);
                ctx.QuadraticBezierTo(new Point(81, 70 + bodyY), new Point(89, 77 + bodyY), true, false);
            }

            frown.Freeze();
            var pen = new Pen(mouthBrush, 2.2);
            pen.Freeze();
            root.Children.Add(new GeometryDrawing(null, pen, frown));
        }
        else
        {
            var smile = new StreamGeometry();
            using (var ctx = smile.Open())
            {
                ctx.BeginFigure(new Point(74, 73 + bodyY), false, false);
                ctx.QuadraticBezierTo(new Point(81, 80 + bodyY), new Point(89, 73 + bodyY), true, false);
            }

            smile.Freeze();
            var pen = new Pen(mouthBrush, 2.2);
            pen.Freeze();
            root.Children.Add(new GeometryDrawing(null, pen, smile));
        }

        // ---- 泡泡（提醒时冒泡）----
        if (_motion == WhaleMotion.Alert)
        {
            double phase = (index % 3) / 3d;
            root.Children.Add(new GeometryDrawing(
                bubbleBrush, null,
                new EllipseGeometry(new Point(50, 34 - phase * 10), 3.4, 3.4)));
            root.Children.Add(new GeometryDrawing(
                bubbleBrush, null,
                new EllipseGeometry(new Point(58, 28 - phase * 8), 2.4, 2.4)));
            root.Children.Add(new GeometryDrawing(
                bubbleBrush, null,
                new EllipseGeometry(new Point(42, 40 - phase * 6), 2, 2)));
        }

        root.Freeze();
        return new DrawingImage(root);
    }

    private static Geometry TailTriangle(Point a, Point b, Point c)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(a, true, true);
            ctx.LineTo(b, true, false);
            ctx.LineTo(c, true, false);
        }

        geo.Freeze();
        return geo;
    }

    private static Geometry TailGeometry(double bodyY)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // 左侧双尾叶（上下两个尖），朝左
            ctx.BeginFigure(new Point(20, 60 + bodyY), true, true);
            ctx.LineTo(new Point(3, 44 + bodyY), true, false);
            ctx.LineTo(new Point(12, 60 + bodyY), true, false);
            ctx.LineTo(new Point(3, 76 + bodyY), true, false);
        }

        // 注意：此处【不能】Freeze——GetFrame 中还要给尾鳍设置 RotateTransform，
        // 冻结后的几何是只读的，设置属性会抛 InvalidOperationException；
        // 几何会在 GetFrame 末尾的 root.Freeze() 时统一进入只读状态。
        return geo;
    }
}
