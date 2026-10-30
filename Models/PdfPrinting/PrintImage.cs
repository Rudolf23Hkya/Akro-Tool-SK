using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using iText.IO.Image;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    // Pictures as they go into the PDF (#37): grayscale on white, paper tone whitened, scaled down to PrintDpi
    // for the box they are printed in (never enlarged). The source files can be ~1 MB+ (classic 1032×536 RGBA),
    // printed in a box ~1 inch wide; embedding them unchanged made a 1-page plan ~11 MB.
    // No picture in the database has real colour, so grayscale loses nothing.
    internal static class PrintImage
    {
        public const float PrintDpi = 300f;

        // Paper whitening — same rule as tools/clean-pictures.ps1 (keep both in sync): only tones within a few levels
        // of the picture's own paper tone become white, with a soft ramp below; every darker stroke keeps its value.
        private const int Ramp = 20;

        public static ImageData Load(string path, float boxWidthPt, float boxHeightPt)
        {
            var decoder = BitmapDecoder.Create(new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var source = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);

            int w = source.PixelWidth, h = source.PixelHeight;
            var bgra = new byte[w * h * 4];
            source.CopyPixels(bgra, w * 4, 0);

            var gray = ToGrayOnWhite(bgra, w * h);

            // Whiten at full resolution, before scaling: scaling first would average thin grey lines towards the
            // paper tone and the whitening could then erase them
            WhitenPaper(gray);

            // Pixel size at PrintDpi for the size the picture gets in the box (ScaleToFit keeps the aspect ratio)
            double pointsPerPixel = Math.Min(boxWidthPt / w, boxHeightPt / h);
            double factor = Math.Min(1.0, pointsPerPixel / 72.0 * PrintDpi);
            int tw = Math.Max(1, (int)Math.Round(w * factor));
            int th = Math.Max(1, (int)Math.Round(h * factor));
            if (tw < w || th < h)
                gray = AreaResize(gray, w, h, tw, th);
            else { tw = w; th = h; }

            // Raw 8-bit gray; iText stores it Flate-compressed (lossless, no JPEG artefacts on lines)
            return ImageDataFactory.Create(tw, th, 1, 8, gray, null);
        }

        private static byte[] ToGrayOnWhite(byte[] bgra, int count)
        {
            var gray = new byte[count];
            for (int i = 0; i < count; i++)
            {
                int a = bgra[i * 4 + 3];
                double b = (bgra[i * 4] * a + 255 * (255 - a)) / 255.0;
                double g = (bgra[i * 4 + 1] * a + 255 * (255 - a)) / 255.0;
                double r = (bgra[i * 4 + 2] * a + 255 * (255 - a)) / 255.0;
                gray[i] = (byte)Math.Min(255, (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b));
            }
            return gray;
        }

        private static void WhitenPaper(byte[] gray)
        {
            // Paper tone = most frequent light tone
            var hist = new int[256];
            foreach (var v in gray)
                if (v >= 200) hist[v]++;

            int paper = 255, best = 0;
            for (int v = 200; v < 256; v++)
                if (hist[v] > best) { best = hist[v]; paper = v; }

            int white = Math.Clamp(paper - 6, 228, 250);
            int lo = white - Ramp;

            for (int i = 0; i < gray.Length; i++)
            {
                int v = gray[i];
                if (v <= lo) continue;
                gray[i] = v >= white ? (byte)255 : (byte)(lo + (int)Math.Round((v - lo) * (255.0 - lo) / (white - lo)));
            }
        }

        // Downscale by averaging the covered source area (good for line art; no ringing)
        private static byte[] AreaResize(byte[] src, int w, int h, int tw, int th)
        {
            var rows = new double[tw * h];
            ResizeAxis(w, tw, (si, ti, weight) =>
            {
                for (int y = 0; y < h; y++) rows[y * tw + ti] += src[y * w + si] * weight;
            });

            var result = new double[tw * th];
            ResizeAxis(h, th, (si, ti, weight) =>
            {
                for (int x = 0; x < tw; x++) result[ti * tw + x] += rows[si * tw + x] * weight;
            });

            var bytes = new byte[tw * th];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)Math.Clamp((int)Math.Round(result[i]), 0, 255);
            return bytes;
        }

        // For every target index: the source indices it covers and their weights (summing to 1)
        private static void ResizeAxis(int srcLen, int dstLen, Action<int, int, double> add)
        {
            double scale = (double)srcLen / dstLen;
            for (int t = 0; t < dstLen; t++)
            {
                double start = t * scale, end = start + scale;
                for (int s = (int)Math.Floor(start); s < Math.Min(srcLen, (int)Math.Ceiling(end)); s++)
                {
                    double cover = Math.Min(end, s + 1) - Math.Max(start, s);
                    if (cover > 0) add(s, t, cover / scale);
                }
            }
        }
    }
}
