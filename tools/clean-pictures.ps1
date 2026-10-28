<#
  Cleans the exercise pictures in one batch (Windows PowerShell 5.1, no extra tools):
    - grayscale (no picture in the database has real colour; removes pink/green/blue paper tints),
    - paper whitening relative to each picture's own paper tone: only tones within a few levels of the paper become
      white (soft ramp below that); every stroke darker than the ramp keeps its exact value,
    - optional speck removal: only tiny dots (<= 2 px, <= 4 px on pictures >= 600 px wide) with no other ink within
      6 px. Every removed speck is circled red on the review sheets.
  The size of each picture stays the same; output is an 8-bit grayscale PNG with the same name.

  Step 1 (review):  .\tools\clean-pictures.ps1 [-NoSpecks]
      writes DataBase_clean\ (same folder layout) + DataBase_clean\_review\ (before | after | removed sheets,
      report.csv). DataBase\ is not touched.
  Step 2 (after approval):  .\tools\clean-pictures.ps1 -Apply
      copies the cleaned pictures from DataBase_clean\ over the originals in DataBase\.

  Pictures that are processed: every *.png under DataBase\ except input_4_crop\ folders and files starting with "_".
#>
param(
    [string]$DbRoot = (Join-Path $PSScriptRoot '..\DataBase'),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\DataBase_clean'),
    [switch]$NoSpecks,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$DbRoot = (Resolve-Path $DbRoot).Path

if ($Apply) {
    $OutDir = (Resolve-Path $OutDir).Path
    $n = 0
    Get-ChildItem $OutDir -Recurse -Filter *.png | Where-Object { $_.FullName -notmatch '\\_review\\' } | ForEach-Object {
        $target = Join-Path $DbRoot $_.FullName.Substring($OutDir.Length + 1)
        if (Test-Path $target) { Copy-Item $_.FullName $target -Force; $n++ }
        else { Write-Warning "No original for $($_.FullName) - skipped" }
    }
    "Copied $n cleaned pictures into $DbRoot"
    return
}

Add-Type -AssemblyName PresentationCore, WindowsBase, System.Xaml, System.Drawing
$refs = @(
    [System.Windows.Media.Imaging.BitmapSource].Assembly.Location,
    [System.Windows.Int32Rect].Assembly.Location,
    [System.Drawing.Bitmap].Assembly.Location,
    [System.Xaml.XamlType].Assembly.Location
)
if (-not ('PicClean' -as [type])) {
Add-Type -ReferencedAssemblies $refs -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D = System.Drawing;

public class PicResult
{
    public string Rel; public int Width; public int Height; public int Paper; public int White;
    public int Specks; public int SpeckPixels; public double ChangedPercent; public long BytesIn; public long BytesOut;
    public string Flags = "";
    public D.Bitmap ThumbBefore; public D.Bitmap ThumbAfter; public D.Bitmap ThumbRemoved;
}

public static class PicClean
{
    const int Ramp = 20;            // tones from (white - Ramp) to white are lightened smoothly
    const int InkLevel = 200;       // darker than this = ink (for speck detection)
    const int NearInkLevel = 230;   // anything darker than this within the isolation radius keeps a speck
    const int Isolation = 6;        // px

    public static PicResult Process(string inPath, string outPath, string rel, bool removeSpecks, int panelW, int panelH)
    {
        var dec = BitmapDecoder.Create(new Uri(inPath), BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var conv = new FormatConvertedBitmap(dec.Frames[0], PixelFormats.Bgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight, n = w * h;
        var bgra = new byte[n * 4];
        conv.CopyPixels(bgra, w * 4, 0);

        // Composite on white, luminance
        var rgb = new byte[n * 3];
        var lum = new byte[n];
        for (int i = 0; i < n; i++)
        {
            int a = bgra[i * 4 + 3];
            int b = (bgra[i * 4] * a + 255 * (255 - a)) / 255;
            int g = (bgra[i * 4 + 1] * a + 255 * (255 - a)) / 255;
            int r = (bgra[i * 4 + 2] * a + 255 * (255 - a)) / 255;
            rgb[i * 3] = (byte)r; rgb[i * 3 + 1] = (byte)g; rgb[i * 3 + 2] = (byte)b;
            lum[i] = (byte)Math.Min(255, (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b));
        }

        // Paper tone = most frequent light tone
        var hist = new int[256];
        for (int i = 0; i < n; i++) if (lum[i] >= 200) hist[lum[i]]++;
        int paper = 255, best = 0;
        for (int v = 200; v < 256; v++) if (hist[v] > best) { best = hist[v]; paper = v; }
        int white = Math.Max(228, Math.Min(250, paper - 6));
        int lo = white - Ramp;

        var outp = new byte[n];
        int changed = 0;
        for (int i = 0; i < n; i++)
        {
            int v = lum[i];
            int o = v <= lo ? v : v >= white ? 255 : lo + (int)Math.Round((v - lo) * (255.0 - lo) / (white - lo));
            outp[i] = (byte)o;
            if (Math.Abs(o - v) > 8) changed++;
        }

        var speckCenters = new List<D.Point>();
        int speckPixels = 0;
        if (removeSpecks) speckPixels = RemoveSpecks(outp, w, h, w >= 600 ? 4 : 2, speckCenters);

        var bs = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, outp, w);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bs));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        using (var fs = File.Create(outPath)) enc.Save(fs);

        var res = new PicResult();
        res.Rel = rel; res.Width = w; res.Height = h; res.Paper = paper; res.White = white;
        res.Specks = speckCenters.Count; res.SpeckPixels = speckPixels;
        res.ChangedPercent = Math.Round(100.0 * changed / n, 1);
        res.BytesIn = new FileInfo(inPath).Length; res.BytesOut = new FileInfo(outPath).Length;
        if (res.Specks > 0) res.Flags += "specks ";
        if (paper < 240) res.Flags += "dark-paper ";
        res.Flags = res.Flags.Trim();

        using (var before = FromRgb(rgb, w, h)) res.ThumbBefore = Thumb(before, panelW, panelH, null, 1);
        using (var after = FromGray(outp, w, h, false)) res.ThumbAfter = Thumb(after, panelW, panelH, null, 1);
        using (var faded = FromGray(outp, w, h, true)) res.ThumbRemoved = Thumb(faded, panelW, panelH, speckCenters, w);
        return res;
    }

    // Tiny isolated dark components -> white. Returns the number of pixels changed.
    static int RemoveSpecks(byte[] img, int w, int h, int maxArea, List<D.Point> centers)
    {
        var label = new int[w * h];
        int next = 0, changedPx = 0;
        var stack = new Stack<int>();
        var comp = new List<int>();
        for (int start = 0; start < w * h; start++)
        {
            if (img[start] >= InkLevel || label[start] != 0) continue;
            next++; comp.Clear(); stack.Push(start); label[start] = next;
            while (stack.Count > 0)
            {
                int p = stack.Pop(); comp.Add(p);
                int px = p % w, py = p / w;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int x = px + dx, y = py + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int q = y * w + x;
                    if (label[q] == 0 && img[q] < InkLevel) { label[q] = next; stack.Push(q); }
                }
            }
            if (comp.Count > maxArea) continue;

            int minX = w, minY = h, maxX = 0, maxY = 0;
            foreach (int p in comp) { int x = p % w, y = p / w; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }

            bool isolated = true;
            for (int y = Math.Max(0, minY - Isolation); y <= Math.Min(h - 1, maxY + Isolation) && isolated; y++)
                for (int x = Math.Max(0, minX - Isolation); x <= Math.Min(w - 1, maxX + Isolation); x++)
                {
                    int q = y * w + x;
                    if (img[q] < NearInkLevel && label[q] != next && !(x >= minX - 1 && x <= maxX + 1 && y >= minY - 1 && y <= maxY + 1)) { isolated = false; break; }
                }
            if (!isolated) continue;

            // The speck and its light halo (the 1 px ring around it) become white
            for (int y = Math.Max(0, minY - 1); y <= Math.Min(h - 1, maxY + 1); y++)
                for (int x = Math.Max(0, minX - 1); x <= Math.Min(w - 1, maxX + 1); x++)
                {
                    int q = y * w + x;
                    if (img[q] < 255) { img[q] = 255; changedPx++; }
                }
            centers.Add(new D.Point((minX + maxX) / 2, (minY + maxY) / 2));
        }
        return changedPx;
    }

    static D.Bitmap FromRgb(byte[] rgb, int w, int h)
    {
        var bmp = new D.Bitmap(w, h, D.Imaging.PixelFormat.Format24bppRgb);
        var data = bmp.LockBits(new D.Rectangle(0, 0, w, h), D.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        var row = new byte[data.Stride];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) { int i = (y * w + x) * 3; row[x * 3] = rgb[i + 2]; row[x * 3 + 1] = rgb[i + 1]; row[x * 3 + 2] = rgb[i]; }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), data.Stride);
        }
        bmp.UnlockBits(data);
        return bmp;
    }

    static D.Bitmap FromGray(byte[] g, int w, int h, bool faded)
    {
        var rgb = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++)
        {
            byte v = faded ? (byte)(255 - (255 - g[i]) / 3) : g[i];
            rgb[i * 3] = v; rgb[i * 3 + 1] = v; rgb[i * 3 + 2] = v;
        }
        return FromRgb(rgb, w, h);
    }

    // Picture fitted into a panel (white, grey frame); removed specks circled red
    static D.Bitmap Thumb(D.Bitmap src, int pw, int ph, List<D.Point> marks, int srcW)
    {
        var t = new D.Bitmap(pw, ph);
        using (var g = D.Graphics.FromImage(t))
        {
            g.Clear(D.Color.White);
            g.InterpolationMode = D.Drawing2D.InterpolationMode.HighQualityBicubic;
            double s = Math.Min((pw - 4.0) / src.Width, (ph - 4.0) / src.Height);
            int dw = (int)(src.Width * s), dh = (int)(src.Height * s);
            int ox = (pw - dw) / 2, oy = (ph - dh) / 2;
            g.DrawImage(src, ox, oy, dw, dh);
            using (var pen = new D.Pen(D.Color.FromArgb(170, 170, 170))) g.DrawRectangle(pen, ox - 1, oy - 1, dw + 1, dh + 1);
            if (marks != null)
                using (var red = new D.Pen(D.Color.Red, 2))
                    foreach (var p in marks) g.DrawEllipse(red, (float)(ox + p.X * s - 6), (float)(oy + p.Y * s - 6), 12, 12);
        }
        return t;
    }

    // Review sheets: one cell per picture = before | after | removed (faded result, red circles = removed specks)
    public static List<string> Sheets(List<PicResult> items, string title, string pathPrefix, int panelW, int panelH, int cellsPerRow, int rowsPerPage)
    {
        var written = new List<string>();
        int cellW = panelW * 3 + 30, cellH = panelH + 22, header = 34;
        int perPage = cellsPerRow * rowsPerPage;
        int pages = (items.Count + perPage - 1) / perPage;
        using (var font = new D.Font("Segoe UI", 9))
        using (var bold = new D.Font("Segoe UI", 13, D.FontStyle.Bold))
        using (var red = new D.SolidBrush(D.Color.FromArgb(200, 0, 0)))
            for (int page = 0; page < pages; page++)
            {
                int count = Math.Min(perPage, items.Count - page * perPage);
                int rows = (count + cellsPerRow - 1) / cellsPerRow;
                using (var sheet = new D.Bitmap(cellsPerRow * cellW + 10, header + rows * cellH + 10))
                using (var g = D.Graphics.FromImage(sheet))
                {
                    g.Clear(D.Color.FromArgb(235, 235, 235));
                    g.DrawString(title + "   (page " + (page + 1) + "/" + pages + ")   before | after | removed", bold, D.Brushes.Black, 6, 6);
                    for (int k = 0; k < count; k++)
                    {
                        var it = items[page * perPage + k];
                        int x = 5 + (k % cellsPerRow) * cellW, y = header + (k / cellsPerRow) * cellH;
                        g.DrawImage(it.ThumbBefore, x, y);
                        g.DrawImage(it.ThumbAfter, x + panelW + 4, y);
                        g.DrawImage(it.ThumbRemoved, x + 2 * (panelW + 4), y);
                        string label = Path.GetFileName(it.Rel) + "  paper " + it.Paper + (it.Specks > 0 ? "  specks " + it.Specks : "");
                        g.DrawString(label, font, it.Flags.Length > 0 ? (D.Brush)red : D.Brushes.Black, x, y + panelH + 2);
                    }
                    string file = pathPrefix + "_" + (page + 1).ToString("00") + ".png";
                    sheet.Save(file, D.Imaging.ImageFormat.Png);
                    written.Add(file);
                }
            }
        return written;
    }
}
'@
}

