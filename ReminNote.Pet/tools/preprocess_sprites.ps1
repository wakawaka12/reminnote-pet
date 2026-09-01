# Pet sprite preprocessing pipeline (inspired by dafeiyu-pet preprocess.py / preprocess2.py)
#
# Usage:
#   1) Put raw white-background images into: <base>\pet_assets_raw
#      Naming rule (file name contains the keyword, rest is ignored):
#        drag           -> pet_drag.png           (centered, base pose)
#        hidden_top     -> pet_hidden_top.png     (head touches top edge = ceiling push)
#        hidden_bottom  -> pet_hidden_bottom.png  (feet touch bottom edge = lying)
#        hidden_left    -> pet_hidden_left.png    (body touches left edge = squeezed left)
#        hidden_right   -> pet_hidden_right.png   (body touches right edge = squeezed right)
#   2) Run (caller must set $env:PET_BASE to the base dir containing "pet_assets_raw"):
#      $env:PET_BASE = 'D:\YourBase'; & .\tools\preprocess_sprites.ps1
#   3) Output: <base>\ReminNote.Pet\Assets\Sprite\pet_*.png  (transparent, contact-edge aligned, de-fringed)

$ErrorActionPreference = 'Stop'

# Base directory is injected by caller (non-ASCII path avoided in this file).
$Base = if ($env:PET_BASE) { $env:PET_BASE } else { 'D:\' + [char]0x684C + [char]0x5BA0 }
$RawDir = Join-Path $Base 'pet_assets_raw'
$OutDir = Join-Path $Base 'ReminNote.Pet\Assets\Sprite'

if (-not (Test-Path $RawDir)) { Write-Error "Raw dir not found: $RawDir"; exit 1 }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

public static class PetPreprocess
{
    public static string MatchOutput(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("hidden_top")) return "pet_hidden_top";
        if (n.Contains("hidden_bottom")) return "pet_hidden_bottom";
        if (n.Contains("hidden_left")) return "pet_hidden_left";
        if (n.Contains("hidden_right")) return "pet_hidden_right";
        if (n.Contains("run")) return "pet_run";
        if (n.Contains("drag")) return "pet_drag";
        return null;
    }

    public static string Process(string srcPath, string outPath, string mode)
    {
        var bmp = new Bitmap(srcPath);
        int w = bmp.Width, h = bmp.Height;
        var visited = new bool[w * h];
        var stack = new Stack<int>();

        for (int x = 0; x < w; x++) { TryPush(bmp, visited, stack, w, h, x, 0); TryPush(bmp, visited, stack, w, h, x, h - 1); }
        for (int y = 0; y < h; y++) { TryPush(bmp, visited, stack, w, h, 0, y); TryPush(bmp, visited, stack, w, h, w - 1, y); }

        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % w, y = i / w;
            bmp.SetPixel(x, y, Color.FromArgb(0, 255, 255, 255));
            TryPush(bmp, visited, stack, w, h, x - 1, y);
            TryPush(bmp, visited, stack, w, h, x + 1, y);
            TryPush(bmp, visited, stack, w, h, x, y - 1);
            TryPush(bmp, visited, stack, w, h, x, y + 1);
        }

        // De-fringe: clear low-alpha whitish halo pixels.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A > 0 && c.A < 130 && c.R > 200 && c.G > 200 && c.B > 200)
                    bmp.SetPixel(x, y, Color.FromArgb(0, 255, 255, 255));
            }

        // Content bounding box.
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (bmp.GetPixel(x, y).A > 8)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < 0) { bmp.Dispose(); return "EMPTY"; }

        // Align contact side to image edge.
        int dx = 0, dy = 0;
        switch (mode)
        {
            case "top":    dy = -minY; break;
            case "bottom": dy = (h - 1) - maxY; break;
            case "left":   dx = -minX; break;
            case "right":  dx = (w - 1) - maxX; break;
        }

        var outBmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(outBmp))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(bmp, dx, dy, w, h);
        }
        bmp.Dispose();
        outBmp.Save(outPath, ImageFormat.Png);
        outBmp.Dispose();
        return "OK";
    }

    private static void TryPush(Bitmap bmp, bool[] visited, Stack<int> stack, int w, int h, int x, int y)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = y * w + x;
        if (visited[i]) return;
        Color c = bmp.GetPixel(x, y);
        if (c.A == 0) { visited[i] = true; return; }
        if (c.R >= 240 && c.G >= 240 && c.B >= 240)
        {
            visited[i] = true;
            stack.Push(i);
        }
    }
}
"@

$files = Get-ChildItem $RawDir -Filter *.png
if (-not $files) { Write-Output "No PNG inputs."; exit 0 }

foreach ($f in $files) {
    $key = [PetPreprocess]::MatchOutput($f.Name)
    if (-not $key) { Write-Output "Skipped (keyword not matched): $($f.Name)"; continue }
    $mode = if ($key -like '*top') { 'top' } elseif ($key -like '*bottom') { 'bottom' } elseif ($key -like '*left') { 'left' } elseif ($key -like '*right') { 'right' } else { '' }
    # Sheet（Godot 式行条幅）输入：输出为 {key}_sheet.png,保持整张宽度,仅透明化+内容贴顶对齐
    $isSheet = $f.Name -like '*_sheet*'
    $outName = if ($isSheet) { "$key`_sheet.png" } else { "$key.png" }
    $outPath = Join-Path $OutDir $outName
    $result = [PetPreprocess]::Process($f.FullName, $outPath, $mode)
    if ($result -eq 'EMPTY') { Write-Output "Failed (empty content): $($f.Name)"; continue }
    Write-Output "OK: $($f.Name) -> $outName  (mode=$mode)"
}

Write-Output "Done. Output dir: $OutDir"
