using NetMedic.Core.Diagnostics;
using NetMedic.Core.Testing;

namespace NetMedic.Tests;

/// <summary>
/// 5 个必测场景的稳定性测试。对应任务书 §11.2 L01/L02/L09/L14/L15。
/// 每个场景运行多次，确保结果稳定可重复。
/// </summary>
public class ScenarioTests
{
    /// <summary>
    /// 所有 5 个场景的预期 Finding 都正确命中。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public async Task Scenario_ProducesExpectedFinding(ScenarioFixture fixture)
    {
        var findings = await RunDiagnosisAsync(fixture.Environment);

        // 首要 Finding 应匹配预期
        var primary = findings.FirstOrDefault();
        Assert.NotNull(primary);
        Assert.Equal(fixture.ExpectedFindingId, primary!.Id);
        Assert.Equal(fixture.ExpectedConfidence, primary.Confidence);
        Assert.Equal(fixture.ExpectedRecommendedActionId, primary.RecommendedActionId);
    }

    /// <summary>
    /// 每个场景运行 5 次，结果必须完全一致（稳定可重复）。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public async Task Scenario_ResultsAreStableAndRepeatable(ScenarioFixture fixture)
    {
        var firstRun = await RunDiagnosisAsync(fixture.Environment);

        for (int i = 0; i < 4; i++)
        {
            var subsequentRun = await RunDiagnosisAsync(fixture.Environment);
            // 首要 Finding 的 Id 和 Confidence 必须一致
            Assert.Equal(firstRun.FirstOrDefault()?.Id, subsequentRun.FirstOrDefault()?.Id);
            Assert.Equal(firstRun.FirstOrDefault()?.Confidence, subsequentRun.FirstOrDefault()?.Confidence);
            Assert.Equal(firstRun.FirstOrDefault()?.RecommendedActionId, subsequentRun.FirstOrDefault()?.RecommendedActionId);
        }
    }

    /// <summary>
    /// L01 健康网络：不应推荐任何修复动作。
    /// </summary>
    [Fact]
    public async Task L01_Healthy_NoRepairActionRecommended()
    {
        var findings = await RunDiagnosisAsync(ScenarioFixtures.L01_Healthy().Environment);
        var primary = findings.First();
        Assert.Equal("finding.network_healthy", primary.Id);
        Assert.Null(primary.RecommendedActionId);
    }

    /// <summary>
    /// L02 失效本地代理：应推荐 FIX-PRX-01。
    /// </summary>
    [Fact]
    public async Task L02_DeadLocalProxy_RecommendsDisableProxy()
    {
        var findings = await RunDiagnosisAsync(ScenarioFixtures.L02_DeadLocalProxy().Environment);
        var primary = findings.First();
        Assert.Equal("finding.dead_local_proxy", primary.Id);
        Assert.Equal("FIX-PRX-01", primary.RecommendedActionId);
    }

    /// <summary>
    /// L09 DNS 故障：应推荐 FIX-DNS-01。
    /// </summary>
    [Fact]
    public async Task L09_DnsFailure_RecommendsFlushDns()
    {
        var findings = await RunDiagnosisAsync(ScenarioFixtures.L09_DnsFailure().Environment);
        var primary = findings.First();
        Assert.Equal("finding.dns_failure", primary.Id);
        Assert.Equal("FIX-DNS-01", primary.RecommendedActionId);
    }

    /// <summary>
    /// L14 NCSI 不一致：不应推荐任何修复（不重置网络）。
    /// </summary>
    [Fact]
    public async Task L14_NcsiMismatch_NoRepairRecommended()
    {
        var findings = await RunDiagnosisAsync(ScenarioFixtures.L14_NcsiMismatch().Environment);
        var primary = findings.First();
        Assert.Equal("finding.ncsi_mismatch", primary.Id);
        Assert.Null(primary.RecommendedActionId);
    }

    /// <summary>
    /// L15 单站故障：不应推荐全局重置（无推荐动作）。
    /// TargetUnreachableRule 命中（合并 SingleSiteIssue 和 ExternalService）。
    /// </summary>
    [Fact]
    public async Task L15_SingleSiteIssue_NoGlobalResetRecommended()
    {
        var findings = await RunDiagnosisAsync(ScenarioFixtures.L15_SingleSiteIssue().Environment);
        var primary = findings.First();
        Assert.Equal("finding.target_unreachable", primary.Id);
        Assert.Null(primary.RecommendedActionId);
    }

    // --- 辅助方法 ---

    private static async Task<IReadOnlyList<Finding>> RunDiagnosisAsync(FakeNetworkEnvironment env)
    {
        var probes = FakeProbeSet.BuildQuick(env);
        var orchestrator = new ProbeOrchestrator(probes);

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(10),
            externalCancellationToken: CancellationToken.None);

        var snapshot = DiagnosticSnapshot.From(result.Session);
        var registry = ScenarioFixtures.BuildRuleRegistry();
        return registry.EvaluateAll(snapshot);
    }

    public static IEnumerable<object[]> AllScenarios() =>
        ScenarioFixtures.All().Select(f => new object[] { f });
}
