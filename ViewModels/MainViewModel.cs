using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using desktop_app.Services;
using System.Collections.Generic;

namespace desktop_app.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAiService _aiService;
    private readonly List<string> _conversationHistory = new();

    [ObservableProperty]
    private BitmapSource? _capturedImage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendToAiCommand))]
    private string _userPrompt = string.Empty;

    [ObservableProperty]
    private string _aiResponse = "Chưa có phản hồi...";

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendToAiCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPinned;

    public MainViewModel(IAiService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Làm mới toàn bộ UI và lịch sử hội thoại cục bộ
    /// </summary>
    public void ResetSession()
    {
        CapturedImage = null;
        HasImage = false;
        UserPrompt = string.Empty;
        AiResponse = "Chưa có phản hồi...";
        _conversationHistory.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanSendToAi))]
    private async Task SendToAi()
    {
        if (string.IsNullOrWhiteSpace(UserPrompt) || IsProcessing) return;

        IsProcessing = true;

        // Ghi nhận câu hỏi của người dùng vào giao diện hiển thị
        _conversationHistory.Add($"**Bạn**: {UserPrompt}");
        UpdateResponseView();

        // Tạo chỉ thị chờ đợi phản quan trực quan
        AiResponse += "\n\n---\n\n*AI đang suy nghĩ...*";

        try
        {
            string? base64Image = null;

            // Chỉ gửi ảnh nếu ảnh tồn tại và chưa từng được gửi trước đó
            if (CapturedImage != null && HasImage)
            {
                base64Image = ImageHelper.ToBase64(CapturedImage);

                // Giải phóng vùng xem ảnh sau lượt gửi đầu tiên để chuyển hẳn sang Text Chat liên tục
                CapturedImage = null;
                HasImage = false;
            }

            var response = await _aiService.AskAsync(UserPrompt, base64Image);

            // Ghi nhận câu trả lời của AI vào giao diện hội thoại
            _conversationHistory.Add($"**AI ({response.Model})**: {response.Answer}");
            UpdateResponseView();

            // Lưu vào cơ sở dữ liệu lịch sử chung của hệ thống
            ChatHistoryService.Add(new ChatHistoryItem(
                Question: UserPrompt,
                Answer: response.Answer,
                ImageBase64: base64Image ?? string.Empty,
                Timestamp: DateTime.Now
            ));

            // Xóa sạch ô nhập liệu để sẵn sàng cho câu hỏi tiếp theo
            UserPrompt = string.Empty;
        }
        catch (Exception ex)
        {
            _conversationHistory.Add($"**Hệ thống báo lỗi**: {ex.Message}");
            UpdateResponseView();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private bool CanSendToAi() => !string.IsNullOrWhiteSpace(UserPrompt) && !IsProcessing;

    private void UpdateResponseView()
    {
        AiResponse = string.Join("\n\n---\n\n", _conversationHistory);
    }
}