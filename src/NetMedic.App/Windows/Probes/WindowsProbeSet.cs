using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// Windows 快速体检探针集构建器。
/// 对应任务书 §5.3 快速探针目录 SYS-01, NET-01~03, DNS-01~02, PRX-01~04, WEB-01~04, TARGET-01。
/// 所有探针只读、非管理员可运行。
/// </summary>
public static class WindowsProbeSet
{
    /// <summary>
    /// 构建快速体检探针列表。
    /// </summary>
    /// <param name="targetHost">用户指定目标主机（可选，为 null 时跳过 TARGET-01）。</param>
    public static IReadOnlyList<IProbe> BuildQuick(string? targetHost = null)
    {
        var probes = new List<IProbe>
        {
            new SystemContextProbe(),
            new AdapterProbe(),
            new IpAddressProbe(),
            new GatewayProbe(),
            new DnsConfigProbe(),
            new DnsResolveProbe(),
            new WininetProxyProbe(),
            new WinhttpProxyProbe(),
            new PacProxyProbe(),
            new LocalProxyPortProbe(),
            new NcsiProbe(),
            new DirectHttpsProbe(),
            new SystemProxyHttpsProbe(),
            new CaptivePortalProbe(),
        };

        // TARGET-01 仅在用户提供目标时添加
        if (!string.IsNullOrWhiteSpace(targetHost))
        {
            probes.Add(new TargetProbe(targetHost));
        }

        return probes;
    }
}
