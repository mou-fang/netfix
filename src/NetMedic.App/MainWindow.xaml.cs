using System.Windows;

namespace NetMedic.App;

/// <summary>
/// 主窗口。DataContext 由 App.OnStartup 注入 MainViewModel。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
