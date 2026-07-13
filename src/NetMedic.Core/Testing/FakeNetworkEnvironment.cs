using System.Collections.Immutable;
using NetMedic.Core.Abstractions;

namespace NetMedic.Core.Testing;

/// <summary>
/// 模拟网络环境。对应任务书 §11.1 FakeNetworkEnvironment。
/// 阶段 2 起使用分组对象（AdapterState/DnsState/ProxyState/WebState/SystemState）。
/// 旧布尔字段保留为委托属性，确保现有测试兼容（ADR-012）。
/// </summary>
public sealed record FakeNetworkEnvironment(
    string Name,
    SystemState System,
    AdapterState Adapter,
    DnsState Dns,
    ProxyState Proxy,
    WebState Web)
    : INetworkEnvironment
{
    /// <summary>
    /// 从旧版 30 布尔字段构造（向后兼容，供现有 fixture 使用）。
    /// </summary>
    public FakeNetworkEnvironment(
        string Name,
        bool HasActiveAdapter, bool HasValidIpv4, bool HasApiPAAddress,
        bool HasDefaultGateway, bool HasDefaultRoute,
        bool DnsConfigured, bool SystemDnsResolves, bool ConfiguredDnsReachable,
        bool WininetProxyEnabled, string? WininetProxyAddress,
        bool WininetProxyIsLoopback, bool WininetProxyPortListening,
        bool WinhttpProxyEnabled, bool WinhttpProxyReachable,
        bool PacEnabled, bool PacReachable,
        bool NcsiConnected, bool DirectHttpsOk, bool SystemProxyHttpsOk,
        bool CaptivePortalDetected,
        bool VpnActive, bool VpnDnsSplit,
        bool IsManagedEnvironment, bool IsRdpSession,
        bool TargetSiteResolves, bool TargetSiteConnects,
        bool Ipv4Ok, bool Ipv6Ok)
        : this(
            Name,
            System: new SystemState(IsRdpSession, IsManagedEnvironment),
            Adapter: new AdapterState(HasActiveAdapter, HasValidIpv4, HasApiPAAddress,
                HasDefaultGateway, HasDefaultRoute, VpnActive, VpnDnsSplit),
            Dns: new DnsState(DnsConfigured, SystemDnsResolves, ConfiguredDnsReachable),
            Proxy: new ProxyState(WininetProxyEnabled, WininetProxyAddress,
                WininetProxyIsLoopback, WininetProxyPortListening,
                WinhttpProxyEnabled, WinhttpProxyReachable,
                PacEnabled, PacReachable),
            Web: new WebState(NcsiConnected, DirectHttpsOk, SystemProxyHttpsOk,
                CaptivePortalDetected, TargetSiteResolves, TargetSiteConnects,
                Ipv4Ok, Ipv6Ok))
    {
    }

    /// <summary>创建一个完全健康的网络环境。</summary>
    public static FakeNetworkEnvironment Healthy(string name = "Healthy") => new(
        Name: name,
        System: SystemState.Normal,
        Adapter: AdapterState.Healthy,
        Dns: DnsState.Healthy,
        Proxy: ProxyState.Direct,
        Web: WebState.Healthy);

    // --- 向后兼容委托属性（映射到分组对象） ---

    public bool IsRdpSession => System.IsRdpSession;
    public bool IsManagedEnvironment => System.IsManagedEnvironment;

    public bool HasActiveAdapter => Adapter.HasActiveAdapter;
    public bool HasValidIpv4 => Adapter.HasValidIpv4;
    public bool HasApiPAAddress => Adapter.HasApiPAAddress;
    public bool HasDefaultGateway => Adapter.HasDefaultGateway;
    public bool HasDefaultRoute => Adapter.HasDefaultRoute;
    public bool VpnActive => Adapter.VpnActive;
    public bool VpnDnsSplit => Adapter.VpnDnsSplit;

    public bool DnsConfigured => Dns.Configured;
    public bool SystemDnsResolves => Dns.SystemResolves;
    public bool ConfiguredDnsReachable => Dns.ConfiguredDnsReachable;

    public bool WininetProxyEnabled => Proxy.WininetEnabled;
    public string? WininetProxyAddress => Proxy.WininetAddress;
    public bool WininetProxyIsLoopback => Proxy.WininetIsLoopback;
    public bool WininetProxyPortListening => Proxy.WininetPortListening;
    public bool WinhttpProxyEnabled => Proxy.WinhttpEnabled;
    public bool WinhttpProxyReachable => Proxy.WinhttpReachable;
    public bool PacEnabled => Proxy.PacEnabled;
    public bool PacReachable => Proxy.PacReachable;

    public bool NcsiConnected => Web.NcsiConnected;
    public bool DirectHttpsOk => Web.DirectHttpsOk;
    public bool SystemProxyHttpsOk => Web.SystemProxyHttpsOk;
    public bool CaptivePortalDetected => Web.CaptivePortalDetected;
    public bool TargetSiteResolves => Web.TargetSiteResolves;
    public bool TargetSiteConnects => Web.TargetSiteConnects;
    public bool Ipv4Ok => Web.Ipv4Ok;
    public bool Ipv6Ok => Web.Ipv6Ok;
}
