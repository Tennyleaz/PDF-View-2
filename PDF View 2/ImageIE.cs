using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;
using Newtonsoft.Json;

namespace PDF_View_2
{
    internal class ImageIE
    {
        public static bool Export(List<ImageContainer> imageList, string exportPath, double zoom)
        {
            List<ExportedPage> document = new List<ExportedPage>();
            for (int i=0; i<imageList.Count; i++)
            {
                ExportedPage page = new ExportedPage();
                page.PageIndex = i;
                page.ExportedImages = imageList[i].Export(zoom);
                document.Add(page);
            }

            //var options = new JsonSerializerOptions
            //{
            //    WriteIndented = true
            //};

            try
            {
                string jsonString = JsonConvert.SerializeObject(document);
                if (Path.GetExtension(exportPath) != ".json")
                    exportPath += @"\.json";
                if (File.Exists(exportPath))
                    File.Delete(exportPath);
                File.WriteAllText(exportPath, jsonString, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        public static bool Import(string jsonPath, List<ImageContainer> listToBeImported, 
            MouseButtonEventHandler mouseDownHandler, MouseButtonEventHandler mouseUpHandler, MouseEventHandler mouseMoveHandler)
        {
            List<ExportedPage> document;
            try
            {
                string jsonString = File.ReadAllText(jsonPath);
                document = JsonConvert.DeserializeObject<List<ExportedPage>>(jsonString);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

            if (listToBeImported.Count < document.Count)
                return false;

            foreach (ExportedPage page in document)
            {
                listToBeImported[page.PageIndex].Import(page.ExportedImages, mouseDownHandler, mouseUpHandler, mouseMoveHandler);
            }

            return true;
        }
    }
}
