using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using desktop_app.Services;

namespace desktop_app.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAiService _aiService;

    [ObservableProperty]
    private BitmapSource? _capturedImage;

    [ObservableProperty]
    private string _userPrompt = string.Empty;

    [ObservableProperty]
    private string _aiResponse = "Chưa có phản hồi...";

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _isProcessing;

    public MainViewModel(IAiService aiService)
    {
        _aiService = aiService;
    }

    [RelayCommand]
    private async Task SendToAi()
    {
        if (string.IsNullOrWhiteSpace(UserPrompt) || CapturedImage == null) return;

        IsProcessing = true;
        AiResponse = "Đang xử lý...";

        try
        {
            var base64Image = ImageHelper.ToBase64(CapturedImage);
            var response = await _aiService.AskAsync(UserPrompt, base64Image);
            AiResponse = response.Answer;
            
            // Lưu vào lịch sử
            ChatHistoryService.Add(new ChatHistoryItem(
                Question: UserPrompt,
                Answer: response.Answer,
                ImageBase64: base64Image,
                Timestamp: DateTime.Now
            ));
        }
        catch (Exception ex)
        {
            AiResponse = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
