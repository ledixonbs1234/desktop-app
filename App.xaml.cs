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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Tải cấu hình phím tắt và lịch sử
        _hotkeyConfig = HotkeyConfig.Load();
        ChatHistoryService.Load();

        // Khởi tạo MainWindow (ẩn, căn giữa màn hình)
        _mainWindow = new MainWindow();
        _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _mainWindow.DataContext = new MainViewModel(_aiService);

        // Tự động làm mới phiên chat Qwen Web khi bị ẩn đi
        _mainWindow.Deactivated += async (s, args) =>
        {
            if (!_isOverlayActive)
            {
                // Kiểm tra trạng thái Pin
                if (_mainWindow.DataContext is MainViewModel vm && vm.IsPinned)
                {
                    return; // Nếu đang ghim, không làm gì cả
                }

                _mainWindow.Hide();
                try
                {
                    // Gửi chỉ thị dọn dẹp bối cảnh trình duyệt
                    await _aiService.AskAsync("/clear");

                    // Đưa giao diện Desktop về trạng thái ban đầu
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

        // Setup System Tray Icon
        SetupTrayIcon();

        // Đăng ký Global Hook
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
        // Kiểm tra nút chuột từ config
        bool isMouseTrigger = e.Button == _hotkeyConfig.MouseButton;
        
        // Kiểm tra phím bàn phím từ config (nếu có)
        bool isKeyboardTrigger = false;
        if (_hotkeyConfig.KeyboardKey.HasValue && _hotkeyConfig.KeyboardKey.Value != Keys.None)
        {
            isKeyboardTrigger = Keyboard.IsKeyDown(KeyInterop.KeyFromVirtualKey((int)_hotkeyConfig.KeyboardKey.Value));
        }

        // Hỗ trợ thêm phím Ctrl+Shift+A làm trigger mặc định phụ
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
                // CHỤP ẢNH NỀN TRƯỚC KHI HIỆN OVERLAY
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
                    
                    // Hiển thị và căn giữa màn hình
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
        if (_globalHook != null)
        {
            _globalHook.MouseDownExt -= GlobalHook_MouseDownExt;
            _globalHook.Dispose();
        }
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
