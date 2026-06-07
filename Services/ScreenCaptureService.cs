using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace desktop_app.Services;

public static class ImageHelper
{
    public static string ToBase64(BitmapSource bitmapSource)
    {
        using var memoryStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }

    public static BitmapSource CaptureScreen()
    {
        // Sử dụng GDI+ để chụp toàn bộ màn hình
        using var bmp = new Bitmap(
            System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width,
            System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
        
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);

        // Chuyển đổi từ System.Drawing.Bitmap sang WPF BitmapSource
        var hBitmap = bmp.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
