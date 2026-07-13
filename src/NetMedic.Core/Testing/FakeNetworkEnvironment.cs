using System.Collections.Immutable;
using NetMedic.Core.Abstractions;

namespace NetMedic.Core.Testing;

/// <summary>
/// 模拟网络环境。对应任务书 §11.1 FakeNetworkEnvironment。
/// 描述网卡、IP、网关、DNS、代理、VPN 等状态，供 Fake 探针读取。
/// 测试用此构造故障场景，无需真的改开发机网络。
/// </summary>
public sealed record FakeNetworkEnvironment(
    string Name,
    bool HasActiveAdapter,
    bool HasValidIpv4,
    bool HasApiPAAddress,
    bool HasDefaultGateway,
    bool HasDefaultRoute,
    bool DnsConfigured,
    bool SystemDnsResolves,
    bool ConfiguredDnsReachable,
    bool WininetProxyEnabled,
    string? WininetProxyAddress,
    bool WininetProxyIsLoopback,
    bool WininetProxyPortListening,
    bool WinhttpProxyEnabled,
    bool WinhttpProxyReachable,
    bool PacEnabled,
    bool PacReachable,
    bool NcsiConnected,
    bool DirectHttpsOk,
    bool SystemProxyHttpsOk,
    bool CaptivePortalDetected,
    bool VpnActive,
    bool VpnDnsSplit,
    bool IsManagedEnvironment,
    bool IsRdpSession,
    bool TargetSiteResolves,
    bool TargetSiteConnects,
    bool Ipv4Ok,
    bool Ipv6Ok)
    : INetworkEnvironment
{
    /// <summary>创建一个完全健康的网络环境。</summary>
    public static FakeNetworkEnvironment Healthy(string name = "Healthy") => new(
        Name: name,
        HasActiveAdapter: true,
        HasValidIpv4: true,
        HasApiPAAddress: false,
        HasDefaultGateway: true,
        HasDefaultRoute: true,
        DnsConfigured: true,
        SystemDnsResolves: true,
        ConfiguredDnsReachable: true,
        WininetProxyEnabled: false,
        WininetProxyAddress: null,
        WininetProxyIsLoopback: false,
        WininetProxyPortListening: false,
        WinhttpProxyEnabled: false,
        WinhttpProxyReachable: false,
        PacEnabled: false,
        PacReachable: false,
        NcsiConnected: true,
        DirectHttpsOk: true,
        SystemProxyHttpsOk: true,
        CaptivePortalDetected: false,
        VpnActive: false,
        VpnDnsSplit: false,
        IsManagedEnvironment: false,
        IsRdpSession: false,
        TargetSiteResolves: true,
        TargetSiteConnects: true,
        Ipv4Ok: true,
        Ipv6Ok: true);
}
