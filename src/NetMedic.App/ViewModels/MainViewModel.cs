using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetMedic.Core;

namespace NetMedic.App;

/// <summary>
/// 主窗口视图模型。阶段 0：仅展示应用信息与占位按钮，不做真实诊断。
/// 对应任务书 §4.2 首页的最早骨架；症状选择器与诊断流程留待阶段 1。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        this.appName = CoreMarker.ProductName;
        this.appVersion = $"v{CoreMarker.Version}";
        this.readyState = "已就绪";
        this.statusMessage = string.Empty;
    }

    [ObservableProperty]
    private string appName;

    [ObservableProperty]
    private string appVersion;

    [ObservableProperty]
    private string readyState;

    [ObservableProperty]
    private string statusMessage;

    /// <summary>
    /// 占位命令：点击“开始体检”后提示功能将在后续阶段实现。
    /// 阶段 1 起替换为真实诊断编排调用。
    /// </summary>
    [RelayCommand]
    private void StartCheckup()
    {
        this.StatusMessage = "功能将在后续阶段实现。";
    }
}
