using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using iText.Forms;
using iText.Forms.Fields;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace Atletika_SutaznyPlan_Generator.Models.PdfPrinting
{
    public static class TrainingPlanPdfIText
    {
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

            culture ??= CultureInfo.InvariantCulture;

            using var pdfDoc = new PdfDocument(new PdfReader(templatePath), new PdfWriter(outputPath));
            var form = PdfAcroForm.GetAcroForm(pdfDoc, true);

            // Use this Document for fixed-position image placement
            using var doc = new Document(pdfDoc);

            // 1) Fill TEXT fields from backend (includes per-slot ExerciseDifficulity.* and ExerciseID.*)
            var textMap = data.ToTextFieldMap(culture);
            FillTextFields(form, textMap);

            // 2) Place IMAGES for the 12 tiles + remove their image fields so they cannot cover the images
            var imagePairs = data.ToImageFieldList(); // (Image1_af_image.N, path)
            PlaceImagesIntoFieldRects(pdfDoc, doc, form, imagePairs, imageMarginPoints, removeImageFields);

            // Optional: remove the parent field too (harmless if it doesn't exist)
            if (removeImageFields)
            {
                var parent = form.GetField("Image1_af_image");
                if (parent != null)
                {
                    // If the parent remains after children removal, remove it to keep the form clean
                    form.RemoveField("Image1_af_image");
                }
            }

            // 3) Flatten remaining fields (text fields etc.)
            if (flatten)
                form.FlattenFields();
        }

        private static void FillTextFields(PdfAcroForm form, Dictionary<string, string> textMap)
        {
            // Use GetAllFormFields so we can set only those that exist in the template
            var fields = form.GetAllFormFields();

            foreach (var kv in textMap)
            {
                if (!fields.TryGetValue(kv.Key, out var field) || field == null)
                    continue;

                // Most of yours are text fields; SetValue works fine here.
                // If a field is not a text field, SetValue is still often OK (itext handles many types).
                field.SetValue(kv.Value ?? string.Empty);
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