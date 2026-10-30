using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.Utils;
using iText.Layout;
using iText.Layout.Element;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    public static class TrainingPlanPdfIText
    {
        // Same folder name and case as in the project (assets\NotoSans-Bold.ttf, copied next to the exe)
        private static readonly string DefaultFontRelativePath = System.IO.Path.Combine("assets", "NotoSans-Bold.ttf");

        /// <summary>
        /// Exports a flattened PDF from the template using TrainingPlanFormData:
        /// - the picture boxes are filled in routine order (Okno 1, 2.a, 2.b, 3, ... — issue #30);
        ///   when they don't fit on one sheet, more pages are added (same template, same header)
        /// - per box: picture, ExerciseDifficulity.k, ExerciseID.k and the box number
        /// - image fields are removed (so placeholders can't cover images), the rest is flattened
        /// </summary>
        public static void ExportFromFormData(
            string templatePath,
            string outputPath,
            TrainingPlanFormData data,
            CultureInfo? culture = null,
            float imageMarginPoints = 3f
        )
        {
            culture ??= new CultureInfo("sk-SK");

            if (data is null) throw new ArgumentNullException(nameof(data));
            if (!File.Exists(templatePath)) throw new FileNotFoundException("Template PDF not found.", templatePath);

            string fontPath = ResolveFontPath(DefaultFontRelativePath);
            if (!File.Exists(fontPath))
                throw new FileNotFoundException("PDF font not found.", fontPath);

            var header = data.ToHeaderFieldMap(culture);
            var boxes = data.ToPrintBoxes();

            // The template decides how many boxes fit on one sheet (16 since the template edit for #30)
            int boxesPerPage = CountPictureBoxes(templatePath);
            if (boxesPerPage == 0)
                throw new InvalidOperationException("The PDF template has no picture boxes.");

            int pageCount = Math.Max(1, (boxes.Count + boxesPerPage - 1) / boxesPerPage);

            // Each page is filled and flattened on its own, then all pages are joined into the output file
            // Smart mode: identical objects (the template's fonts and logos) are stored once, not once per page (#37)
            using var output = new PdfDocument(new PdfWriter(outputPath, new WriterProperties().UseSmartMode()));
            var merger = new PdfMerger(output);

            for (int page = 0; page < pageCount; page++)
            {
                var pageBoxes = boxes.Skip(page * boxesPerPage).Take(boxesPerPage).ToList();
                var pageBytes = FillPage(templatePath, fontPath, header, pageBoxes, boxesPerPage,
                                         isFirstPage: page == 0, culture, imageMarginPoints);

                using var pageDoc = new PdfDocument(new PdfReader(new MemoryStream(pageBytes)));
                merger.Merge(pageDoc, 1, pageDoc.GetNumberOfPages());
            }
        }

        // Fills one copy of the template: header + the given boxes (box k of this page = Image1_af_image.k)
        private static byte[] FillPage(
            string templatePath,
            string fontPath,
            Dictionary<string, string> header,
            IReadOnlyList<PrintBox> boxes,
            int boxesPerPage,
            bool isFirstPage,
            CultureInfo culture,
            float imageMarginPoints)
        {
            using var ms = new MemoryStream();

            using (var pdfDoc = new PdfDocument(new PdfReader(templatePath), new PdfWriter(ms)))
            {
                var form = PdfAcroForm.GetAcroForm(pdfDoc, true);

                // Embedded Unicode font for Slovak characters
                PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);

                // Use this Document for fixed-position image placement
                using var doc = new Document(pdfDoc);

                // Box rectangles are needed for the box numbers below, before the image fields are removed
                var boxRects = Enumerable.Range(0, boxesPerPage)
                    .Select(k => form.GetField(AcroFieldNames.ImageField(k))?.GetWidgets()?.FirstOrDefault()?.GetRectangle().ToRectangle())
                    .ToList();

                // 1) Text: header + per-box value / ID / number
                var textMap = new Dictionary<string, string>(header);
                var printedNumbers = new List<(int box, string label)>();

                // Printed numbers of unused boxes are blanked on extra pages and on a renumbered page (Zvlášť),
                // so the sheet never reads "… 7, 8, 11, 12"; a plain first page keeps them as before
                bool blankUnusedNumbers = !isFirstPage ||
                    boxes.Where((b, k) => b.Label != (k + 1).ToString(CultureInfo.InvariantCulture)).Any();

                for (int k = 0; k < boxesPerPage; k++)
                {
                    var label = k < boxes.Count ? boxes[k].Label : "";

                    if (k < boxes.Count)
                    {
                        if (boxes[k].Difficulty is decimal difficulty)
                            textMap[AcroFieldNames.ExerciseDifficultyField(k)] = difficulty.ToString("0.000", culture);
                        textMap[AcroFieldNames.ExerciseIdField(k)] = boxes[k].ExerciseId;
                    }

                    if (form.GetField(AcroFieldNames.BoxNumberField(k)) != null)
                    {
                        if (label.Length > 0)
                            textMap[AcroFieldNames.BoxNumberField(k)] = label;
                    }
                    else
                    {
                        // Template without BoxNumber fields: the numbers 1..N are printed text.
                        // Replace a number only where it differs (unused boxes: see blankUnusedNumbers).
                        var printed = (k + 1).ToString(CultureInfo.InvariantCulture);
                        if (label.Length > 0 ? label != printed : blankUnusedNumbers)
                            printedNumbers.Add((k, label));
                    }
                }

                FillTextFields(form, textMap, font);

                // 2) Pictures + remove their image fields so they cannot cover the images
                var imagePairs = boxes.Select((b, k) => (AcroFieldNames.ImageField(k), b.ImagePath)).ToList();
                PlaceImagesIntoFieldRects(pdfDoc, doc, form, imagePairs, imageMarginPoints, removeImageFields: true);

                // Remove the parent field too, so unused boxes print empty
                if (form.GetField(AcroFieldNames.ImageFieldsParent) != null)
                    form.RemoveField(AcroFieldNames.ImageFieldsParent);

                // 3) Box numbers drawn over the printed ones (old template only), regular face of similar size
                if (printedNumbers.Count > 0)
                {
                    var numberFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                    foreach (var (box, label) in printedNumbers)
                    {
                        if (boxRects[box] is Rectangle rect)
                            ReplacePrintedBoxNumber(pdfDoc.GetPage(1), rect, label, numberFont);
                    }
                }

                // Value cells of the continuation boxes (n.b, n.c, ...), read before flattening removes the fields
                var greyedValueRects = boxes
                    .Select((b, k) => b.IsContinuation
                        ? form.GetField(AcroFieldNames.ExerciseDifficultyField(k))?.GetWidgets()?.FirstOrDefault()?.GetRectangle().ToRectangle()
                        : null)
                    .OfType<Rectangle>()
                    .ToList();

                // 4) Flatten remaining fields (text fields etc.)
                form.FlattenFields();

                // 5) Grey value cell under n.b, n.c, ...: only n.a carries the Okno value (issue #44).
                // Drawn after flattening, so no field appearance can cover it.
                if (greyedValueRects.Count > 0)
                    GreyOutCells(pdfDoc.GetPage(1), greyedValueRects);
            }

            return ms.ToArray();
        }

        // Number of picture boxes: Image1_af_image.0, .1, ... without gaps
        private static int CountPictureBoxes(string templatePath)
        {
            using var pdfDoc = new PdfDocument(new PdfReader(templatePath));
            var form = PdfAcroForm.GetAcroForm(pdfDoc, false);
            if (form == null) return 0;

            int count = 0;
            while (form.GetField(AcroFieldNames.ImageField(count)) != null)
                count++;
            return count;
        }

        // The printed box number sits in the strip just above the picture rectangle, at its left edge
        // (Cvicebny-plan_Template.pdf: 9.8 pt text, baseline ~4.3 pt above the picture; 13–16 slightly smaller, still covered). Cover it with white
        // and write the new label ("2.b") in its place; an empty label only blanks the number.
        private static void ReplacePrintedBoxNumber(PdfPage page, Rectangle pictureRect, string label, PdfFont font)
        {
            var canvas = new PdfCanvas(page);

            canvas.SaveState()
                  .SetFillColor(ColorConstants.WHITE)
                  .Rectangle(pictureRect.GetLeft() + 1.5f, pictureRect.GetTop() + 1.5f, 30f, 10.5f)
                  .Fill()
                  .RestoreState();

            if (label.Length > 0)
            {
                canvas.BeginText()
                      .SetFontAndSize(font, 8.5f)
                      .MoveText(pictureRect.GetLeft() + 1.9f, pictureRect.GetTop() + 4.3f)
                      .ShowText(label)
                      .EndText();
            }

            canvas.Release();
        }

        // Light grey (80 % white) fill, inset so the table lines around the cell stay visible
        private static void GreyOutCells(PdfPage page, IEnumerable<Rectangle> cells)
        {
            const float inset = 0.5f;
            var canvas = new PdfCanvas(page);

            canvas.SaveState().SetFillColor(new DeviceGray(0.8f));
            foreach (var r in cells)
                canvas.Rectangle(r.GetLeft() + inset, r.GetBottom() + inset, r.GetWidth() - 2 * inset, r.GetHeight() - 2 * inset);
            canvas.Fill().RestoreState();

            canvas.Release();
        }

        private static void FillTextFields(PdfAcroForm form, Dictionary<string, string> textMap, PdfFont font)
        {
            var fields = form.GetAllFormFields();

            foreach (var kv in textMap)
            {
                if (!fields.TryGetValue(kv.Key, out var field) || field == null)
                    continue;

                string value = kv.Value ?? string.Empty;

                try
                {
                    // Preserve the field's current font size if possible.
                    // 0 means auto-size in AcroForm DA strings, so we keep that behavior.
                    float fontSize = GetFontSizeOrDefault(field, 0f);

                    field.SetValue(value, font, fontSize);
                    field.RegenerateField();
                }
                catch
                {
                    // Fallback: still try to set the value if the field type behaves differently.
                    field.SetValue(value, font, 0f);
                    field.RegenerateField();
                }
            }
        }

        private static float GetFontSizeOrDefault(PdfFormField field, float defaultValue)
        {
            try
            {
                // Many AcroForm text fields expose the current font size.
                // If unavailable or invalid, use the provided default.
                float size = field.GetFontSize();
                return size >= 0 ? size : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static void PlaceImagesIntoFieldRects(
            PdfDocument pdfDoc,
            Document doc,
            PdfAcroForm form,
            IReadOnlyList<(string fieldName, string imagePath)> images,
            float marginPoints,
            bool removeImageFields
        )
        {
            // The same picture in several boxes (Zvlášť members with the same exercise) is embedded once
            var embedded = new Dictionary<(string path, int w, int h), PdfImageXObject>();

            foreach (var (fieldName, imgPath) in images)
            {
                if (string.IsNullOrWhiteSpace(fieldName)) continue;
                if (string.IsNullOrWhiteSpace(imgPath) || !File.Exists(imgPath)) continue;

                var field = form.GetField(fieldName);
                if (field == null) continue;

                var widget = field.GetWidgets()?.FirstOrDefault();
                if (widget == null) continue;

                Rectangle rect = widget.GetRectangle().ToRectangle();
                int pageNum = pdfDoc.GetPageNumber(widget.GetPage());

                // Fit inside rect with a small margin so it doesn't touch/breach grid lines
                var w = Math.Max(1f, rect.GetWidth() - 2 * marginPoints);
                var h = Math.Max(1f, rect.GetHeight() - 2 * marginPoints);

                // Grayscale, print resolution for this box (#37)
                var key = (imgPath, (int)Math.Round(w), (int)Math.Round(h));
                if (!embedded.TryGetValue(key, out var xObject))
                    embedded[key] = xObject = new PdfImageXObject(PrintImage.Load(imgPath, w, h));
                var img = new Image(xObject);

                img.ScaleToFit(w, h);

                // Center inside the original rect
                float x = rect.GetLeft() + (rect.GetWidth() - img.GetImageScaledWidth()) / 2f;
                float y = rect.GetBottom() + (rect.GetHeight() - img.GetImageScaledHeight()) / 2f;

                img.SetFixedPosition(pageNum, x, y);
                doc.Add(img);

                // Remove the image field so its widget/placeholder cannot appear above the image
                if (removeImageFields)
                    form.RemoveField(fieldName);
            }
        }

        private static string ResolveFontPath(string relativePath)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(baseDir, relativePath);
        }

        // Utility: print all fields (helps confirm field names)
        public static void DumpFieldNames(string templatePath)
        {
            using var pdfDoc = new PdfDocument(new PdfReader(templatePath));
            var form = PdfAcroForm.GetAcroForm(pdfDoc, false);
            if (form == null)
            {
                Console.WriteLine("No AcroForm found.");
                return;
            }

            foreach (var kv in form.GetAllFormFields())
                Console.WriteLine(kv.Key);
        }
    }
}