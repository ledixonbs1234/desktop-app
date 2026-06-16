using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using desktop_app.Services;
using System.Collections.Generic;
using System.Windows;

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

    public void ResetSession()
    {
        CapturedImage = null;
        HasImage = false;
        UserPrompt = string.Empty;
        AiResponse = "Chưa có phản hồi...";
        _conversationHistory.Clear();
        
        // Force GC để giải phóng BitmapSource cũ và unmanaged resources
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [RelayCommand(CanExecute = nameof(CanSendToAi))]
    private async Task SendToAi()
    {
        if (string.IsNullOrWhiteSpace(UserPrompt) || IsProcessing) return;

        IsProcessing = true;

        // Lưu giữ câu hỏi hiện tại và dọn dẹp ô nhập liệu
        string currentPrompt = UserPrompt;
        UserPrompt = string.Empty;

        // Ghi nhận câu hỏi của người dùng vào giao diện hiển thị
        _conversationHistory.Add($"**Bạn**: {currentPrompt}");
        UpdateResponseView();

        // Chuẩn bị dòng phản hồi trống cho AI
        string aiPrefix = "**AI**: ";
        string currentResponseText = "";

        _conversationHistory.Add($"{aiPrefix}*AI đang suy nghĩ...*");
        UpdateResponseView();
        int activeAiHistoryIndex = _conversationHistory.Count - 1;

        try
        {
            string? base64Image = null;

            if (CapturedImage != null && HasImage)
            {
                base64Image = ImageHelper.ToBase64(CapturedImage);
                CapturedImage = null;
                HasImage = false;
            }

            string finalModel = "unknown";

            // Nhận và cập nhật từng phần phản hồi từ AI
            await foreach (var chunk in _aiService.AskStreamAsync(currentPrompt, base64Image))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    currentResponseText += chunk.Text;
                }
                if (!string.IsNullOrEmpty(chunk.Model))
                {
                    finalModel = chunk.Model;
                }

                // Thực hiện cập nhật UI Thread một cách an toàn thông qua Dispatcher
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _conversationHistory[activeAiHistoryIndex] = $"**AI ({finalModel})**: {currentResponseText}";
                    UpdateResponseView();
                });
            }

            // Ghi nhận lịch sử hoàn chỉnh sau khi stream kết thúc
            ChatHistoryService.Add(new ChatHistoryItem(
                Question: currentPrompt,
                Answer: currentResponseText,
                ImageBase64: base64Image ?? string.Empty,
                Timestamp: DateTime.Now
            ));
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _conversationHistory.Add($"**Hệ thống báo lỗi**: {ex.Message}");
                UpdateResponseView();
            });
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