using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Condition = System.Windows.Automation.Condition;

namespace desktop_app.Services
{
    public class AutomationServer
    {
        private HttpListener? _listener;
        private bool _isRunning;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:54322/");
                _listener.Start();
                _isRunning = true;
                Task.Run(ResponseLoop);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Không thể khởi động Automation Server: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
            }
            catch { }
        }

        private async Task ResponseLoop()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch
                {
                    // Thầm lặng bỏ qua khi listener dừng lại
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Cấu hình CORS Headers
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            try
            {
                var path = request.Url?.AbsolutePath.ToLower();
                if (path == "/tree")
                {
                    IntPtr hwnd = GetForegroundWindow();
                    IntPtr helperHwnd = IntPtr.Zero;

                    // KHẮC PHỤC LỖI: Truy xuất MainWindow thông qua Dispatcher để chuyển tiếp tác vụ về UI STA Thread an toàn
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (Application.Current.MainWindow != null)
                            {
                                helperHwnd = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Dispatcher Warning]: {ex.Message}");
                        }
                    });

                    // Tránh lấy cây giao diện của chính Helper App nếu cửa sổ đó đang hoạt động
                    if (hwnd == helperHwnd && hwnd != IntPtr.Zero)
                    {
                        // Có thể bỏ qua quét hoặc trả về thông báo trạng thái rỗng
                    }

                    string windowTitle = "Cửa sổ không xác định";
                    var elements = new List<UiElementDto>();
                    string screenshotBase64 = "";

                    if (hwnd != IntPtr.Zero)
                    {
                        try
                        {
                            screenshotBase64 = CaptureWindow(hwnd);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CaptureWindow Warning]: {ex.Message}");
                        }

                        await Task.Run(() =>
                        {
                            try
                            {
                                AutomationElement root = AutomationElement.FromHandle(hwnd);
                                windowTitle = root.Current.Name ?? "Cửa sổ mục tiêu";
                                WalkUiaTree(root, elements, 0, 5); // Độ sâu quét tối đa: 5 tầng UI
                            }
                            catch (Exception ex)
                            {
                                windowTitle = $"Lỗi đọc UIA: {ex.Message}";
                            }
                        });
                    }

                    var result = new
                    {
                        success = true,
                        windowTitle,
                        tree = elements,
                        screenshot = screenshotBase64
                    };

                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    response.ContentType = "application/json; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (path == "/action" && request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(request.InputStream);
                    var body = await reader.ReadToEndAsync();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var actionReq = JsonSerializer.Deserialize<ActionRequestDto>(body, options);

                    bool success = false;
                    string message = "Không tìm thấy Element đích trên ứng dụng đang gỡ lỗi.";

                    IntPtr hwnd = GetForegroundWindow();
                    if (actionReq != null && hwnd != IntPtr.Zero)
                    {
                        await Task.Run(() =>
                        {
                            try
                            {
                                AutomationElement root = AutomationElement.FromHandle(hwnd);
                                AutomationElement? targetElement = FindUiaElement(root, actionReq.Target);

                                if (targetElement != null)
                                {
                                    if (actionReq.Action == "click")
                                    {
                                        if (targetElement.TryGetCurrentPattern(InvokePattern.Pattern, out object invokePattern))
                                        {
                                            ((InvokePattern)invokePattern).Invoke();
                                            success = true;
                                            message = $"Đã gọi InvokePattern thành công trên '{actionReq.Target}'";
                                        }
                                        else
                                        {
                                            var rect = targetElement.Current.BoundingRectangle;
                                            if (rect != System.Windows.Rect.Empty)
                                            {
                                                int clickX = (int)(rect.Left + rect.Width / 2);
                                                int clickY = (int)(rect.Top + rect.Height / 2);
                                                SimulateMouseClick(clickX, clickY);
                                                success = true;
                                                message = $"Đã giả lập click chuột tại tọa độ ({clickX}, {clickY}) trên '{actionReq.Target}'";
                                            }
                                        }
                                    }
                                    else if (actionReq.Action == "setText")
                                    {
                                        if (targetElement.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern))
                                        {
                                            ((ValuePattern)valuePattern).SetValue(actionReq.Value ?? "");
                                            success = true;
                                            message = $"Đã điền text vào '{actionReq.Target}' bằng ValuePattern.";
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                message = $"Lỗi tương tác UIA: {ex.Message}";
                            }
                        });
                    }

                    var result = new { success, message };
                    var json = JsonSerializer.Serialize(result);
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    response.ContentType = "application/json; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutomationServer Exception]: {ex}");
                Console.WriteLine($"[AutomationServer Exception]: {ex}");

                response.StatusCode = (int)HttpStatusCode.InternalServerError;

                string errorMsg = $"[AutomationServer Error]: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    errorMsg += $"\nInner Exception: {ex.InnerException.Message}\nInner Stack Trace:\n{ex.InnerException.StackTrace}";
                }

                byte[] buffer = Encoding.UTF8.GetBytes(errorMsg);
                response.ContentType = "text/plain; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private static void WalkUiaTree(AutomationElement element, List<UiElementDto> elements, int currentDepth, int maxDepth)
        {
            if (element == null || currentDepth > maxDepth) return;

            try
            {
                var current = element.Current;
                var rect = current.BoundingRectangle;
                string rectStr = rect != System.Windows.Rect.Empty ? $"{(int)rect.X},{(int)rect.Y},{(int)rect.Width},{(int)rect.Height}" : "0,0,0,0";

                elements.Add(new UiElementDto(
                    Name: current.Name ?? "",
                    Type: current.ControlType?.LocalizedControlType ?? "CustomControl",
                    AutomationId: current.AutomationId ?? "",
                    IsEnabled: current.IsEnabled,
                    BoundingRect: rectStr
                ));

                AutomationElementCollection children = element.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);
                foreach (AutomationElement child in children)
                {
                    WalkUiaTree(child, elements, currentDepth + 1, maxDepth);
                }
            }
            catch
            {
                // Thầm lặng bỏ qua
            }
        }

        private static AutomationElement? FindUiaElement(AutomationElement parent, string selector)
        {
            if (parent == null) return null;

            try
            {
                if (parent.Current.AutomationId == selector || parent.Current.Name == selector)
                {
                    return parent;
                }

                AutomationElementCollection children = parent.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement child in children)
                {
                    var found = FindUiaElement(child, selector);
                    if (found != null) return found;
                }
            }
            catch { }
            return null;
        }

        private static void SimulateMouseClick(int x, int y)
        {
            SetCursorPos(x, y);
            System.Threading.Thread.Sleep(80);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            System.Threading.Thread.Sleep(80);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        private static string CaptureWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            try
            {
                GetWindowRect(hwnd, out RECT rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0) return "";

                using (var bmp = new System.Drawing.Bitmap(width, height))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
                    }
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                return "";
            }
        }
    }

    public record UiElementDto(string Name, string Type, string AutomationId, bool IsEnabled, string BoundingRect);
    public class ActionRequestDto
    {
        public string Target { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Value { get; set; }
    }
}