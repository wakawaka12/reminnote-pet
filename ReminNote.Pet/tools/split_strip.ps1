# Split a horizontal multi-pose strip into per-pose images.
# Inspired by dafeiyu-pet / hatch-pet "row strip -> cells" workflow.
#
# Usage:
#   $env:PET_BASE = 'D:\YourBase'
#   & .\tools\split_strip.ps1 -Strip 'D:\YourBase\pet_assets_raw\strip.png' -CellCount 5
# Output: <base>\pet_assets_raw\hidden_top.png / hidden_bottom.png / hidden_left.png / hidden_right.png / drag.png
# (order = left-to-right: top, bottom, left, right, drag)

param(
    [Parameter(Mandatory=$true)][string]$Strip,
    [int]$CellCount = 5
)

$ErrorActionPreference = 'Stop'
$Base = if ($env:PET_BASE) { $env:PET_BASE } else { 'D:\' + [char]0x684C + [char]0x5BA0 }
$RawDir = Join-Path $Base 'pet_assets_raw'
if (-not (Test-Path $RawDir)) { New-Item -ItemType Directory -Path $RawDir -Force | Out-Null }

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

public static class StripSplit
{
    public static int Split(string srcPath, string outDir, int cellCount)
    {
        var bmp = new Bitmap(srcPath);
        int w = bmp.Width, h = bmp.Height;
        var colHas = new bool[w];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y += 6)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 40) { colHas[x] = true; break; }
            }
        }

        // Even split into N cells, then shrink each cell to its content bounds.
        var picked = new List<int[]>();
        int cellW = w / cellCount;
        for (int i = 0; i < cellCount; i++)
        {
            int x0 = i * cellW;
            int x1 = (i == cellCount - 1) ? w - 1 : (i + 1) * cellW - 1;
            int a0 = x0, a1 = x1;
            for (int x = x0; x <= x1; x++)
            {
                bool has = false;
                for (int y = 0; y < h; y += 6) if (bmp.GetPixel(x, y).A > 40) { has = true; break; }
                if (has) { a0 = x; break; }
            }
            for (int x = x1; x >= x0; x--)
            {
                bool has = false;
                for (int y = 0; y < h; y += 6) if (bmp.GetPixel(x, y).A > 40) { has = true; break; }
                if (has) { a1 = x; break; }
            }
            if (a1 < a0) continue;
            picked.Add(new int[] { a0, a1 });
        }

        string[] names = { "hidden_top", "hidden_bottom", "hidden_left", "hidden_right", "drag" };
        int saved = 0;
        for (int i = 0; i < picked.Count && i < names.Length; i++)
        {
            int x0 = picked[i][0], x1 = picked[i][1];
            // vertical content bounds inside this column range
            int y0 = h, y1 = -1;
            for (int y = 0; y < h; y += 4)
            {
                for (int x = x0; x <= x1; x += 4)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 40) { if (y < y0) y0 = y; if (y > y1) y1 = y; break; }
                }
            }
            if (y1 < 0) continue;
            int pad = 12;
            int cx0 = Math.Max(0, x0 - pad), cx1 = Math.Min(w - 1, x1 + pad);
            int cy0 = Math.Max(0, y0 - pad), cy1 = Math.Min(h - 1, y1 + pad);
            int cw = cx1 - cx0 + 1, ch = cy1 - cy0 + 1;
            var cell = new Bitmap(cw, ch);
            using (var g = Graphics.FromImage(cell))
            {
                g.Clear(Color.White);
                g.DrawImage(bmp, new Rectangle(0, 0, cw, ch), new Rectangle(cx0, cy0, cw, ch), GraphicsUnit.Pixel);
            }
            cell.Save(System.IO.Path.Combine(outDir, names[i] + ".png"), ImageFormat.Png);
            cell.Dispose();
            saved++;
        }
        bmp.Dispose();
        return saved;
    }
}
"@

$count = [StripSplit]::Split((Get-Item $Strip).FullName, $RawDir, $CellCount)
Write-Output "Split done: $count cells -> $RawDir"
