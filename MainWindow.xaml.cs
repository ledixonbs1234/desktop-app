using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace desktop_app;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Loại bỏ logic reset session khi Pin để giữ nguyên ảnh và nội dung chat.
        // Chức năng Pin chỉ dùng để ngăn ứng dụng tự ẩn khi mất focus.
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void PromptTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Nếu nhấn Shift + Enter thì cho phép xuống hàng (mặc định của TextBox khi AcceptsReturn=True)
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                return;
            }

            // Các trường hợp khác nhấn Enter thì gửi tin nhắn
            e.Handled = true;

            if (DataContext is ViewModels.MainViewModel vm && vm.SendToAiCommand.CanExecute(null))
            {
                vm.SendToAiCommand.Execute(null);
            }
        }
    }

    private void MarkdownViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResponseScrollViewer.ScrollToEnd();
    }

    private void ResponseScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            double scrollChange = e.Delta > 0 ? -48 : 48;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollChange);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Xử lý điền nhanh văn bản mẫu khi click phím tiện ích dưới Chatbox
    /// </summary>
    private void QuickButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string quickText)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.UserPrompt = quickText;

                // Trả con trỏ soạn thảo về TextBox để người dùng có thể chỉnh sửa thêm hoặc nhấn Enter gửi luôn
                PromptTextBox.Focus();
                PromptTextBox.CaretIndex = PromptTextBox.Text.Length;
            }
        }
    }
}