using System.Windows;

namespace NetMedic.App;

/// <summary>
/// 应用程序入口。
/// 阶段 0/1：显式构造 MainWindow 并注入 MainViewModel。
/// 阶段 2：支持 --verify-probes 命令行模式，运行真实探针验证。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 阶段 2 验证模式：--verify-probes [target_host]
        if (e.Args.Length > 0 && e.Args[0] == "--verify-probes")
        {
            string? target = e.Args.Length > 1 ? e.Args[1] : null;
            _ = NetMedic.App.Windows.ProbeVerificationRunner.RunAsync(target).ContinueWith(_ =>
            {
                Shutdown(0);
            });
            return;
        }

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(),
        };
        mainWindow.Show();
    }
}
