using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace desktop_app.Services;

public record ChatHistoryItem(
    string Question,
    string Answer,
    string ImageBase64,
    DateTime Timestamp
);

public static class ChatHistoryService
{
    private const string HistoryPath = "chat_history.json";
    private const int MaxHistoryItems = 100; // Giới hạn số lượng lịch sử để tránh tăng RAM
    private static readonly ObservableCollection<ChatHistoryItem> _history = new();

    public static ObservableCollection<ChatHistoryItem> History => _history;

    public static void Load()
    {
        _history.Clear();
        if (File.Exists(HistoryPath))
        {
            var json = File.ReadAllText(HistoryPath);
            var items = JsonSerializer.Deserialize<List<ChatHistoryItem>>(json);
            if (items != null)
            {
                // File JSON lưu theo thứ tự [Mới nhất, ..., Cũ nhất]
                // Chỉ load tối đa MaxHistoryItems mục mới nhất (từ đầu danh sách)
                var recentItems = items.Take(MaxHistoryItems).ToList();
                foreach (var item in recentItems)
                    _history.Add(item);
            }
        }
    }

    public static void Add(ChatHistoryItem item)
    {
        _history.Insert(0, item);
        
        // Xóa bớt các items cũ nếu vượt quá giới hạn
        while (_history.Count > MaxHistoryItems)
        {
            _history.RemoveAt(_history.Count - 1);
        }
        
        Save();
    }

    private static void Save()
    {
        var json = JsonSerializer.Serialize(_history.ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(HistoryPath, json);
    }
}
