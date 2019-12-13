using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PDF_View_2
{
    public class ImageContainer
    {
        public List<Image> Images { get; private set; }

        public ImageContainer()
        {
            Images = new List<Image>();
        }

        public void Clear()
        {
            Images.Clear();
        }

        public void Replace(List<Image> newImages)
        {
            Images = newImages;
        }

        public void Add(Image image)
        {
            Images.Add(image);
        }

        public bool Remove(Image image)
        {
            return Images.Remove(image);
        }

        public List<ExportedImage> Export(double zoom)
        {
            List<ExportedImage> exportedImages = new List<ExportedImage>();
            foreach (Image image in Images)
            {
                ExportedImage exported = new ExportedImage();
                // x, y margin
                exported.Position = new Point(image.Margin.Left / zoom, image.Margin.Top / zoom);
                // width and height
                exported.Width = (int)(image.Width / zoom);
                exported.Height = (int)(image.Height / zoom);
                // file name
                exported.FileName = image.Source.ToString();
                exportedImages.Add(exported);
            }

            return exportedImages;
        }

        public void Import(List<ExportedImage> exportedImages, MouseButtonEventHandler mouseDownHandler, MouseButtonEventHandler mouseUpHandler, MouseEventHandler mouseMoveHandler)
        {
            Images.Clear();
            foreach (ExportedImage ex in exportedImages)
            {
                ImageSource imageSource;
                try
                {
                    imageSource = new BitmapImage(new Uri(@"C:\Users\Tenny\Pictures\microsoft-account.png"));
                    imageSource.Freeze();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    continue;
                }
                Image image = new Image();
                image.Width = ex.Width;
                image.Height = ex.Height;
                image.Margin = new Thickness(ex.Position.X, ex.Position.Y, 0,0);
                image.Source = imageSource;
                image.MouseLeftButtonDown += mouseDownHandler;
                image.MouseLeftButtonUp += mouseUpHandler;
                image.MouseMove += mouseMoveHandler;
                Images.Add(image);
            }
        }
    }

    public class ExportedPage
    {
        public int PageIndex { get; set; }
        public List<ExportedImage> ExportedImages { get; set; }
    }

    public class ExportedImage
    {
        public string FileName { get; set; }
        public Point Position { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
