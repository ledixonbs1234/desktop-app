using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using desktop_app.Services;
using desktop_app.ViewModels;
using Application = System.Windows.Application;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace desktop_app;

public partial class App : Application
{
    private IKeyboardMouseEvents? _globalHook;
    private MainWindow? _mainWindow;
    private NotifyIcon? _trayIcon;
    private readonly AiService _aiService = new();
    private bool _isOverlayActive = false;
    private HotkeyConfig _hotkeyConfig = new();
    private readonly AutomationServer _automationServer = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Khởi động server tự động hóa
        _automationServer.Start();

        // Tải cấu hình phím tắt và lịch sử
        _hotkeyConfig = HotkeyConfig.Load();
        ChatHistoryService.Load();

        // Khởi tạo MainWindow
        _mainWindow = new MainWindow();
        _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _mainWindow.DataContext = new MainViewModel(_aiService);

        _mainWindow.Deactivated += async (s, args) =>
        {
            if (!_isOverlayActive)
            {
                if (_mainWindow.DataContext is MainViewModel vm && vm.IsPinned)
                {
                    return;
                }

                _mainWindow.Hide();
                try
                {
                    await _aiService.AskAsync("/clear");
                    if (_mainWindow.DataContext is MainViewModel vmReset)
                    {
                        vmReset.ResetSession();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi dọn dẹp phiên chat: {ex.Message}");
                }
            }
        };

        // Thiết lập khay hệ thống
        SetupTrayIcon();

        // CHẠY KIỂM TRA LIÊN KẾT BACKEND KHÔNG GÂY TREO UI THREAD
        _ = VerifyBackendConnectionAsync();

        _globalHook = Hook.GlobalEvents();
        _globalHook.MouseDownExt += GlobalHook_MouseDownExt;
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
            Visible = true,
            Text = "AI Desktop Assistant"
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("Hiện cửa sổ", null, (s, e) =>
        {
            CenterAndShowMainWindow();
        }));
        contextMenu.Items.Add(new ToolStripMenuItem("Thoát", null, (s, e) =>
        {
            _trayIcon?.Dispose();
            Shutdown();
        }));
        _trayIcon.ContextMenuStrip = contextMenu;
    }

    // PHƯƠNG THỨC GỬI THÔNG BÁO BALLOON KHI KẾT NỐI / LỖI KẾT NỐI
    private async Task VerifyBackendConnectionAsync()
    {
        // Chờ 500ms để đảm bảo các tiến trình nền hoạt động ổn định
        await Task.Delay(500);

        bool isConnected = await _aiService.CheckConnectionAsync();

        if (isConnected)
        {
            _trayIcon?.ShowBalloonTip(
                3000, // Thời gian hiển thị (ms)
                "AI Desktop Assistant",
                "Đã liên kết thành công với Bridge Server tại cổng 54321!",
                ToolTipIcon.Info
            );
        }
        else
        {
            _trayIcon?.ShowBalloonTip(
                5000,
                "Lỗi Kết Nối Backend",
                "Không thể kết nối tới Bridge Server. Hãy chắc chắn rằng Node.js backend đang chạy ở cổng 54321.",
                ToolTipIcon.Warning
            );
        }
    }

    private void CenterAndShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.Focus();
        }
    }

    private void GlobalHook_MouseDownExt(object? sender, MouseEventExtArgs e)
    {
        bool isMouseTrigger = e.Button == _hotkeyConfig.MouseButton;
        bool isKeyboardTrigger = false;
        if (_hotkeyConfig.KeyboardKey.HasValue && _hotkeyConfig.KeyboardKey.Value != Keys.None)
        {
            isKeyboardTrigger = Keyboard.IsKeyDown(KeyInterop.KeyFromVirtualKey((int)_hotkeyConfig.KeyboardKey.Value));
        }

        bool isDefaultCombo = !isMouseTrigger && !isKeyboardTrigger &&
            Keyboard.IsKeyDown(Key.LeftCtrl) &&
            Keyboard.IsKeyDown(Key.LeftShift) &&
            e.Button == MouseButtons.Left;

        if (isMouseTrigger || isKeyboardTrigger || isDefaultCombo)
        {
            if (_isOverlayActive) return;

            e.Handled = true;
            _isOverlayActive = true;

            try
            {
                ScreenshotBuffer.CaptureAndCache();
                _mainWindow?.Hide();
                var overlay = new CropOverlayWindow();
                bool? result = overlay.ShowDialog();

                if (result == true && overlay.CroppedImage != null && _mainWindow != null)
                {
                    if (_mainWindow.DataContext is MainViewModel vm)
                    {
                        vm.CapturedImage = overlay.CroppedImage;
                        vm.HasImage = true;
                        vm.AiResponse = "Đã chụp vùng chọn. Hãy đặt câu hỏi!";
                    }
                    CenterAndShowMainWindow();
                }
            }
            finally
            {
                _isOverlayActive = false;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _automationServer.Stop();
        if (_globalHook != null)
        {
            _globalHook.MouseDownExt -= GlobalHook_MouseDownExt;
            _globalHook.Dispose();
        }
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}