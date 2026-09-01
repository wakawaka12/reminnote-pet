using System.Windows;
using System.Windows.Media;
using dafeiyu.ViewModels;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace dafeiyu.Animation;

/// <summary>
/// 程序化占位精灵：用 DrawingImage 生成一个简单角色（脸 + 眼睛 + 嘴）的若干帧。
/// 用于在没有真实素材前演示「序列帧动画」机制与状态差异。
/// 待机 = 呼吸/眨眼；提醒 = 弹跳/瞪眼。定义 <see cref="FrameCount"/> 为统一帧数。
/// </summary>
public sealed class PlaceholderSpriteSource : ISpriteSource
{
    private const double Canvas = 120d;
    private const int FramesPerGroup = 6;

    private readonly Color _body;
    private readonly bool _alertMotion;

    public PlaceholderSpriteSource(Color bodyColor, bool alertMotion)
    {
        _body = bodyColor;
        _alertMotion = alertMotion;
    }

    public int FrameCount => FramesPerGroup;

    public ImageSource GetFrame(int index)
    {
        index = ((index % FramesPerGroup) + FramesPerGroup) % FramesPerGroup;

        var group = new DrawingGroup();
        var bodyBrush = new SolidColorBrush(_body);
        bodyBrush.Freeze();

        // 身体：随帧轻微上下移动（待机呼吸 / 提醒弹跳）
        var bounce = _alertMotion ? 6d * Math.Sin(index / (double)FramesPerGroup * Math.PI * 2) : 0d;
        var breathe = _alertMotion ? 0d : 2d * Math.Sin(index / (double)FramesPerGroup * Math.PI * 2);
        var offsetY = bounce - breathe;

        var body = new EllipseGeometry(new Point(Canvas / 2, Canvas / 2 + offsetY * 0.4), 34, 34);
        group.Children.Add(new GeometryDrawing(bodyBrush, null, body));

        // 眼睛（随眨眼张合，提醒态瞪大）
        var eyeOpen = _alertMotion ? 5d : (index % 2 == 0 ? 4.5 : 1.5);
        var eyeBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x2B));
        eyeBrush.Freeze();
        var eyeL = new EllipseGeometry(new Point(Canvas / 2 - 13, Canvas / 2 - 6 + offsetY), 3.2, eyeOpen);
        var eyeR = new EllipseGeometry(new Point(Canvas / 2 + 13, Canvas / 2 - 6 + offsetY), 3.2, eyeOpen);
        group.Children.Add(new GeometryDrawing(eyeBrush, null, eyeL));
        group.Children.Add(new GeometryDrawing(eyeBrush, null, eyeR));

        // 嘴：提醒态张大（惊叹），待机态微笑随帧微动
        var mouthBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x4A));
        mouthBrush.Freeze();
        var mouth = _alertMotion
            ? new EllipseGeometry(new Point(Canvas / 2, Canvas / 2 + 16 + offsetY), 7, 9)
            : new EllipseGeometry(new Point(Canvas / 2, Canvas / 2 + 15 + offsetY), 9, 3.2);
        group.Children.Add(new GeometryDrawing(mouthBrush, null, mouth));

        group.Freeze();
        return new DrawingImage(group);
    }
}
