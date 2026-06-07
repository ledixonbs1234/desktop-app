using System.Windows;
using System.Windows.Input;

namespace desktop_app;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Không chặn sự kiện Closing nữa để có thể thoát ứng dụng bình thường
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Đóng ứng dụng hoàn toàn
        Application.Current.Shutdown();
    }
}
