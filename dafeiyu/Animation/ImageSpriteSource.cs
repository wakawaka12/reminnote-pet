using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace dafeiyu.Animation;

/// <summary>
/// 从 <c>Assets/Sprite</c> 读取真 PNG 的帧源，供「待机/提醒/点按」使用。
/// 约定文件名见 docs/07_ASSET_SPEC.md：pet_idle / pet_idle_blink / pet_alert / pet_tap ...
/// - <paramref name="basePath"/> 主图；
/// - <paramref name="altPath"/> 可选交替帧（比如眨眼），在 <paramref name="altFrameIndex"/> 帧切换；
/// - <paramref name="framePaths"/> 可选精灵帧组（如 pet_hidden_top_0/1.png）：存在时按帧序循环播放，
///   每帧停留 2 个时序节拍；帧组缺失时回退到主图/占位；
/// - 若图不存在（或加载失败）回退到 <paramref name="fallback"/> 占位精灵，保证程序始终可运行。
/// </summary>
public sealed class ImageSpriteSource : ISpriteSource
{
    private const int FrameCountValue = 6; // 复用统一的时序帧数（单图 + 交替帧模式）

    private readonly ImageSource? _base;
    private readonly ImageSource? _alt;
    private readonly int _altFrameIndex;
    private readonly int _fallbackFrameCount;
    private readonly ISpriteSource _fallback;
    private readonly ImageSource[]? _frameImages;

    public ImageSpriteSource(
        string basePath,
        string? altPath = null,
        int altFrameIndex = -1,
        ISpriteSource? fallback = null,
        IReadOnlyList<string>? framePaths = null)
    {
        _base = Load(basePath);
        _alt = Load(altPath);
        _altFrameIndex = altFrameIndex;
        _fallback = fallback ?? new PlaceholderSpriteSource(
            Color.FromArgb(0xFF, 0x6C, 0x8C, 0xFF),
            alertMotion: false);
        _fallbackFrameCount = _fallback.FrameCount;
        _frameImages = LoadAll(framePaths);
    }

    /// <summary>是否启用了精灵帧组（多帧序列）。</summary>
    public bool UsesFrameSequence => _frameImages is { Length: > 0 };

    public int FrameCount =>
        _frameImages is { Length: > 0 }
            ? FrameCountValue   // 统一 6 拍节奏（与待机眨眼一致）
            : _base is not null || _alt is not null
                ? FrameCountValue
                : _fallbackFrameCount;

    public ImageSource GetFrame(int index)
    {
        if (_frameImages is { Length: > 0 })
        {
            // “眨眼式”动画节奏：大部分拍子保持常态（帧0），仅第 5 拍短暂“压紧/挣扎”一次（帧1），
            // 与待机眨眼同样自然舒缓，避免持续交替造成的“不连贯/鬼畜感”。
            index = ((index % FrameCountValue) + FrameCountValue) % FrameCountValue;
            var frameIdx = index == 4 && _frameImages.Length > 1 ? 1 : 0;
            return _frameImages[frameIdx];
        }

        if (_base is null && _alt is null)
        {
            return _fallback.GetFrame(index);
        }

        index = ((index % FrameCountValue) + FrameCountValue) % FrameCountValue;

        if (_alt is not null && index == _altFrameIndex)
        {
            return _alt;
        }

        return _base ?? _fallback.GetFrame(index);
    }

    private static ImageSource? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>加载帧组；任一帧缺失则整体视为未提供（回退主图/占位）。</summary>
    private static ImageSource[]? LoadAll(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            return null;
        }

        var result = new List<ImageSource>(paths.Count);
        foreach (var path in paths)
        {
            var image = Load(path);
            if (image is null)
            {
                return null;
            }

            result.Add(image);
        }

        return result.Count > 0 ? result.ToArray() : null;
    }
}
