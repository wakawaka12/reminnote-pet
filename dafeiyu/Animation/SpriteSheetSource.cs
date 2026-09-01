using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace dafeiyu.Animation;

/// <summary>
/// Godot 式精灵图集（SpriteSheet）帧源：一张行条幅图，按时序逐格切片播放。
/// 行条幅约定：一行 N 帧横排（格高=条幅高、格宽=条幅宽/帧数）。
/// <paramref name="columns"/> 显式指定帧数（推荐）；为 0 时按 round(宽/高) 自动推断（仅适配方格）。
/// 缺失/加载失败时由调用方回退到基图或占位精灵。
/// </summary>
public sealed class SpriteSheetSource : ISpriteSource
{
    private readonly BitmapImage _sheet;
    private readonly int _frameCount;
    private readonly double _frameW;
    private readonly double _frameH;

    public SpriteSheetSource(string sheetPath, int columns = 0)
    {
        var fullPath = Path.IsPathRooted(sheetPath)
            ? sheetPath
            : Path.Combine(AppContext.BaseDirectory, sheetPath);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        _sheet = bitmap;

        _frameH = _sheet.PixelHeight;
        if (columns > 0)
        {
            _frameCount = columns;
        }
        else
        {
            _frameCount = Math.Max(1, (int)Math.Round(_sheet.PixelWidth / _frameH));
        }

        _frameW = _sheet.PixelWidth / (double)_frameCount;
    }

    public int FrameCount => _frameCount;

    public ImageSource GetFrame(int index)
    {
        index = ((index % _frameCount) + _frameCount) % _frameCount;
        var rect = new Int32Rect((int)(index * _frameW), 0, (int)_frameW, (int)_frameH);
        var cropped = new CroppedBitmap(_sheet, rect);
        cropped.Freeze();
        return cropped;
    }
}
