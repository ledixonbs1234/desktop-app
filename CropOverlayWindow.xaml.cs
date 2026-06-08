using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using desktop_app.Services;

namespace desktop_app;

public partial class CropOverlayWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;
    public BitmapSource? CroppedImage { get; private set; }

    public CropOverlayWindow()
    {
        InitializeComponent();
        
        BackgroundImage.Source = ScreenshotBuffer.GetCachedScreenshot();
        
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
            }
        };
    }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSelecting = true;
        _startPoint = e.GetPosition(OverlayCanvas);
        
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var currentPoint = e.GetPosition(OverlayCanvas);
        double x = Math.Min(_startPoint.X, currentPoint.X);
        double y = Math.Min(_startPoint.Y, currentPoint.Y);
        double w = Math.Abs(currentPoint.X - _startPoint.X);
        double h = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;

        double x = Canvas.GetLeft(SelectionRect);
        double y = Canvas.GetTop(SelectionRect);
        double w = SelectionRect.Width;
        double h = SelectionRect.Height;

        if (w > 10 && h > 10)
        {
            try
            {
                var cached = ScreenshotBuffer.GetCachedScreenshot();
                if (cached != null)
                {
                    var croppedBitmap = new CroppedBitmap(cached, new Int32Rect(
                        (int)x, (int)y, (int)w, (int)h));
                    
                    CroppedImage = croppedBitmap;
                    this.DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi crop ảnh: {ex.Message}");
                this.DialogResult = false;
            }
        }
        else
        {
            this.DialogResult = false;
        }
        
        ScreenshotBuffer.Clear();
        this.Close();
    }
}