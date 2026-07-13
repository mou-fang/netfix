using System.Collections.Immutable;
using System.Net.NetworkInformation;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// NET-01: 活动网卡与链路状态探针。
/// 使用 .NET BCL NetworkInterface API（对应任务书 §8.5 优先使用 BCL）。
/// </summary>
public sealed class AdapterProbe : WindowsProbeBase
{
    public override string Id => "NET-01";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        evidence["up_adapter_count"] = adapters.Count;
        evidence["adapter_names"] = adapters.Select(a => a.Name).ToList();

        if (adapters.Count == 0)
        {
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.adapter.none",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        // 检查至少有一个非虚拟网卡或至少有活动连接
        bool hasPhysicalOrConnected = adapters.Any(a =>
            a.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
            a.NetworkInterfaceType != NetworkInterfaceType.Ppp);

        evidence["has_physical_or_connected"] = hasPhysicalOrConnected;

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.net.adapter.ok",
            evidence: evidence.AsReadOnly()));
    }
}

/// <summary>
/// NET-02: IPv4/IPv6 地址与 DHCP/APIPA 探针。
/// </summary>
public sealed class IpAddressProbe : WindowsProbeBase
{
    public override string Id => "NET-02";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        bool hasValidIpv4 = false;
        bool hasApiPA = false;

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var adapter in adapters)
        {
            var props = adapter.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // 检查 APIPA (169.254.x.x)
                    if (IsApiPA(addr.Address))
                    {
                        hasApiPA = true;
                        evidence[$"adapter_{adapter.Name}_apipa"] = addr.Address.ToString();
                    }
                    else
                    {
                        hasValidIpv4 = true;
                    }
                }
            }
        }

        evidence["has_valid_ipv4"] = hasValidIpv4;
        evidence["has_apipa"] = hasApiPA;

        if (hasApiPA && !hasValidIpv4)
        {
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.ip.apipa",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        if (!hasValidIpv4)
        {
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.ip.none",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.net.ip.ok",
            evidence: evidence.AsReadOnly()));
    }

    private static bool IsApiPA(System.Net.IPAddress addr)
    {
        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = addr.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }
}

/// <summary>
/// NET-03: 默认网关与默认路由探针。
/// </summary>
public sealed class GatewayProbe : WindowsProbeBase
{
    public override string Id => "NET-03";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        var gateways = new List<string>();

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var adapter in adapters)
        {
            var props = adapter.GetIPProperties();
            foreach (var gw in props.GatewayAddresses)
            {
                gateways.Add(gw.Address.ToString());
            }
        }

        evidence["gateway_count"] = gateways.Count;
        evidence["gateways"] = gateways;

        if (gateways.Count == 0)
        {
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.gateway.none",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.net.gateway.ok",
            evidence: evidence.AsReadOnly()));
    }
}
