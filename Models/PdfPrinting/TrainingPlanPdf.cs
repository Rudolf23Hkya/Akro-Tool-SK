using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    public static class TrainingPlanPdf
    {
        // PDF coords (origin bottom-left): x1,y1,x2,y2 for the 12 image widgets
        private static readonly (double x1, double y1, double x2, double y2)[] SlotRectsPdf =
        {
            // Top row: 1..8
            (138.982, 332.291, 214.691, 388.800),
            (215.345, 332.182, 289.912, 388.800),
            (289.882, 332.182, 364.909, 388.800),
            (364.800, 332.182, 439.854, 388.800),
            (439.732, 332.182, 515.018, 388.800),
            (514.909, 332.182, 589.909, 388.800),
            (589.745, 332.141, 664.609, 388.800),
            (665.345, 332.182, 740.454, 388.800),

            // Bottom row: 9..12
            (139.091, 234.982, 215.018, 291.273),
            (214.964, 235.043, 289.909, 291.218),
            (289.745, 235.077, 364.663, 291.218),
            (364.541, 235.125, 440.038, 291.212),
        };

        /// <summary>
        /// Draws up to 12 images into the 12 slots of the template PDF.
        /// This does NOT fill the AcroForm fields; it removes the 12 widget placeholders and draws images on the page.
        /// </summary>
        public static void Fill12Images(string templatePath, string outputPath, IReadOnlyList<string> imagePaths)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Template PDF not found.", templatePath);

            using var doc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
            var page = doc.Pages[0];

            HideImagePlaceholders(page);

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            int count = Math.Min(12, imagePaths.Count);
            for (int i = 0; i < count; i++)
            {
                var path = imagePaths[i];
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                var box = ToXRect(page, SlotRectsPdf[i]);

                using var img = XImage.FromFile(path);

                // Choose one:
                DrawImageContain(gfx, img, box); // safe: no cropping
                //DrawImageCover(gfx, img, box);  // fills box, crops a bit
            }

            doc.Save(outputPath);
        }

        private static void HideImagePlaceholders(PdfPage page)
        {
            var annots = page.Annotations;
            const double tol = 0.75; // points tolerance

            for (int i = 0; i < annots.Count; i++)
            {
                var a = annots[i];

                // Only widgets (form field visuals)
                var subtype = a.Elements.GetName("/Subtype");
                if (!string.Equals(subtype, "/Widget", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Match by rectangle
                var r = a.Rectangle;
                var rect = (x1: r.X1, y1: r.Y1, x2: r.X2, y2: r.Y2);

                if (SlotRectsPdf.Any(s => RectClose(rect, s, tol)))
                {
                    // Annotation flags: 2 = Hidden (not displayed, not printed)
                    // This hides the placeholder icon reliably.
                    a.Elements.SetInteger("/F", 2);

                    // Optional: some viewers cache appearances; removing /AP can help
                    // (safe to try; if your PDFsharp build doesn't support Remove, comment this out)
                    a.Elements.Remove("/AP");
                }
            }
        }

        private static bool RectClose(
            (double x1, double y1, double x2, double y2) a,
            (double x1, double y1, double x2, double y2) b,
            double tol)
            => Math.Abs(a.x1 - b.x1) <= tol &&
               Math.Abs(a.y1 - b.y1) <= tol &&
               Math.Abs(a.x2 - b.x2) <= tol &&
               Math.Abs(a.y2 - b.y2) <= tol;

        // Convert PDF rect (bottom-left origin) -> PDFsharp draw rect (top-left origin)
        private static XRect ToXRect(PdfPage page, (double x1, double y1, double x2, double y2) r)
        {
            double x = r.x1;
            double w = r.x2 - r.x1;
            double h = r.y2 - r.y1;

            double pageH = page.Height.Point;
            double yTopLeft = pageH - r.y2;

            return new XRect(x, yTopLeft, w, h);
        }

        // Fits entire image inside the box (no cropping)
        private static void DrawImageContain(XGraphics gfx, XImage img, XRect box)
        {
            double iw = img.PixelWidth;
            double ih = img.PixelHeight;

            double scale = Math.Min(box.Width / iw, box.Height / ih);
            double w = iw * scale;
            double h = ih * scale;

            double x = box.X + (box.Width - w) / 2;
            double y = box.Y + (box.Height - h) / 2;

            gfx.DrawImage(img, x, y, w, h);
        }

        // Fills the box completely (crops image if aspect ratios differ)
        private static void DrawImageCover(XGraphics gfx, XImage img, XRect box)
        {
            double iw = img.PixelWidth;
            double ih = img.PixelHeight;

            double scale = Math.Max(box.Width / iw, box.Height / ih);
            double w = iw * scale;
            double h = ih * scale;

            double x = box.X + (box.Width - w) / 2;
            double y = box.Y + (box.Height - h) / 2;

            gfx.DrawImage(img, x, y, w, h);
        }
    }
}