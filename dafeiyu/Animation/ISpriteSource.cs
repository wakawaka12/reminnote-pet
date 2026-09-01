using System.Windows.Media;

namespace dafeiyu.Animation;

/// <summary>
/// 精灵帧源。返回一帧帧图像。骨架阶段默认用 <see cref="PlaceholderSpriteSource"/> 程序生成；
/// 未来接入宿主应用时，可提供从 Assets/Sprite/*.png 切片的实现（见 docs/07_ASSET_SPEC.md），
/// 驱动层无需改动。
/// </summary>
public interface ISpriteSource
{
    int FrameCount { get; }
    ImageSource GetFrame(int index);
}
