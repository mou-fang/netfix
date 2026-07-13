namespace NetMedic.Core.Testing;

/// <summary>
/// 网卡状态分组。对应任务书 §5.3 NET-01~03。
/// </summary>
public sealed record AdapterState(
    bool HasActiveAdapter,
    bool HasValidIpv4,
    bool HasApiPAAddress,
    bool HasDefaultGateway,
    bool HasDefaultRoute,
    bool VpnActive,
    bool VpnDnsSplit)
{
    public static AdapterState Healthy => new(
        true, true, false, true, true, false, false);
}

/// <summary>
/// DNS 状态分组。对应任务书 §5.3 DNS-01~02。
/// </summary>
public sealed record DnsState(
    bool Configured,
    bool SystemResolves,
    bool ConfiguredDnsReachable)
{
    public static DnsState Healthy => new(true, true, true);
}

/// <summary>
/// 代理状态分组。对应任务书 §5.3 PRX-01~04。
/// WinINET、WinHTTP、PAC 和本地端口分别记录，不混为一个状态。
/// </summary>
public sealed record ProxyState(
    bool WininetEnabled,
    string? WininetAddress,
    bool WininetIsLoopback,
    bool WininetPortListening,
    bool WinhttpEnabled,
    bool WinhttpReachable,
    bool PacEnabled,
    bool PacReachable)
{
    public static ProxyState Direct => new(
        false, null, false, false, false, false, false, false);
}

/// <summary>
/// Web 状态分组。对应任务书 §5.3 WEB-01~04。
/// </summary>
public sealed record WebState(
    bool NcsiConnected,
    bool DirectHttpsOk,
    bool SystemProxyHttpsOk,
    bool CaptivePortalDetected,
    bool TargetSiteResolves,
    bool TargetSiteConnects,
    bool Ipv4Ok,
    bool Ipv6Ok)
{
    public static WebState Healthy => new(
        true, true, true, false, true, true, true, true);
}

/// <summary>
/// 系统上下文状态分组。对应任务书 §5.3 SYS-01。
/// </summary>
public sealed record SystemState(
    bool IsRdpSession,
    bool IsManagedEnvironment)
{
    public static SystemState Normal => new(false, false);
}
