using NetMedic.Core.Abstractions;

namespace NetMedic.App.Windows;

/// <summary>
/// 真实 Windows 网络环境。实现 INetworkEnvironment。
/// 阶段 2：真实探针从此读取系统状态。
/// </summary>
public sealed class WindowsNetworkEnvironment : INetworkEnvironment
{
    public string Name => "WindowsReal";
}
