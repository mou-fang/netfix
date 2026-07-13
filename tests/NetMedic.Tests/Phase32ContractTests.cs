using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;
using NetMedic.Core.Testing;

namespace NetMedic.Tests;

/// <summary>
/// 阶段 3.2 测试：资源完整性、修复按钮控制、规则冲突、NET-01 契约。
/// 直接调用生产代码。
/// </summary>
public class Phase32ContractTests
{
    // === 1. 资源完整性测试 ===

    /// <summary>
    /// 每条 BuiltinRuleRegistry 规则产生的 Finding 必须有非空的
    /// TitleKey/ExplanationKey/UserSummaryKey/GuidanceKey。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void Finding_AllResourceKeys_NonEmpty(ScenarioFixture fixture)
    {
        var findings = RunDiagnosis(fixture.Environment);
        Assert.NotEmpty(findings);

        foreach (var f in findings)
        {
            Assert.False(string.IsNullOrEmpty(f.TitleKey), $"{f.Id}: TitleKey empty");
            Assert.False(string.IsNullOrEmpty(f.ExplanationKey), $"{f.Id}: ExplanationKey empty");
            Assert.False(string.IsNullOrEmpty(f.UserSummaryKey), $"{f.Id}: UserSummaryKey empty");
            // GuidanceKey 可以为 null，但如果存在则不能为空字符串
            if (f.GuidanceKey is not null)
            {
                Assert.False(string.IsNullOrWhiteSpace(f.GuidanceKey), $"{f.Id}: GuidanceKey whitespace");
            }
        }
    }

    /// <summary>
    /// 首选 Finding 必须有 GuidanceKey。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void PrimaryFinding_HasGuidanceKey(ScenarioFixture fixture)
    {
        var findings = RunDiagnosis(fixture.Environment);
        var primary = findings.First();
        Assert.False(string.IsNullOrEmpty(primary.GuidanceKey), $"{primary.Id}: GuidanceKey missing");
    }

    // === 2. 修复按钮显示/隐藏测试 ===

    /// <summary>
    /// 所有 Finding 的 RecommendedActionId 必须属于 ExecutableRepairActions，
    /// 或者为 null。
    /// 阶段 3.3：ExecutableRepairActions 为空，因此所有 RecommendedActionId 必须为 null。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void RecommendedActionId_MustBeSupportedOrNull(ScenarioFixture fixture)
    {
        var findings = RunDiagnosis(fixture.Environment);
        foreach (var f in findings)
        {
            if (f.RecommendedActionId is not null)
            {
                Assert.True(BuiltinRuleRegistry.ExecutableRepairActions.Contains(f.RecommendedActionId),
                    $"{f.Id}: RecommendedActionId '{f.RecommendedActionId}' not in ExecutableRepairActions");
            }
        }
    }

    /// <summary>
    /// 阶段 3.3：所有场景的所有 Finding 的 RecommendedActionId 必须为 null。
    /// </summary>
    [Fact]
    public void AllFindings_RecommendedActionId_IsNull_InStage3()
    {
        foreach (var fixture in ScenarioFixtures.All())
        {
            var findings = RunDiagnosis(fixture.Environment);
            foreach (var f in findings)
            {
                Assert.Null(f.RecommendedActionId);
            }
        }
    }

    /// <summary>
    /// 阶段 3.3：ExecutableRepairActions 必须为空（无真实 IRepairAction 实现）。
    /// </summary>
    [Fact]
    public void ExecutableRepairActions_IsEmpty_InStage3()
    {
        Assert.Empty(BuiltinRuleRegistry.ExecutableRepairActions);
    }

