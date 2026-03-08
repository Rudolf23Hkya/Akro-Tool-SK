using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Font;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    public static class TrainingPlanPdfIText
    {
        private const string DefaultFontRelativePath = @"Assets\NotoSans-Bold.ttf";

        /// <summary>
        /// Exports a flattened PDF from the template using TrainingPlanFormData:
        /// - fills all text fields (including per-tile ExerciseDifficulity.* and ExerciseID.*)
        /// - places 12 images into Image1_af_image.0..11
        /// - removes the image fields (so placeholders can't cover images)
        /// - flattens remaining fields (so no AcroForm controls remain)
        /// </summary>
        public static void ExportFromFormData(
            string templatePath,
            string outputPath,
            TrainingPlanFormData data,
            CultureInfo? culture = null,
            float imageMarginPoints = 3f,
            bool removeImageFields = true,
            bool flatten = true
        )
        {
            culture ??= new CultureInfo("sk-SK");

            if (data is null) throw new ArgumentNullException(nameof(data));
            if (!File.Exists(templatePath)) throw new FileNotFoundException("Template PDF not found.", templatePath);

            string fontPath = ResolveFontPath(DefaultFontRelativePath);
            if (!File.Exists(fontPath))
                throw new FileNotFoundException("PDF font not found.", fontPath);

            using var pdfDoc = new PdfDocument(new PdfReader(templatePath), new PdfWriter(outputPath));
            var form = PdfAcroForm.GetAcroForm(pdfDoc, true);

            // Embedded Unicode font for Slovak characters
            PdfFont font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);

            // Use this Document for fixed-position image placement
            using var doc = new Document(pdfDoc);

            // 1) Fill TEXT fields from backend (includes per-slot ExerciseDifficulity.* and ExerciseID.*)
            var textMap = data.ToTextFieldMap(culture);
            FillTextFields(form, textMap, font);

            // 2) Place IMAGES for the 12 tiles + remove their image fields so they cannot cover the images
            var imagePairs = data.ToImageFieldList(); // (Image1_af_image.N, path)
            PlaceImagesIntoFieldRects(pdfDoc, doc, form, imagePairs, imageMarginPoints, removeImageFields);

            // Optional: remove the parent field too (harmless if it doesn't exist)
            if (removeImageFields)
            {
                var parent = form.GetField("Image1_af_image");
                if (parent != null)
                {
                    form.RemoveField("Image1_af_image");
                }
            }

            // 3) Flatten remaining fields (text fields etc.)
            if (flatten)
                form.FlattenFields();
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

                var imgData = ImageDataFactory.Create(imgPath);
                var img = new Image(imgData);

                // Fit inside rect with a small margin so it doesn't touch/breach grid lines
                var w = Math.Max(1f, rect.GetWidth() - 2 * marginPoints);
                var h = Math.Max(1f, rect.GetHeight() - 2 * marginPoints);

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