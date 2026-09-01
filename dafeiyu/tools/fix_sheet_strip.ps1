# Fix a horizontal strip: detect per-cell content via white gaps and re-compose
# an even-width sheet, so frame slicing (round-robin) cuts exactly on cell borders.
# Usage:
#   & .\tools\fix_sheet_strip.ps1 -In <strip.png> -Out <fixed.png> -Cols 8
param(
    [Parameter(Mandatory=$true)][string]$In,
    [Parameter(Mandatory=$true)][string]$Out,
    [int]$Cols = 8
)

$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

public static class StripFix
{
    // content column = has non-white, opaque pixel
    private static bool HasContent(Bitmap b, int x, int h)
    {
        for (int y = 0; y < h; y += 6)
        {
            Color c = b.GetPixel(x, y);
            if (c.A > 40 && (c.R < 243 || c.G < 243 || c.B < 243)) return true;
        }
        return false;
    }

    public static void Fix(string srcPath, string outPath, int cols)
    {
        var src = new Bitmap(srcPath);
        int w = src.Width, h = src.Height;
        var colHas = new bool[w];
        for (int x = 0; x < w; x++) colHas[x] = HasContent(src, x, h);

        // white gap bands
        var gaps = new List<int[]>(); // {start,end}
        int gs = -1;
        for (int x = 0; x < w; x++)
        {
            if (!colHas[x]) { if (gs < 0) gs = x; }
            else { if (gs >= 0 && x - gs >= 4) gaps.Add(new int[] { gs, x - 1 }); gs = -1; }
        }
        if (gs >= 0 && w - gs >= 4) gaps.Add(new int[] { gs, w - 1 });

        // content segments = between gap midpoints (or image edges)
        var bounds = new List<int>();
        bounds.Add(0);
        foreach (var g in gaps) bounds.Add((g[0] + g[1]) / 2);
        bounds.Add(w - 1);

        var segs = new List<int[]>();
        if (gaps.Count >= cols - 1)
        {
            for (int i = 0; i + 1 < bounds.Count && segs.Count < cols; i++)
            {
                int x0 = bounds[i], x1 = bounds[i + 1];
                if (x1 - x0 < 8) continue;
                segs.Add(new int[] { x0, x1 });
            }
        }
        else
        {
            // fallback: even split
            for (int i = 0; i < cols; i++)
                segs.Add(new int[] { i * w / cols, (i + 1) * w / cols - 1 });
        }

        // shrink each seg to content bbox
        var items = new List<Rectangle>();
        foreach (var s in segs)
        {
            int x0 = s[0], x1 = s[1];
            int minX = -1, maxX = -1, minY = h, maxY = -1;
            for (int x = x0; x <= x1; x++)
            {
                bool has = HasContent(src, x, h);
                if (has) { if (minX < 0) minX = x; maxX = x; }
            }
            if (minX < 0) continue;
            for (int y = 0; y < h; y += 4)
            {
                for (int x = minX; x <= maxX; x += 4)
                {
                    Color c = src.GetPixel(x, y);
                    if (c.A > 40 && (c.R < 243 || c.G < 243 || c.B < 243))
                    {
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        break;
                    }
                }
            }
            if (maxY < 0) { minY = 0; maxY = h - 1; }
            items.Add(new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }

        if (items.Count == 0) { src.Dispose(); throw new Exception("no content found"); }

        // compose even-width sheet: cell width = max content width + 2*pad
        int pad = 12;
        int cw = 0;
        foreach (var it in items) cw = Math.Max(cw, it.Width);
        cw += pad * 2;
        int n = items.Count;
        var outBmp = new Bitmap(cw * n, h);
        using (var g = Graphics.FromImage(outBmp))
        {
            g.Clear(Color.Transparent);
            for (int i = 0; i < n; i++)
            {
                var it = items[i];
                int dx = i * cw + (cw - it.Width) / 2;
                g.DrawImage(src, new Rectangle(dx, it.Y, it.Width, it.Height), it, GraphicsUnit.Pixel);
            }
        }
        src.Dispose();
        outBmp.Save(outPath, ImageFormat.Png);
        outBmp.Dispose();
    }
}
"@

[StripFix]::Fix((Get-Item $In).FullName, (Get-Item $Out).FullName, $Cols)
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::new((Get-Item $Out).FullName)
Write-Output ("fixed sheet: {0}x{1}, cols={2}, cellW={3}" -f $b.Width, $b.Height, $Cols, [math]::Round($b.Width/$Cols))
$b.Dispose()