    /// <summary>
    /// FIX-PRX-01（失效代理）：阶段 3.3 ExecutableRepairActions 为空，
    /// RecommendedActionId 为 null，CanRepairPrimaryFinding = false（不显示按钮）。
    /// </summary>
    [Fact]
    public void CanRepair_PrimaryFinding_FIX_PRX_01_ShowsButton()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L02_DeadLocalProxy().Environment);
        var primary = findings.First();
        Assert.Equal("finding.dead_local_proxy", primary.Id);
        Assert.Null(primary.RecommendedActionId);
        // ExecutableRepairActions 为空，即使存在 action id 也不应显示修复按钮
        Assert.False(primary.RecommendedActionId is not null
            && BuiltinRuleRegistry.ExecutableRepairActions.Contains(primary.RecommendedActionId));
    }

    /// <summary>
    /// FIX-DNS-01（DNS 故障）：阶段 3.3 ExecutableRepairActions 为空，
    /// RecommendedActionId 为 null，CanRepairPrimaryFinding = false（不显示按钮）。
    /// </summary>
    [Fact]
    public void CanRepair_PrimaryFinding_FIX_DNS_01_ShowsButton()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L09_DnsFailure().Environment);
        var primary = findings.First();
        Assert.Equal("finding.dns_failure", primary.Id);
        Assert.Null(primary.RecommendedActionId);
        // ExecutableRepairActions 为空，即使存在 action id 也不应显示修复按钮
        Assert.False(primary.RecommendedActionId is not null
            && BuiltinRuleRegistry.ExecutableRepairActions.Contains(primary.RecommendedActionId));
    }

    /// <summary>
    /// PAC 不可达：RecommendedActionId = null（不显示修复按钮）
    /// </summary>
    [Fact]
    public void CanRepair_PacUnreachable_HidesButton()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L21_PacUnreachable().Environment);
        var primary = findings.First();
        Assert.Equal("finding.pac_unreachable", primary.Id);
        Assert.Null(primary.RecommendedActionId);
    }

    /// <summary>
    /// APIPA/DHCP：RecommendedActionId = null（不显示修复按钮）
    /// </summary>
    [Fact]
    public void CanRepair_ApipaDhcp_HidesButton()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L22_ApipaDhcp().Environment);
        var primary = findings.First();
        Assert.Equal("finding.apipa_dhcp", primary.Id);
        Assert.Null(primary.RecommendedActionId);
    }

    /// <summary>
    /// 无推荐动作（健康网络）：不显示修复按钮
    /// </summary>
    [Fact]
    public void CanRepair_Healthy_HidesButton()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L01_Healthy().Environment);
        var primary = findings.First();
        Assert.Null(primary.RecommendedActionId);
    }

    // === 3. 规则冲突收尾 ===

    /// <summary>
    /// NCSI 不一致场景：只能输出 ncsi_mismatch，不同时输出 network_healthy。
    /// 检查完整 Finding 列表。
    /// </summary>
    [Fact]
    public void NcsiMismatch_OnlyNcsiMismatch_NoNetworkHealthy()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L14_NcsiMismatch().Environment);
        Assert.Contains(findings, f => f.Id == "finding.ncsi_mismatch");
        Assert.DoesNotContain(findings, f => f.Id == "finding.network_healthy");
    }

    /// <summary>
    /// 认证门户场景：不得同时输出 ncsi_mismatch。
    /// </summary>
    [Fact]
    public void CaptivePortal_NoNcsiMismatch()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L23_CaptivePortal().Environment);
        Assert.Contains(findings, f => f.Id == "finding.captive_portal");
        Assert.DoesNotContain(findings, f => f.Id == "finding.ncsi_mismatch");
    }

    /// <summary>
    /// 单站场景：只能有一个 target_unreachable，不重复。
    /// </summary>
    [Fact]
    public void TargetUnreachable_NoDuplicate()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L15_SingleSiteIssue().Environment);
        var targetFindings = findings.Where(f => f.Id == "finding.target_unreachable").ToList();
        Assert.Single(targetFindings);
    }

    /// <summary>
    /// 健康网络：network_healthy 作为首选，不与故障同时出现。
    /// </summary>
    [Fact]
    public void Healthy_NetworkHealthyIsPrimary_NoConflicts()
    {
        var findings = RunDiagnosis(ScenarioFixtures.L01_Healthy().Environment);
        Assert.Equal("finding.network_healthy", findings.First().Id);
        Assert.DoesNotContain(findings, f => f.Id == "finding.dead_local_proxy");
        Assert.DoesNotContain(findings, f => f.Id == "finding.dns_failure");
        Assert.DoesNotContain(findings, f => f.Id == "finding.captive_portal");
    }

    // === 4. NET-01 契约对照 ===

    /// <summary>
    /// AdapterSelectionEvaluator: 单个有网关 -> Passed
    /// </summary>
    [Fact]
    public void AdapterSelection_SingleGateway_ReturnsPassed()
    {
        var result = AdapterSelectionEvaluator.Evaluate(
            [("eth0", true), ("wifi0", false)]);
        Assert.Equal(ProbeStatus.Passed, result.Status);
        Assert.Equal("eth0", result.PrimaryAdapter);
    }

    /// <summary>
    /// AdapterSelectionEvaluator: 多个有网关 -> Warning + ambiguous
    /// </summary>
    [Fact]
    public void AdapterSelection_MultipleGateway_ReturnsWarning()
    {
        var result = AdapterSelectionEvaluator.Evaluate(
            [("eth0", true), ("vpn0", true)]);
        Assert.Equal(ProbeStatus.Warning, result.Status);
        Assert.Equal("ambiguous", result.PrimaryAdapter);
        Assert.Equal(2, result.CandidatesWithGateway.Count);
    }

    /// <summary>
    /// AdapterSelectionEvaluator: 无网关 -> Failed
    /// </summary>
    [Fact]
    public void AdapterSelection_NoGateway_ReturnsFailed()
    {
        var result = AdapterSelectionEvaluator.Evaluate(
            [("eth0", false), ("wifi0", false)]);
        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.Null(result.PrimaryAdapter);
    }

    // === 5. 无结论兜底 ===

    /// <summary>
    /// InconclusiveRule: 有 Failed 探针但无具体规则命中时产生 finding.inconclusive。
    /// 构造 NET-03 Failed（无网关）但 NET-01/02 正常的场景：
    /// - ApipaDhcpRule 不命中（NET-02 Passed, has_apipa=false）
    /// - DnsFailureRule 不命中（DNS-02 Passed）
    /// - NetworkHealthyRule 不命中（NET-03 Failed）
    /// -> InconclusiveRule 命中
    /// </summary>
    [Fact]
    public void Inconclusive_FiresWhenNoSpecificRuleMatches()
    {
        var env = FakeNetworkEnvironment.Healthy("Inconclusive") with
        {
            Adapter = AdapterState.Healthy with { HasDefaultGateway = false, HasDefaultRoute = false },
            Web = WebState.Healthy with { DirectHttpsOk = false, SystemProxyHttpsOk = false, TargetSiteResolves = false, TargetSiteConnects = false },
        };
        var findings = RunDiagnosis(env);
        // 应该有 finding.inconclusive 作为兜底
        Assert.Contains(findings, f => f.Id == "finding.inconclusive");
        var inconclusive = findings.First(f => f.Id == "finding.inconclusive");
        Assert.Equal(Confidence.Insufficient, inconclusive.Confidence);
        Assert.Null(inconclusive.RecommendedActionId);
    }

    // === 辅助方法 ===

    private static IReadOnlyList<Finding> RunDiagnosis(FakeNetworkEnvironment env)
    {
        var probes = FakeProbeSet.BuildQuick(env);
        var orchestrator = new ProbeOrchestrator(probes);
        var result = orchestrator.ExecuteAsync(
            env, SymptomCategory.Unsure, DiagnosticMode.Quick,
            TimeSpan.FromSeconds(10), CancellationToken.None).Result;
        var snapshot = DiagnosticSnapshot.From(result.Session);
        return BuiltinRuleRegistry.CreateDefault().EvaluateAll(snapshot);
    }

    public static IEnumerable<object[]> AllScenarios() =>
        ScenarioFixtures.All().Select(f => new object[] { f });
}
