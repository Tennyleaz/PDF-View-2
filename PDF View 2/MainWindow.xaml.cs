using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Globalization;
using PdfiumViewer;

namespace PDF_View_2
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private PdfDocument pdfDoc;
        private string filepath;
        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
        private readonly Process currentProcess = Process.GetCurrentProcess();
        private int renderWidth = 0;
        private int renderHeight = 0;
        private const double maxZoom = 3;
        private double minZoom = 3;
        private int currentPage;
        //private Vector lastOffsetBackground = new Vector();
        //private Vector lastOffset = new Vector();
        //private Vector origionalBackground = new Vector();
        // 拖曳和選圖用的變數
        private bool _started;
        private Point _downPoint;
        private bool DragInProgress = false;
        private Image movingObject;  // 記錄拖曳圖片資料
        private Point StartPosition; // 移動開始時的座標
        private Point EndPosition;   // 移動結束時的座標
        private double imgScreenWidth, imgScreenHeight;  // 縮放點用的
        // 暫存圖片用的變數
        private List<ImageContainer> imageList;

        public MainWindow()
        {
            InitializeComponent();
            imageList = new List<ImageContainer>();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //iSize = m_iWidth * m_iHeigh;
            SizeChanged += MainWindow_OnSizeChanged;
            grid2.MouseDown += CanvasGrid_MouseDown;
            grid2.MouseMove += CanvasGrid_MouseMove;
            grid2.MouseUp += CanvasGrid_MouseUp;
            ShowImageResizeHandle(false);
        }

        #region load and save PDF files

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Title = "Save PDF";
            dialog.Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*";
            dialog.FileName = filepath;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
        }

        private async void LoadPDFButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*";
                dialog.Title = "Open PDF File";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    pdfDoc = PdfDocument.Load(dialog.FileName);
                    filepath = dialog.FileName;
                    labelTotalPages.Content = pdfDoc.PageCount;

                    if (pdfDoc.PageCount > 0)
                    {
                        for (int i = 0; i < pdfDoc.PageCount; i++)
                            imageList.Add(new ImageContainer());
                        await GoPage(0);
                        currentPage = 0;
                        DeterminePageButton();
                        btnEmbed.IsEnabled = true;
                        //btnClear.IsEnabled = true;
                        btnSave.IsEnabled = true;
                        Zoom.IsEnabled = true;
                    }
                    else
                        pdfDoc = null;
                }
            }
        }

        #endregion

        #region render and switch PDF pages

        private BitmapSource RenderPageToMemDC(int page)
        {
            // cast the result of the expression, not the width/height values
            renderWidth = (int)(pdfDoc.PageSizes[page].Width * maxZoom);  // produce the max allowed image, and resize to smaller on display
            renderHeight = (int)(pdfDoc.PageSizes[page].Height * maxZoom);

            var image = pdfDoc.Render(page, renderWidth, renderHeight, 300, 300, false);
            return BitmapHelper.ToBitmapSource(image);
        }

        private async Task GoPage(int page)
        {
            if (page < 0 || page +1 > pdfDoc.PageCount)
                return;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            imageMemDC.Source = await Task.Run(() =>
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    return RenderPageToMemDC(page);
                }
                , tokenSource.Token);

            // calculate min zoom
            double wScale = scrollViewer.ActualWidth / renderWidth;
            double hScale = scrollViewer.ActualHeight / renderHeight;
            minZoom = Math.Min(wScale, hScale);
            //Console.WriteLine("minZoom=" + minZoom);
            Zoom.Minimum = minZoom;
            //origionalBackground = lastOffsetBackground = VisualTreeHelper.GetOffset(imageMemDC);
            movingObject = null;

            labelMemDC.Content = string.Format("Page: {0}, Memory: {1} MB, Time: {2:0.0} sec",
                page,
                ConvertBytesToMegabytes(currentProcess.WorkingSet64),
                sw.Elapsed.TotalSeconds);

            currentProcess.Refresh();
            GC.Collect();

            LoadImagesToPage();
            ShowImageResizeHandle(false);
            btnClear.IsEnabled = false;
        }

        private async void BtnPrev_OnClick(object sender, RoutedEventArgs e)
        {
            if (currentPage <= 0)
                return;
            currentPage--;
            await GoPage(currentPage);
            DeterminePageButton();
        }

        private async void BtnNext_OnClick(object sender, RoutedEventArgs e)
        {
            if (currentPage + 1 >= pdfDoc.PageCount)
                return;
            currentPage++;
            await GoPage(currentPage);
            DeterminePageButton();
        }

        private async void BtnGoPage_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtPage.Text, out int page))
                return;
            
            await GoPage(page-1);
        }

        #endregion

        private static double ConvertBytesToMegabytes(long bytes)
        {
            return (bytes / 1024f) / 1024f;
        }

        private void Zoom_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (pdfDoc == null)
                return;
            ShowImageResizeHandle(false);
        }

        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (pdfDoc == null)
                return;
            // calculate min zoom
            double wScale = scrollViewer.ActualWidth / renderWidth;
            double hScale = scrollViewer.ActualHeight / renderHeight;
            minZoom = Math.Min(wScale, hScale);
            //Console.WriteLine("minZoom=" + minZoom);
            Zoom.Minimum = minZoom;
            /*if (upLeftHandle.Visibility == Visibility.Visible)
            {
                CalculateImageResizeHandle(movingObject);
                MoveImageResizeHandle(movingObject);
            }*/
            ShowImageResizeHandle(false);
        }

        private void DeterminePageButton()
        {
            txtPage.Text = (currentPage + 1).ToString();
            btnPrev.IsEnabled = (currentPage > 0);
            btnNext.IsEnabled = (currentPage + 1 < pdfDoc.PageCount);
        }

        #region draw rectangle and embed images

        private void CanvasGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DragInProgress)
                return;
            _started = true;
            _downPoint = e.GetPosition(grid2);
            //Point centerPoint = new Point(canvasGrid.ActualWidth, canvasGrid.ActualHeight);
            //_downPoint.X -= centerPoint.X;
            //_downPoint.Y -= centerPoint.Y;
        }
        
        private void CanvasGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _started = false;
            btnClear.IsEnabled = true;
        }

        private void CanvasGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (DragInProgress)
                return;
            if (_started)
            {
                Point point = e.GetPosition(grid2);
                Rect rect = new Rect(_downPoint, point);
                pickRectangle.Margin = new Thickness(rect.Left, rect.Top, 0, 0);
                pickRectangle.Width = rect.Width;
                pickRectangle.Height = rect.Height;
                pickRectangle.Visibility = Visibility.Visible;
            }
        }
        
        private void BtnEmbed_OnClick(object sender, RoutedEventArgs e)
        {
            // 重新定義Transform属性
            Image img = new Image();
            img.Margin = pickRectangle.Margin;
            img.Width = pickRectangle.Width;
            img.Height = pickRectangle.Height;
            img.Stretch = Stretch.Fill;
            img.HorizontalAlignment = HorizontalAlignment.Left;
            img.VerticalAlignment = VerticalAlignment.Top;
            ImageSource imageSource = new BitmapImage(new Uri(@"C:\Users\Tenny\Pictures\microsoft-account.png"));
            imageSource.Freeze();
            img.Source = imageSource;
            img.MouseLeftButtonDown += Image_MouseLeftButtonDown;
            img.MouseMove += Image_MouseMove;
            img.MouseLeftButtonUp += Img_MouseLeftButtonUp;
            canvasGrid.Children.Add(img);
            imageList[currentPage].Add(img);
        }

        #endregion

        private void Img_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Img_MouseLeftButtonUp");
            Image img = sender as Image;
            DragInProgress = false;
            //movingObject = null;
            Panel.SetZIndex(img, 0);
            btnDelete.IsEnabled = true;
            btnClear.IsEnabled = true;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Image_MouseLeftButtonDown");
            Image img = sender as Image;
            DragInProgress = false;
            movingObject = img;
            StartPosition = e.GetPosition(img);
            CalculateImageResizeHandle(img);
            MoveImageResizeHandle(img);
            Panel.SetZIndex(img, 1);
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Image img && e.LeftButton == MouseButtonState.Pressed && img == movingObject)
            {
                EndPosition = e.GetPosition(grid2);
                img.Margin = new Thickness(EndPosition.X - StartPosition.X, EndPosition.Y - StartPosition.Y, 0, 0);
                MoveImageResizeHandle(img);
                DragInProgress = true;
            }
        }

        private void BtnClear_OnClick(object sender, RoutedEventArgs e)
        {
            ShowImageResizeHandle(false);
            btnClear.IsEnabled = false;
        }

        private void MoveImageResizeHandle(Image img)
        {
            if (img == null)
            {
                ShowImageResizeHandle(false);
                return;
            }

            pickRectangle.Margin = img.Margin;
            Point relativePoint = img.TransformToAncestor(grid1).Transform(new Point(0, 0));
            upLeftHandle.Margin = new Thickness(relativePoint.X - 2, relativePoint.Y - 2, 0, 0);
            upRightHandle.Margin = new Thickness(relativePoint.X - 3 + imgScreenWidth, relativePoint.Y - 2, 0, 0);
            buttomLeftHandle.Margin = new Thickness(relativePoint.X - 2, relativePoint.Y - 3 + imgScreenHeight, 0, 0);
            buttomRightHandle.Margin = new Thickness(relativePoint.X - 3 + imgScreenWidth, relativePoint.Y - 3 + imgScreenHeight, 0, 0);
        }

        private void CalculateImageResizeHandle(Image img)
        {
            if (img == null)
                return;
            double imgWidth = img.Width;
            double imgHeight = img.Height;
            pickRectangle.Width = imgWidth;
            pickRectangle.Height = imgHeight;
            pickRectangle.Margin = img.Margin;
            imgScreenWidth = imgWidth * Zoom.Value;
            imgScreenHeight = imgHeight * Zoom.Value;
            ShowImageResizeHandle(true);
        }

        private void ShowImageResizeHandle(bool isVisible)
        {
            pickRectangle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            upLeftHandle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            upRightHandle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            buttomLeftHandle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            buttomRightHandle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        private void LoadImagesToPage()
        {
            canvasGrid.Children.Clear();
            foreach (Image image in imageList[currentPage].Images)
            {
                canvasGrid.Children.Add(image);
            }
        }

        private void BtnDelete_OnClick(object sender, RoutedEventArgs e)
        {
            if (movingObject != null)
            {
                imageList[currentPage].Images.Remove(movingObject);
                canvasGrid.Children.Remove(movingObject);
                ShowImageResizeHandle(false);
                btnClear.IsEnabled = false;
            }
        }
        }
    }
}
