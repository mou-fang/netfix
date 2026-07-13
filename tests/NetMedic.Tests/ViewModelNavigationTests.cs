using NetMedic.App;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;
using NetMedic.Core.Testing;

namespace NetMedic.Tests;

/// <summary>
/// ViewModel 导航测试：直接调用生产 MainViewModel 命令，验证阶段 3 修复流程不可达。
/// 仅 net10.0-windows（需要 NetMedic.App）。
/// </summary>
public class ViewModelNavigationTests
{
    /// <summary>
    /// 即使首选 Finding 的 RecommendedActionId = "FIX-PRX-01"（计划但未注册），
    /// ExecutableRepairActions 为空，RequestRepair 不应进入 RepairConfirm 页。
    /// </summary>
    [Fact]
    public void RequestRepair_WithPlannedActionId_DoesNotEnterRepairConfirm()
    {
        var vm = new MainViewModel();

        // 构造一个 RecommendedActionId = "FIX-PRX-01" 的 Finding（计划但不可执行）
        var finding = Finding.Create(
            id: "finding.dead_local_proxy",
            confidence: Confidence.High,
            severity: FindingSeverity.High,
            titleKey: "finding.dead_local_proxy.title",
            explanationKey: "finding.dead_local_proxy.explanation",
            userSummaryKey: "finding.dead_local_proxy.summary",
            guidanceKey: "finding.dead_local_proxy.guidance",
            recommendedActionId: "FIX-PRX-01");

        vm.CurrentPage = AppPage.Result;
        vm.PrimaryFinding = finding;
        // ExecutableRepairActions 为空，CanExecute 恒为 false
        vm.CanRepairPrimaryFinding = RepairAvailabilityEvaluator.CanExecute(
            finding, BuiltinRuleRegistry.ExecutableRepairActions);

        Assert.False(vm.CanRepairPrimaryFinding);

        vm.RequestRepairCommand.Execute(null);

        // 没有跳转到修复确认页
        Assert.Equal(AppPage.Result, vm.CurrentPage);
        Assert.Null(vm.SelectedRepairAction);
    }

    /// <summary>
    /// 所有场景的所有 Finding 在阶段 3 都不可执行修复。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void AllScenarios_CanRepair_IsFalse_InStage3(ScenarioFixture fixture)
    {
        var findings = RunDiagnosis(fixture.Environment);
        foreach (var f in findings)
        {
            Assert.False(RepairAvailabilityEvaluator.CanExecute(f, BuiltinRuleRegistry.ExecutableRepairActions));
        }
    }

    /// <summary>
    /// 即使因某种原因到达 RepairConfirm 页，ConfirmRepair 不应产生假成功文案、不跳转 RepairResult。
    /// </summary>
    [Fact]
    public void ConfirmRepair_DoesNotProduceFakeSuccess()
    {
        var vm = new MainViewModel();
        vm.CurrentPage = AppPage.RepairConfirm;

        vm.ConfirmRepairCommand.Execute(null);

        // 不得进入 RepairResult
        Assert.NotEqual(AppPage.RepairResult, vm.CurrentPage);
        // 不得出现假成功文案
        Assert.DoesNotContain("修复成功", vm.ResultMessage);
        Assert.DoesNotContain("已修复", vm.ResultMessage);
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
