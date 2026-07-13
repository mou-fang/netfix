using System.Resources;
using NetMedic.App.Resources;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;
using NetMedic.Core.Testing;

namespace NetMedic.Tests;

/// <summary>
/// 阶段 3.3 资源完整性测试：使用真实 Strings.ResourceManager 验证资源存在。
/// 仅 net10.0-windows（需要 NetMedic.App）。
/// </summary>
public class ResourceIntegrityTests
{
    /// <summary>
    /// 遍历所有内置规则场景，验证每个 Finding 的资源 key 对应真实非空资源。
    /// 不能只检查 key 字符串非空，必须用 ResourceManager.GetString 验证。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void AllFindingResourceKeys_ResolveToNonEmptyStrings(ScenarioFixture fixture)
    {
        var findings = RunDiagnosis(fixture.Environment);
        Assert.NotEmpty(findings);

        var rm = Strings.ResourceManager;

        foreach (var f in findings)
        {
            AssertResourceExists(rm, f.TitleKey);
            AssertResourceExists(rm, f.ExplanationKey);
            AssertResourceExists(rm, f.UserSummaryKey);
            if (f.GuidanceKey is not null)
            {
                AssertResourceExists(rm, f.GuidanceKey);
            }
        }
    }

    /// <summary>
    /// 首选 Finding 必须有 GuidanceKey 且资源存在。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void PrimaryFinding_GuidanceKey_ExistsInResources(ScenarioFixture fixture)
    {
        var findings = RunDiagnosis(fixture.Environment);
        var primary = findings.First();
        Assert.False(string.IsNullOrEmpty(primary.GuidanceKey));
        AssertResourceExists(Strings.ResourceManager, primary.GuidanceKey);
    }

    /// <summary>
    /// 验证所有计划动作的资源也存在（供阶段 4 使用）。
    /// </summary>
    [Fact]
    public void Phase4InitialRepairDescriptorResources_Exist()
    {
        var rm = Strings.ResourceManager;
        // FIX-PRX-01
        AssertResourceExists(rm, "repair.disable_dead_proxy.title");
        AssertResourceExists(rm, "repair.disable_dead_proxy.desc");
        AssertResourceExists(rm, "repair.disable_dead_proxy.confirm");
        // FIX-DNS-01
        AssertResourceExists(rm, "repair.flush_dns.title");
        AssertResourceExists(rm, "repair.flush_dns.desc");
        AssertResourceExists(rm, "repair.flush_dns.confirm");
    }

    /// <summary>
    /// 确保已删除 RepairResult_FakeSuccess 资源。
    /// </summary>
    [Fact]
    public void FakeSuccessResource_Removed()
    {
        var rm = Strings.ResourceManager;
        var result = rm.GetString("RepairResult_FakeSuccess");
        // 资源必须已删除，严格断言 null
        Assert.Null(result);
    }

    /// <summary>
    /// 阶段 4.0 模拟修复资源完整性测试：
    /// 所有 MockRepair_* 资源必须存在且非空。
    /// </summary>
    [Theory]
    [InlineData("MockRepair_Title")]
    [InlineData("MockRepair_NoSystemModification")]
    [InlineData("MockRepair_PlanPreview")]
    [InlineData("MockRepair_CapturingSnapshot")]
    [InlineData("MockRepair_Executing")]
    [InlineData("MockRepair_Verifying")]
    [InlineData("MockRepair_RollingBack")]
    [InlineData("MockRepair_Success")]
    [InlineData("MockRepair_SuccessDetail")]
    [InlineData("MockRepair_ExecutionFailed")]
    [InlineData("MockRepair_RollbackSuccess")]
    [InlineData("MockRepair_RollbackFailed")]
    [InlineData("MockRepair_Cancelled")]
    [InlineData("MockRepair_PrivilegeInsufficient")]
    [InlineData("MockRepair_RealNotEnabled")]
    [InlineData("MockRepair_StartMock")]
    public void MockRepairResources_AllExistAndNonEmpty(string resourceKey)
    {
        var rm = Strings.ResourceManager;
        AssertResourceExists(rm, resourceKey);
    }

    /// <summary>
    /// 模拟成功文案必须包含"模拟"字样，且不得包含"修复成功"或"网络已修复"。
    /// </summary>
    [Fact]
    public void MockRepair_SuccessText_ContainsSimulationLabel()
    {
        var rm = Strings.ResourceManager;
        var success = rm.GetString("MockRepair_Success");
        Assert.False(string.IsNullOrEmpty(success));
        Assert.Contains("模拟", success);
        Assert.DoesNotContain("修复成功", success);
        Assert.DoesNotContain("网络已修复", success);

        var detail = rm.GetString("MockRepair_SuccessDetail");
        Assert.False(string.IsNullOrEmpty(detail));
        Assert.DoesNotContain("修复成功", detail);
        Assert.DoesNotContain("网络已修复", detail);
    }

    private static void AssertResourceExists(ResourceManager rm, string key)
    {
        var value = rm.GetString(key);
        Assert.False(string.IsNullOrEmpty(value),
            $"Resource '{key}' not found or empty");
        Assert.True(value != key,
            $"Resource '{key}' returned the key itself (missing resource)");
    }

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
