using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;

namespace NetMedic.Core.Testing;

/// <summary>
/// 场景 fixture 工厂。对应任务书 §11.2 必测场景 L01/L02/L09/L14/L15。
/// 每个场景返回一个完整的 FakeNetworkEnvironment + 预期 Finding。
/// 阶段 1 只覆盖这五个场景，保证结果稳定可重复。
/// </summary>
public static class ScenarioFixtures
{
    /// <summary>
    /// L01: 健康网络，无代理/VPN。预期：显示正常，不修改设置。
    /// </summary>
    public static ScenarioFixture L01_Healthy() => new(
        Name: "L01_Healthy",
        Environment: FakeNetworkEnvironment.Healthy("L01_Healthy"),
        ExpectedFindingId: "finding.network_healthy",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>
    /// L02: 本地代理 127.0.0.1:7890 无监听，直连成功。预期：建议关闭失效代理。
    /// </summary>
    public static ScenarioFixture L02_DeadLocalProxy() => new(
        Name: "L02_DeadLocalProxy",
        Environment: FakeNetworkEnvironment.Healthy("L02_DeadLocalProxy") with
        {
            Proxy = ProxyState.Direct with
            {
                WininetEnabled = true,
                WininetAddress = "127.0.0.1:7890",
                WininetIsLoopback = true,
                WininetPortListening = false,
            },
            Web = WebState.Healthy with { SystemProxyHttpsOk = false },
        },
        ExpectedFindingId: "finding.dead_local_proxy",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: "FIX-PRX-01");

    /// <summary>
    /// L09: DNS 服务器不可达。预期：归类 DNS 故障。
    /// 网关/路由正常，直连 TCP 正常，但系统 DNS 解析失败。
    /// </summary>
    public static ScenarioFixture L09_DnsFailure() => new(
        Name: "L09_DnsFailure",
        Environment: FakeNetworkEnvironment.Healthy("L09_DnsFailure") with
        {
            Dns = DnsState.Healthy with
            {
                SystemResolves = false,
                ConfiguredDnsReachable = false,
            },
            Web = WebState.Healthy with
            {
                DirectHttpsOk = false,
                TargetSiteResolves = false,
                TargetSiteConnects = false,
            },
        },
        ExpectedFindingId: "finding.dns_failure",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: "FIX-DNS-01");

    /// <summary>
    /// L14: NCSI 报无网但 HTTPS 正常。预期：说明图标异常，不修复网络。
    /// </summary>
    public static ScenarioFixture L14_NcsiMismatch() => new(
        Name: "L14_NcsiMismatch",
        Environment: FakeNetworkEnvironment.Healthy("L14_NcsiMismatch") with
        {
            Web = WebState.Healthy with { NcsiConnected = false },
        },
        ExpectedFindingId: "finding.ncsi_mismatch",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>
    /// L15: 只有一个网站失败。预期：不执行全局重置。
    /// 健康目标正常，只有用户指定目标失败。
    /// TargetUnreachableRule 命中（合并 SingleSiteIssue 和 ExternalService）。
    /// </summary>
    public static ScenarioFixture L15_SingleSiteIssue() => new(
        Name: "L15_SingleSiteIssue",
        Environment: FakeNetworkEnvironment.Healthy("L15_SingleSiteIssue") with
        {
            Web = WebState.Healthy with
            {
                TargetSiteResolves = false,
                TargetSiteConnects = false,
            },
        },
        ExpectedFindingId: "finding.target_unreachable",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>所有场景，便于测试遍历。</summary>
    public static IReadOnlyList<ScenarioFixture> All() =>
    [
        L01_Healthy(),
        L02_DeadLocalProxy(),
        L09_DnsFailure(),
        L14_NcsiMismatch(),
        L15_SingleSiteIssue(),
        L20_WinHttpProxyConfig(),
        L21_PacUnreachable(),
        L22_ApipaDhcp(),
        L23_CaptivePortal(),
        L24_ExternalService(),
    ];

    /// <summary>L20: WinHTTP 自定义代理配置。信息性，Confidence=Medium，无修复动作。</summary>
    public static ScenarioFixture L20_WinHttpProxyConfig() => new(
        Name: "L20_WinHttpProxyConfig",
        Environment: FakeNetworkEnvironment.Healthy("L20_WinHttpProxyConfig") with
        {
            Proxy = ProxyState.Direct with
            {
                WinhttpEnabled = true,
            },
        },
        ExpectedFindingId: "finding.winhttp_proxy_config",
        ExpectedConfidence: Confidence.Medium,
        ExpectedRecommendedActionId: null);

    /// <summary>L21: PAC 不可达。家庭网络，非企业环境。</summary>
    public static ScenarioFixture L21_PacUnreachable() => new(
        Name: "L21_PacUnreachable",
        Environment: FakeNetworkEnvironment.Healthy("L21_PacUnreachable") with
        {
            Proxy = ProxyState.Direct with
            {
                PacEnabled = true,
                PacReachable = false,
            },
            Web = WebState.Healthy with { SystemProxyHttpsOk = false },
        },
        ExpectedFindingId: "finding.pac_unreachable",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>L22: APIPA/DHCP 异常。DHCP 接口获得 169.254 地址。</summary>
    public static ScenarioFixture L22_ApipaDhcp() => new(
        Name: "L22_ApipaDhcp",
        Environment: FakeNetworkEnvironment.Healthy("L22_ApipaDhcp") with
        {
            Adapter = AdapterState.Healthy with
            {
                HasValidIpv4 = false,
                HasApiPAAddress = true,
                HasDefaultGateway = false,
            },
            Web = WebState.Healthy with
            {
                DirectHttpsOk = false,
                SystemProxyHttpsOk = false,
                TargetSiteResolves = false,
                TargetSiteConnects = false,
            },
        },
        ExpectedFindingId: "finding.apipa_dhcp",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>L23: 认证门户。WEB-01 检测到重定向信号。</summary>
    public static ScenarioFixture L23_CaptivePortal() => new(
        Name: "L23_CaptivePortal",
        Environment: FakeNetworkEnvironment.Healthy("L23_CaptivePortal") with
        {
            Web = WebState.Healthy with
            {
                NcsiConnected = false,
                CaptivePortalDetected = true,
                DirectHttpsOk = false,
                SystemProxyHttpsOk = false,
            },
        },
        ExpectedFindingId: "finding.captive_portal",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>L24: 外部服务故障。本机正常，仅目标网站不可达。</summary>
    public static ScenarioFixture L24_ExternalService() => new(
        Name: "L24_ExternalService",
        Environment: FakeNetworkEnvironment.Healthy("L24_ExternalService") with
        {
            Web = WebState.Healthy with
            {
                TargetSiteResolves = false,
                TargetSiteConnects = false,
            },
        },
        ExpectedFindingId: "finding.target_unreachable",
        ExpectedConfidence: Confidence.High,
        ExpectedRecommendedActionId: null);

    /// <summary>构建包含所有内置规则的注册表（委托生产入口）。</summary>
    public static RuleRegistry BuildRuleRegistry() => BuiltinRuleRegistry.CreateDefault();
}

/// <summary>
/// 场景 fixture：环境 + 预期结果。
/// </summary>
public sealed record ScenarioFixture(
    string Name,
    FakeNetworkEnvironment Environment,
    string ExpectedFindingId,
    Confidence ExpectedConfidence,
    string? ExpectedRecommendedActionId);
