using System;
using System.Collections.Generic;
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
        public static void FillImagesAndFlatten(
            string templatePath,
            string outputPath,
            IReadOnlyList<string> imagePaths,
            IReadOnlyList<string> imageFieldNames // 12 names, in slot order 1..12
        )
        {
            const float pad = 0.90f; // 10% smaller for padding
            const float margin = 3f; // maring

            if (imagePaths.Count < imageFieldNames.Count)
                throw new ArgumentException("Not enough image paths provided.");

            using var pdfDoc = new PdfDocument(new PdfReader(templatePath), new PdfWriter(outputPath));
            var form = PdfAcroForm.GetAcroForm(pdfDoc, true);

            // This 'Document' lets us add images easily with fixed positioning
            using var doc = new Document(pdfDoc);

            for (int i = 0; i < imageFieldNames.Count; i++)
            {
                var fieldName = imageFieldNames[i];
                var imgPath = imagePaths[i];

                if (!File.Exists(imgPath))
                    continue;

                var field = form.GetField(fieldName);
                if (field == null)
                    continue;

                var widget = field.GetWidgets()?.FirstOrDefault();
                if (widget == null)
                    continue;

                // Where is the field?
                Rectangle rect = widget.GetRectangle().ToRectangle();
                int pageNum = pdfDoc.GetPageNumber(widget.GetPage());

                // Add image into that rectangle
                var imgData = ImageDataFactory.Create(imgPath);
                var img = new Image(imgData);

                // Scale to fit (contain) and center in the rect
                //img.ScaleToFit(rect.GetWidth() * pad, rect.GetHeight() * pad);
                img.ScaleToFit(rect.GetWidth() - 2 * margin, rect.GetHeight() - 2 * margin);

                float x = rect.GetLeft() + (rect.GetWidth() - img.GetImageScaledWidth()) / 2f;
                float y = rect.GetBottom() + (rect.GetHeight() - img.GetImageScaledHeight()) / 2f;

                img.SetFixedPosition(pageNum, x, y);

                doc.Add(img);

                // Remove the field so its widget/placeholder can't cover the image
                form.RemoveField(fieldName);
            }

            // Flatten remaining fields (text fields etc.)
            form.FlattenFields();  // :contentReference[oaicite:6]{index=6}
        }

        // Utility: print all fields (helps you discover names)
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
