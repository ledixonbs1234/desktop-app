using System.Windows.Media.Imaging;

namespace desktop_app.Services;

public static class ScreenshotBuffer
{
    private static BitmapSource? _cachedScreenshot;

    /// <summary>
    /// Chụp màn hình và lưu vào bộ đệm. Phải gọi TRƯỚC khi hiện Overlay.
    /// </summary>
    public static void CaptureAndCache()
    {
        _cachedScreenshot = ImageHelper.CaptureScreen();
    }

    /// <summary>
    /// Lấy ảnh đã chụp từ bộ đệm.
    /// </summary>
    public static BitmapSource? GetCachedScreenshot()
    {
        return _cachedScreenshot;
    }

    /// <summary>
    /// Xóa bộ đệm sau khi crop xong để giải phóng bộ nhớ.
    /// </summary>
    public static void Clear()
    {
        _cachedScreenshot = null;
    }
}
