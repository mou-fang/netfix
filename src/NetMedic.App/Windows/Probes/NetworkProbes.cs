using System.Collections.Immutable;
using System.Net.NetworkInformation;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// NET-01: 活动网卡与链路状态探针。
/// 修正（阶段 2.1）：不简单选取第一张 Up 网卡作为活动网卡。
/// 多张网卡同时活动时保留候选列表，根据默认网关给出主接口；
/// 暂时无法唯一确定时返回"多活动接口"，不随意选择。
/// </summary>
public sealed class AdapterProbe : WindowsProbeBase
{
    public override string Id => "NET-01";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        var upAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();

        evidence["up_adapter_count"] = upAdapters.Count;

        if (upAdapters.Count == 0)
        {
            evidence["adapter_names"] = new List<string>();
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.adapter.none",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        // 收集有默认网关的候选接口
        var candidatesWithGateway = new List<(NetworkInterface adapter, List<string> gateways)>();
        var candidatesNoGateway = new List<string>();

        foreach (var adapter in upAdapters)
        {
            var props = adapter.GetIPProperties();
            var gateways = props.GatewayAddresses
                .Where(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(g => g.Address.ToString())
                .ToList();

            if (gateways.Count > 0)
            {
                candidatesWithGateway.Add((adapter, gateways));
            }
            else
            {
                candidatesNoGateway.Add(adapter.Name);
            }
        }

        evidence["candidates_with_gateway"] = candidatesWithGateway.Select(c => c.adapter.Name).ToList();
        evidence["candidates_without_gateway"] = candidatesNoGateway;
        evidence["adapter_names"] = upAdapters.Select(a => a.Name).ToList();

        if (candidatesWithGateway.Count == 0)
        {
            // 有 Up 网卡但没有一个有网关
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.adapter.no_gateway",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        if (candidatesWithGateway.Count == 1)
        {
            // 唯一有网关的接口 = 主接口
            var primary = candidatesWithGateway[0];
            evidence["primary_adapter"] = primary.adapter.Name;
            evidence["primary_gateways"] = primary.gateways;
            return Task.FromResult(ProbeResult.Pass(this.Id, "probe.net.adapter.ok",
                evidence: evidence.AsReadOnly()));
        }

        // 多个有网关的候选：无法唯一确定主接口
        // 尝试用接口 metric 选最低（最优）的
        var sorted = candidatesWithGateway
            .OrderBy(c => c.adapter.GetIPProperties().GetIPv4Properties().Index)
            .ToList();

        // 检查 metric 差异是否显著
        // 如果无法可靠区分，返回 Warning + "多活动接口"
        evidence["multiple_gateway_adapters"] = sorted.Select(c => c.adapter.Name).ToList();
        evidence["primary_adapter"] = "ambiguous";

        return Task.FromResult(ProbeResult.Fail(this.Id, "probe.net.adapter.multiple_active",
            evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium));
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
                if (gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    gateways.Add(gw.Address.ToString());
                }
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
