using System.Collections.ObjectModel;
using System.IO;
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
                foreach (var item in items)
                    _history.Add(item);
            }
        }
    }

    public static void Add(ChatHistoryItem item)
    {
        _history.Insert(0, item);
        Save();
    }

    private static void Save()
    {
        var json = JsonSerializer.Serialize(_history.ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(HistoryPath, json);
    }
}
