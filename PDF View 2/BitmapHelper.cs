using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;

namespace PDF_View_2
{
    internal class BitmapHelper
    {
        public static BitmapSource ToBitmapSource(Image image)
        {
            return ToBitmapSource(image as Bitmap);
        }

        /// <summary>
        /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.Source
        /// </summary>
        /// <param name="bitmap">The Source Bitmap</param>
        /// <returns>The equivalent BitmapSource</returns>
        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null) return null;

            using (Bitmap source = (Bitmap)bitmap.Clone())
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                NativeMethods.DeleteObject(ptr); //release the HBitmap
                bs.Freeze();
                return bs;
            }
        }

        public static BitmapSource ToBitmapSource(byte[] bytes, int width, int height, int dpiX, int dpiY)
        {
            var result = BitmapSource.Create(
                            width,
                            height,
                            dpiX,
                            dpiY,
                            PixelFormats.Bgra32,
                            null /* palette */,
                            bytes,
                            width * 4 /* stride */);
            result.Freeze();

            return result;
        }

        public static ImageFormat SwichImageFormat(ref string strFileType)
        {
            switch (strFileType)
            {
                case "Bmp":
                {
                    strFileType = "Bmp";
                    return ImageFormat.Bmp;
                }
                case "Jpeg":
                {
                    strFileType = "Jpg";
                    return ImageFormat.Jpeg;
                }
                case "Png":
                {
                    strFileType = "Png";
                    return ImageFormat.Png;
                }
                case "Tiff":
                {

                    strFileType = "Tif";
                    return ImageFormat.Tiff;
                }
                default:
                {
                    strFileType = "Bmp";
                    return ImageFormat.Bmp;
                }
            }
        }
    }
}
