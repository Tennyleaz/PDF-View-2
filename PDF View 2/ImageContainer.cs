using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        public bool Delete(Image image)
        {
            return Images.Remove(image);
        }

        public void Expoet()
        {
            throw new NotImplementedException();
        }
    }
}
