using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;

namespace PDF_View_2
{
    internal class MyPdfWriter
    {
        public static bool WritePdf(string inputPath, string outputPath, List<ImageContainer> imageList, double zoom)
        {
            if (inputPath == outputPath)
                return false;

            PdfWriter writer = new PdfWriter(outputPath);
            PdfReader reader = new PdfReader(inputPath);
            PdfDocument pdf = new PdfDocument(reader, writer);

            for (int i = 0; i < pdf.GetNumberOfPages(); i++)
            {
                PdfPage page = pdf.GetPage(i + 1);
                var objects = page.GetPdfObject();
                var canvas = new PdfCanvas(page);

                float pageHeight = page.GetPageSize().GetHeight();
                var exportedImages = imageList[i].Export(zoom);
                foreach (var exImg in exportedImages)
                {
                    pageHeight = pageHeight - (float)exImg.Position.Y - exImg.Height;
                    Rectangle imageRectangle = new Rectangle((float)exImg.Position.X, pageHeight, exImg.Width, exImg.Height);
                    Uri imageUri = new Uri(exImg.FileName);
                    canvas.AddImage(ImageDataFactory.Create(imageUri), imageRectangle, false);
                }
            }

            pdf.Close();
            return true;
        }
    }
}
