using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace desktop_app.Services;

public class HotkeyConfig
{
    private const string ConfigPath = "hotkey_config.json";
    
    // Phím tắt bàn phím (mặc định null)
    public Keys? KeyboardKey { get; set; } = null;
    
    // Nút chuột kích hoạt (mặc định Middle)
    public MouseButtons MouseButton { get; set; } = MouseButtons.Middle;

    public static HotkeyConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<HotkeyConfig>(json) ?? new HotkeyConfig();
        }
        return new HotkeyConfig();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