$OutDir = [IO.Path]::GetFullPath($OutDir)
$review = Join-Path $OutDir '_review'
if (Test-Path $review) { Remove-Item $review -Recurse -Force }
New-Item -ItemType Directory -Force $review | Out-Null

$pictures = Get-ChildItem $DbRoot -Recurse -Filter *.png |
    Where-Object { $_.FullName -notmatch '\\input_4_crop\\' -and -not $_.Name.StartsWith('_') }
"Pictures: $($pictures.Count)  ->  $OutDir"

$panelW = 170; $panelH = 110
$all = New-Object System.Collections.Generic.List[object]
foreach ($group in $pictures | Group-Object DirectoryName | Sort-Object Name) {
    $relDir = $group.Name.Substring($DbRoot.Length + 1)
    $items = New-Object 'System.Collections.Generic.List[PicResult]'
    foreach ($f in $group.Group | Sort-Object Name) {
        $rel = $f.FullName.Substring($DbRoot.Length + 1)
        $items.Add([PicClean]::Process($f.FullName, (Join-Path $OutDir $rel), $rel, -not $NoSpecks, $panelW, $panelH))
    }
    # Flagged pictures first, then by name
    $sorted = New-Object 'System.Collections.Generic.List[PicResult]'
    $items | Sort-Object @{ e = { $_.Flags.Length -eq 0 } }, Rel | ForEach-Object { $sorted.Add($_) }
    $sheets = [PicClean]::Sheets($sorted, $relDir, (Join-Path $review ($relDir -replace '[\\/]', '_')), $panelW, $panelH, 3, 10)
    "{0,-22} {1,4} pictures, flagged {2,3}, sheets {3}" -f $relDir, $items.Count, @($items | Where-Object { $_.Flags }).Count, $sheets.Count
    foreach ($it in $items) {
        $all.Add([pscustomobject]@{
            File = $it.Rel; Width = $it.Width; Height = $it.Height; Paper = $it.Paper; WhiteFrom = $it.White
            ChangedPercent = $it.ChangedPercent; Specks = $it.Specks; SpeckPixels = $it.SpeckPixels
            KBIn = [math]::Round($it.BytesIn / 1KB); KBOut = [math]::Round($it.BytesOut / 1KB); Flags = $it.Flags })
        $it.ThumbBefore.Dispose(); $it.ThumbAfter.Dispose(); $it.ThumbRemoved.Dispose()
    }
}

$all | Sort-Object @{ e = { $_.Flags -eq '' } }, File | Export-Csv (Join-Path $review 'report.csv') -NoTypeInformation -Encoding UTF8
"Total: {0:N1} MB -> {1:N1} MB; flagged: {2} (specks {3}, dark paper {4})" -f (($all | Measure-Object KBIn -Sum).Sum / 1KB), (($all | Measure-Object KBOut -Sum).Sum / 1KB),
    @($all | Where-Object Flags).Count, @($all | Where-Object { $_.Specks -gt 0 }).Count, @($all | Where-Object { $_.Paper -lt 240 }).Count
"Review sheets and report.csv: $review"
