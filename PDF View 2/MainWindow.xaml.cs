﻿using System;
using System.Collections.Generic;
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
using PdfTracker;

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
        private int renderWidth = 0;   // PDF底圖原始(最大)寬度
        private int renderHeight = 0;  // PDF底圖原始(最大)寬度
        private const double MAX_ZOOM = 3;
        private double minZoom = 3;
        private int currentPage;
        // 拖曳和選圖用的變數
        private bool _started;  // 拖曳建立選取區用的
        private Point _downPoint;  // 選取區的初始座標
        private bool DragInProgress = false;
        private Point StartPosition; // 移動開始時的座標
        private Point EndPosition;   // 移動結束時的座標
        private double imgScreenWidth, imgScreenHeight;  // 縮放點用的
        private Image _movingObject;  // 不該存取這個！用下面的 MovingObject 管理
        /// <summary>
        /// 記錄拖曳圖片資料
        /// </summary>
        private Image MovingObject
        {
            get => _movingObject;
            set
            {
                // move last selected item back
                if (_movingObject != null)
                    Panel.SetZIndex(_movingObject, 0);
                // move new item to topmost
                _movingObject = value;
                if (_movingObject != null)
                    Panel.SetZIndex(_movingObject, 1);
            }
        }
        // 暫存圖片用的變數
        private List<ImageContainer> imageList;
        private const int MAX_IMAGES_PER_PAGE = 10;
        private Image deletedItem;
        private readonly MatomoAnalytics matomoAnalytics;

        public MainWindow()
        {
            InitializeComponent();
            imageList = new List<ImageContainer>();
            matomoAnalytics = new MatomoAnalytics();
            matomoAnalytics.Init();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SizeChanged += MainWindow_OnSizeChanged;
            grid2.MouseDown += CanvasGrid_MouseDown;
            grid2.MouseMove += CanvasGrid_MouseMove;
            grid2.MouseUp += CanvasGrid_MouseUp;
            ShowImageResizeHandle(false);
            matomoAnalytics.TrackOP(PDF_OP.Launch);
        }

        #region load and save PDF files

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.GetDirectoryName(filepath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(filepath);
            path += @"\" + fileName + " - new.pdf";
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Title = "Save PDF";
            dialog.Filter = "PDF Files (*.pdf)|*.pdf";
            dialog.FileName = path;
            dialog.OverwritePrompt = true;

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            if (dialog.FileName == filepath)
            {
                MessageBox.Show("You need to save to a new file.");
                return;
            }

            if (MyPdfWriter.WritePdf(filepath, dialog.FileName, imageList, MAX_ZOOM))
                MessageBox.Show("OK");
            else
                MessageBox.Show("Failed!");
            /*string path = System.IO.Path.GetDirectoryName(filepath);
            path += @"\test.json";
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Title = "Save JSON";
            dialog.Filter = "JSON Files (*.json)|*.json";
            dialog.FileName = path;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            if (ImageIE.Export(imageList, dialog.FileName, MAX_ZOOM))
                MessageBox.Show("OK");
            else
                MessageBox.Show("Failed!");*/
        }

        private async void LoadPDFButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*";
                dialog.Title = "Open PDF File";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        pdfDoc = PdfDocument.Load(dialog.FileName);
                    }
                    catch (PdfException ex)
                    {
                        if (ex.Error == PdfError.PasswordProtected)
                        {
                            if (!TryLoadPdfWithPassword(dialog.FileName))
                            {
                                MessageBox.Show("Open PDF failed!");
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show(ex.ToString());
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        return;
                    }

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
                        btnSave.IsEnabled = true;
                        Zoom.IsEnabled = true;
                        pageControl.IsEnabled = true;
                    }
                    else
                    {
                        pdfDoc = null;
                        pageControl.IsEnabled = false;
                        btnEmbed.IsEnabled = false;
                        btnSave.IsEnabled = false;
                        Zoom.IsEnabled = false;
                    }
                }
            }
        }

        private bool TryLoadPdfWithPassword(string fileName)
        {
            PasswordWindow passWin = new PasswordWindow();
            passWin.Owner = this;
            if (passWin.ShowDialog() != true)
                return false;
            try
            {
                pdfDoc = PdfDocument.Load(fileName, passWin.Password);
                return true;
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.ToString());
                Console.WriteLine(e);
            }

            return false;
        }

        #endregion

        #region render and switch PDF pages

        private BitmapSource RenderPageToMemDC(int page)
        {
            // cast the result of the expression, not the width/height values
            renderWidth = (int)(pdfDoc.PageSizes[page].Width * MAX_ZOOM);  // produce the max allowed image, and resize to smaller on display
            renderHeight = (int)(pdfDoc.PageSizes[page].Height * MAX_ZOOM);

            var image = pdfDoc.Render(page, renderWidth, renderHeight, 300, 300, false);
            return BitmapHelper.ToBitmapSource(image);
        }

        private async Task GoPage(int page)
        {
            if (page < 0 || page +1 > pdfDoc.PageCount)
                return;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            imageMemDC.Source = await Task.Factory.StartNew(() =>  // Task.Run() in .net 4.5+
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    return RenderPageToMemDC(page);
                }
                , tokenSource.Token);

            // calculate min zoom
            double wScale = scrollViewer.ActualWidth / renderWidth;
            double hScale = scrollViewer.ActualHeight / renderHeight;
            minZoom = Math.Min(wScale, hScale);
            Zoom.Minimum = minZoom;

            labelMemDC.Content = string.Format("Page: {0}, Memory: {1} MB, Time: {2:0.0} sec",
                page,
                Utility.ConvertBytesToMegabytes(currentProcess.WorkingSet64),
                sw.Elapsed.TotalSeconds);

            currentProcess.Refresh();
            GC.Collect();

            deletedItem = null;
            btnRecoverDelete.IsEnabled = false;
            MovingObject = null;
            currentPage = page;
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

        private async void TxtPage_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!int.TryParse(txtPage.Text, out int page))
                    return;
                if (page <= 0 || page > pdfDoc.PageCount)
                    return;
                if (currentPage == page - 1)
                    return;
                currentPage = page - 1;
                await GoPage(currentPage);
                DeterminePageButton();
            }
        }

        #endregion

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
                MovingObject = null;
                ShowImageResizeHandle(false);
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
            if (pickRectangle.Visibility != Visibility.Visible)
                return;
            if (imageList[currentPage].Images.Count >= MAX_IMAGES_PER_PAGE)
            {
                MessageBox.Show($"Max image per page is {MAX_IMAGES_PER_PAGE}!");
                return;
            }

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

            // select created image
            MovingObject = img;
            btnDelete.IsEnabled = true;
        }

        #endregion

        private void Img_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Img_MouseLeftButtonUp");
            Image img = sender as Image;
            DragInProgress = false;
            //movingObject = null;
            //Panel.SetZIndex(img, 0);
            btnDelete.IsEnabled = true;
            btnClear.IsEnabled = true;
            this.Cursor = Cursors.Arrow;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Image_MouseLeftButtonDown");
            Image img = sender as Image;
            DragInProgress = false;

            MovingObject = img;
            StartPosition = e.GetPosition(img);
            CalculateImageResizeHandle(img);
            MoveImageResizeHandle(img);
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Image img && e.LeftButton == MouseButtonState.Pressed && img == MovingObject)
            {
                EndPosition = e.GetPosition(grid2);
                //labelDebug.Content = $"EndPosition={(int) EndPosition.X}, {(int) EndPosition.Y}";
                // check pdf page bound
                if (EndPosition.X < 0)
                    EndPosition.X = 0;
                if (EndPosition.Y < 0)
                    EndPosition.Y = 0;
                if (EndPosition.X > imageMemDC.ActualWidth)
                    EndPosition.X = imageMemDC.ActualWidth;
                if (EndPosition.Y > imageMemDC.ActualHeight)
                    EndPosition.Y = imageMemDC.ActualHeight;
                img.Margin = new Thickness(EndPosition.X - StartPosition.X, EndPosition.Y - StartPosition.Y, 0, 0);
                MoveImageResizeHandle(img);
                DragInProgress = true;
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void BtnClear_OnClick(object sender, RoutedEventArgs e)
        {
            ShowImageResizeHandle(false);
            btnClear.IsEnabled = false;
        }

        private void BtnClearPage_OnClick(object sender, RoutedEventArgs e)
        {
            MovingObject = null;
            canvasGrid.Children.Clear();
            imageList[currentPage].Clear();
        }

        private void MoveImageResizeHandle(Image img)
        {
            if (img == null)
            {
                ShowImageResizeHandle(false);
                return;
            }
            
            Point relativePoint = img.TransformToAncestor(grid1).Transform(new Point(0, 0));
            resizeControl.Margin = new Thickness(relativePoint.X, relativePoint.Y, 0, 0);
        }

        private void CalculateImageResizeHandle(Image img)
        {
            if (img == null)
                return;
            double imgWidth = img.Width;
            double imgHeight = img.Height;
            
            imgScreenWidth = imgWidth * Zoom.Value;
            imgScreenHeight = imgHeight * Zoom.Value;
            ShowImageResizeHandle(true);
            resizeControl.Width = imgScreenWidth;
            resizeControl.Height = imgScreenHeight;
        }

        private void ShowImageResizeHandle(bool isVisible)
        {
            resizeControl.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            pickRectangle.Visibility = Visibility.Collapsed;
            
            if (!isVisible)
                btnDelete.IsEnabled = false;
        }

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
            if (MovingObject != null)
            {
                deletedItem = MovingObject;
                imageList[currentPage].Remove(MovingObject);
                canvasGrid.Children.Remove(MovingObject);
                ShowImageResizeHandle(false);
                btnClear.IsEnabled = false;
                btnRecoverDelete.IsEnabled = true;
            }
        }

        private void BtnRecoverDelete_OnClick(object sender, RoutedEventArgs e)
        {
            if (deletedItem != null)
            {
                canvasGrid.Children.Add(deletedItem);
                imageList[currentPage].Add(deletedItem);

                // select created image
                MovingObject = deletedItem;
                btnDelete.IsEnabled = true;
                btnRecoverDelete.IsEnabled = false;
            }
        }

        private void ResizeControl_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DragInProgress)
                return;
            if (MovingObject == null)
                return;
            if (e.PreviousSize == e.NewSize)
                return;
            
            // resize image
            double imgActualWidth = e.NewSize.Width / Zoom.Value;
            double imgActualHeight = e.NewSize.Height / Zoom.Value;
            MovingObject.Width = imgActualWidth;
            MovingObject.Height = imgActualHeight;
            // move image margin
            Point relativePoint = resizeControl.TransformToVisual(grid2).Transform(new Point(0, 0));
            MovingObject.Margin = new Thickness(relativePoint.X, relativePoint.Y, 0, 0);
        }
    }
}
