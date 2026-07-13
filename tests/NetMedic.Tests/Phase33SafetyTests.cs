using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;
using NetMedic.Core.Testing;

namespace NetMedic.Tests;

/// <summary>
/// 阶段 3.3 安全反例测试 + L01-L16 覆盖矩阵。
/// 直接调用生产代码（规则 + FakeProbeSet + BuiltinRuleRegistry）。
/// </summary>
public class Phase33SafetyTests
{
    // === DeadLocalProxyRule 反例测试 ===

    /// <summary>非回环远程代理不可达，不命中 dead_local_proxy。</summary>
    [Fact]
    public async Task DeadLocalProxy_NonLoopbackProxy_DoesNotMatch()
    {
        var env = FakeNetworkEnvironment.Healthy("NonLoopbackProxy") with
        {
            Proxy = ProxyState.Direct with
            {
                WininetEnabled = true,
                WininetAddress = "10.0.0.5:7890",
                WininetIsLoopback = false, // 非回环
                WininetPortListening = false,
            },
            Web = WebState.Healthy with { SystemProxyHttpsOk = false },
        };

        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id == "finding.dead_local_proxy");
    }

    /// <summary>回环代理端口正常监听，不命中 dead_local_proxy。</summary>
    [Fact]
    public async Task DeadLocalProxy_PortListening_DoesNotMatch()
    {
        var env = FakeNetworkEnvironment.Healthy("ProxyListening") with
        {
            Proxy = ProxyState.Direct with
            {
                WininetEnabled = true,
                WininetAddress = "127.0.0.1:7890",
                WininetIsLoopback = true,
                WininetPortListening = true, // 端口正常
            },
        };

        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id == "finding.dead_local_proxy");
    }

    /// <summary>回环代理端口关闭且直连成功，才命中。</summary>
    [Fact]
    public async Task DeadLocalProxy_LoopbackPortClosed_DirectOk_Matches()
    {
        var env = FakeNetworkEnvironment.Healthy("DeadProxy") with
        {
            Proxy = ProxyState.Direct with
            {
                WininetEnabled = true,
                WininetAddress = "127.0.0.1:7890",
                WininetIsLoopback = true,
                WininetPortListening = false,
            },
            Web = WebState.Healthy with { SystemProxyHttpsOk = false },
        };

        var findings = await RunDiagnosisAsync(env);
        Assert.Contains(findings, f => f.Id == "finding.dead_local_proxy");
    }

    /// <summary>回环代理端口关闭但直连失败，不命中（需要直连成功作为反证）。</summary>
    [Fact]
    public async Task DeadLocalProxy_DirectFails_DoesNotMatch()
    {
        var env = FakeNetworkEnvironment.Healthy("DeadProxyDirectFail") with
        {
            Proxy = ProxyState.Direct with
            {
                WininetEnabled = true,
                WininetAddress = "127.0.0.1:7890",
                WininetIsLoopback = true,
                WininetPortListening = false,
            },
            Web = WebState.Healthy with { DirectHttpsOk = false, SystemProxyHttpsOk = false },
        };

        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id == "finding.dead_local_proxy");
    }

    // === DNS 规则反例测试 ===

    /// <summary>DNS 服务器不可达时，不推荐 FIX-DNS-01。</summary>
    [Fact]
    public async Task DnsFailure_DoesNotRecommendFlushCache()
    {
        var env = ScenarioFixtures.L09_DnsFailure().Environment;
        var findings = await RunDiagnosisAsync(env);
        var dnsFinding = findings.FirstOrDefault(f => f.Id == "finding.dns_failure");
        Assert.NotNull(dnsFinding);
        Assert.Null(dnsFinding!.RecommendedActionId);
    }

    /// <summary>仅 DNS-02 Failed 不得声称是 DNS 缓存异常。</summary>
    [Fact]
    public async Task DnsFailure_NotClaimedAsCacheAnomaly()
    {
        var env = ScenarioFixtures.L09_DnsFailure().Environment;
        var findings = await RunDiagnosisAsync(env);
        // 不应存在 "缓存异常" 相关的 finding
        Assert.DoesNotContain(findings, f => f.Id.Contains("cache"));
    }

    /// <summary>有网关不等于 DNS 缓存异常。</summary>
    [Fact]
    public async Task GatewayOk_NotDnsCacheAnomaly()
    {
        var env = FakeNetworkEnvironment.Healthy("GatewayOk") with
        {
            Dns = DnsState.Healthy, // DNS 正常
        };
        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id.Contains("dns") && f.Id.Contains("cache"));
    }

    // === L01-L16 覆盖矩阵 ===

    /// <summary>
    /// L01-L16 场景覆盖矩阵。
    /// 标注每个场景的当前覆盖状态。
    /// </summary>
    public static readonly TheoryData<string, string, string> CoverageMatrix = new()
    {
        // (场景ID, 描述, 状态)
        { "L01", "健康网络，无代理/VPN", "已覆盖" },
        { "L02", "失效本地代理", "已覆盖" },
        { "L03", "本地代理端口正常工作", "已覆盖(反例)" },
        { "L04", "WinHTTP自定义代理(仅配置)", "已覆盖" },
        { "L05", "PAC不可达(家庭网络)", "已覆盖" },
        { "L06", "PAC由策略配置(保护上下文)", "已覆盖(保护降级)" },
        { "L07", "APIPA/DHCP异常", "已覆盖" },
        { "L08", "静态IP无网关", "延后(需深度探针)" },
        { "L09", "DNS服务器不可达", "已覆盖" },
        { "L10", "DNS缓存异常(需DNS-03/04)", "延后(需深度探针)" },
        { "L11", "正常VPN/虚拟网卡不误判", "延后(需VPN-01探针)" },
        { "L12", "VPN退出后遗留路由", "延后(需ROUTE-01探针)" },
        { "L13", "认证门户", "已覆盖" },
        { "L14", "NCSI异常但HTTPS正常", "已覆盖" },
        { "L15", "单站故障", "已覆盖" },
        { "L16", "IPv4/IPv6分离测试", "延后(需IP-01探针)" },
    };

    /// <summary>
    /// 验证覆盖矩阵完整性：每个 L01-L16 都有明确状态。
    /// </summary>
    [Theory]
    [MemberData(nameof(CoverageMatrix))]
    public void CoverageMatrix_AllScenariosHaveStatus(string scenarioId, string description, string status)
    {
        Assert.False(string.IsNullOrEmpty(scenarioId));
        Assert.False(string.IsNullOrEmpty(description));
        Assert.True(status.StartsWith("已覆盖") || status.StartsWith("延后"),
            $"Unexpected status '{status}' for {scenarioId}");
    }

    /// <summary>
    /// L03: 本地代理端口正常工作，不关闭代理、不显示修复。
    /// </summary>
    [Fact]
    public async Task L03_ProxyPortWorking_NoRepairNoClose()
    {
        var env = FakeNetworkEnvironment.Healthy("L03") with
        {
            Proxy = ProxyState.Direct with
            {
                WininetEnabled = true,
                WininetAddress = "127.0.0.1:7890",
                WininetIsLoopback = true,
                WininetPortListening = true,
            },
        };
        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id == "finding.dead_local_proxy");
    }

    /// <summary>
    /// L08: 静态 IP 无网关，不按 DHCP/APIPA 自动处理。
    /// 当前无静态 IP 探针，只验证 APIPA 规则不误命中。
    /// </summary>
    [Fact]
    public async Task L08_StaticIpNoGateway_NotApipa()
    {
        // 构造无 APIPA 但无网关的场景
        var env = FakeNetworkEnvironment.Healthy("L08") with
        {
            Adapter = AdapterState.Healthy with
            {
                HasDefaultGateway = false,
                HasDefaultRoute = false,
                HasApiPAAddress = false, // 非 APIPA
                HasValidIpv4 = true, // 有有效 IP
            },
        };
        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id == "finding.apipa_dhcp");
    }

    /// <summary>
    /// L11: 正常 VPN/虚拟网卡不得因存在虚拟网卡而误判。
    /// 当前无 VPN-01 探针，只验证不产生虚假 VPN 相关结论。
    /// </summary>
    [Fact]
    public async Task L11_VpnAdapterPresent_NoFalseFinding()
    {
        var env = FakeNetworkEnvironment.Healthy("L11") with
        {
            Adapter = AdapterState.Healthy with { VpnActive = true },
        };
        var findings = await RunDiagnosisAsync(env);
        // 不应产生 VPN 残留或 VPN 故障结论
        Assert.DoesNotContain(findings, f => f.Id.Contains("vpn"));
    }

    /// <summary>
    /// L16: 当前没有完整 IPv4/IPv6 分离探针，不输出虚假双栈结论。
    /// </summary>
    [Fact]
    public async Task L16_NoDualStackConclusions_WithoutProbes()
    {
        var env = FakeNetworkEnvironment.Healthy("L16");
        var findings = await RunDiagnosisAsync(env);
        Assert.DoesNotContain(findings, f => f.Id.Contains("ipv6") || f.Id.Contains("dual_stack"));
    }

    // === 可执行动作测试 ===

    /// <summary>
    /// 阶段 3 可执行修复动作数量为 0。
    /// </summary>
    [Fact]
    public void ExecutableRepairActions_EmptyInStage3()
    {
        Assert.Empty(BuiltinRuleRegistry.ExecutableRepairActions);
    }

    /// <summary>
    /// 计划动作不等于可执行动作。
    /// </summary>
    [Fact]
    public void PlannedActions_NotExecutable()
    {
        Assert.NotEmpty(BuiltinRuleRegistry.PlannedRepairActionIds);
        // 所有计划动作都不在可执行集合中
        foreach (var planned in BuiltinRuleRegistry.PlannedRepairActionIds)
        {
            Assert.False(BuiltinRuleRegistry.ExecutableRepairActions.Contains(planned),
                $"Planned action '{planned}' should not be executable in stage 3");
        }
    }

    // === 辅助方法 ===

    private static async Task<IReadOnlyList<Finding>> RunDiagnosisAsync(FakeNetworkEnvironment env)
    {
        var probes = FakeProbeSet.BuildQuick(env);
        var orchestrator = new ProbeOrchestrator(probes);
        var result = await orchestrator.ExecuteAsync(
            env, SymptomCategory.Unsure, DiagnosticMode.Quick,
            TimeSpan.FromSeconds(10), CancellationToken.None);
        var snapshot = DiagnosticSnapshot.From(result.Session);
        return BuiltinRuleRegistry.CreateDefault().EvaluateAll(snapshot);
    }
}
