using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Testing;

namespace NetMedic.Tests;

/// <summary>
/// 探针编排器测试：取消、单探针超时、总体超时。
/// 对应任务书阶段 1 验收：取消和超时不冻结 UI，且有自动测试。
/// 所有测试使用极短延迟（≤200ms），不依赖长时间 Thread.Sleep。
/// </summary>
public class OrchestratorTests
{
    /// <summary>
    /// 单探针超时：探针内部延迟超过其 Timeout，应返回 Error 状态且 ProbeError 标记超时。
    /// </summary>
    [Fact]
    public async Task SingleProbeTimeout_ReturnsErrorStatus()
    {
        // 探针超时 50ms，但内部延迟 2000ms（远超超时）
        var slowProbe = new FakeProbe(
            "SLOW-01",
            _ => ProbeResult.Pass("SLOW-01", "ok"),
            timeout: TimeSpan.FromMilliseconds(50),
            injectedDelay: TimeSpan.FromMilliseconds(2000));

        var orchestrator = new ProbeOrchestrator([slowProbe]);
        var env = FakeNetworkEnvironment.Healthy();

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(10),
            externalCancellationToken: CancellationToken.None);

        var probeResult = Assert.Single(result.Results);
        Assert.Equal(ProbeStatus.Error, probeResult.Status);
        Assert.NotNull(probeResult.Error);
        Assert.Equal("PROBE_TIMEOUT", probeResult.Error.Code);
        Assert.False(result.WasCancelled);
    }

    /// <summary>
    /// 外部取消：用户取消后，未开始的探针被跳过，已完成的保留结果。
    /// </summary>
    [Fact]
    public async Task ExternalCancel_UnstartedProbesSkipped()
    {
        // 第一个探针快速完成，第二个延迟 2000ms
        var fastProbe = FakeProbe.Always("FAST-01", ProbeResult.Pass("FAST-01", "ok"));
        var slowProbe = new FakeProbe(
            "SLOW-01",
            _ => ProbeResult.Pass("SLOW-01", "ok"),
            timeout: TimeSpan.FromSeconds(10),
            injectedDelay: TimeSpan.FromMilliseconds(2000));

        var orchestrator = new ProbeOrchestrator([fastProbe, slowProbe], maxConcurrency: 1);
        var env = FakeNetworkEnvironment.Healthy();

        using var cts = new CancellationTokenSource();
        // 在 100ms 后取消（fastProbe 完成后，slowProbe 进行中）
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(10),
            externalCancellationToken: cts.Token);

        Assert.True(result.WasCancelled);
        // 至少有一个结果（fastProbe 完成）
        Assert.NotEmpty(result.Results);
        // slowProbe 应被跳过或取消
        Assert.Contains(result.Results, r => r.Id == "SLOW-01" &&
            (r.Status == ProbeStatus.Skipped || r.Status == ProbeStatus.Error));
    }

    /// <summary>
    /// 总体超时：totalBudget 到期后，未完成的探针被跳过。
    /// </summary>
    [Fact]
    public async Task TotalBudgetTimeout_UnfinishedProbesSkipped()
    {
        // 两个探针都延迟 2000ms，总体预算只有 100ms
        var probe1 = new FakeProbe("SLOW-01", _ => ProbeResult.Pass("SLOW-01", "ok"),
            timeout: TimeSpan.FromSeconds(10), injectedDelay: TimeSpan.FromMilliseconds(2000));
        var probe2 = new FakeProbe("SLOW-02", _ => ProbeResult.Pass("SLOW-02", "ok"),
            timeout: TimeSpan.FromSeconds(10), injectedDelay: TimeSpan.FromMilliseconds(2000));

        var orchestrator = new ProbeOrchestrator([probe1, probe2]);
        var env = FakeNetworkEnvironment.Healthy();

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromMilliseconds(100),
            externalCancellationToken: CancellationToken.None);

        // 总体超时不是外部取消
        Assert.False(result.WasCancelled);
        // 两个探针都应被跳过
        Assert.All(result.Results, r =>
        {
            Assert.True(r.Status == ProbeStatus.Skipped || r.Status == ProbeStatus.Error);
        });
    }

    /// <summary>
    /// 正常执行：所有探针快速完成，无超时无取消。
    /// </summary>
    [Fact]
    public async Task NormalExecution_AllProbesComplete()
    {
        var probe1 = FakeProbe.Always("P-01", ProbeResult.Pass("P-01", "ok"));
        var probe2 = FakeProbe.Always("P-02", ProbeResult.Pass("P-02", "ok"));
        var probe3 = FakeProbe.Always("P-03", ProbeResult.Pass("P-03", "ok"));

        var orchestrator = new ProbeOrchestrator([probe1, probe2, probe3]);
        var env = FakeNetworkEnvironment.Healthy();

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(5),
            externalCancellationToken: CancellationToken.None);

        Assert.False(result.WasCancelled);
        Assert.Equal(3, result.Results.Count);
        Assert.All(result.Results, r => Assert.Equal(ProbeStatus.Passed, r.Status));
    }

    /// <summary>
    /// 进度事件：每个探针应发出 Started 和 Finished 事件。
    /// </summary>
    [Fact]
    public async Task ProgressEvents_StartedAndFinishedEmitted()
    {
        var probe = FakeProbe.Always("P-01", ProbeResult.Pass("P-01", "ok"));
        var orchestrator = new ProbeOrchestrator([probe]);
        var env = FakeNetworkEnvironment.Healthy();

        var events = new List<ProbeProgressEvent>();
        var progress = new Progress<ProbeProgressEvent>(e => events.Add(e));

        await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(5),
            externalCancellationToken: CancellationToken.None,
            progress: progress);

        // 至少有 Started 和 Finished
        Assert.Contains(events, e => e.ProbeId == "P-01" && e.Stage == ProbeStage.Started);
        Assert.Contains(events, e => e.ProbeId == "P-01" && e.Stage == ProbeStage.Finished);
    }

    /// <summary>
    /// 探针异常：探针内部抛异常，应返回 Error 状态而非崩溃。
    /// </summary>
    [Fact]
    public async Task ProbeException_ReturnsErrorStatus()
    {
        var throwingProbe = new FakeProbe("ERR-01", _ => throw new InvalidOperationException("boom"));
        var orchestrator = new ProbeOrchestrator([throwingProbe]);
        var env = FakeNetworkEnvironment.Healthy();

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(5),
            externalCancellationToken: CancellationToken.None);

        var probeResult = Assert.Single(result.Results);
        Assert.Equal(ProbeStatus.Error, probeResult.Status);
        Assert.NotNull(probeResult.Error);
        Assert.Equal("PROBE_EXCEPTION", probeResult.Error.Code);
    }
}
