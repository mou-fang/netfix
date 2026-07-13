using System.Windows;

namespace NetMedic.App;

/// <summary>
/// 应用程序入口。阶段 0：显式构造 MainWindow 并注入 MainViewModel。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(),
        };
        mainWindow.Show();
    }
}
