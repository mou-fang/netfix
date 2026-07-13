using System.Collections.Immutable;
using System.Diagnostics;
using NetMedic.Core.Abstractions;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 探针进度事件。探针开始/完成时由编排器发出。
/// 对应任务书 §7.8 应用内事件 probe_started / probe_finished。
/// </summary>
public sealed record ProbeProgressEvent(
    string ProbeId,
    ProbeStage Stage,
    DateTimeOffset Timestamp);

public enum ProbeStage
{
    Started,
    Finished,
    Skipped,
    TimedOut,
}

/// <summary>
/// 探针编排结果。包含会话和所有探针结果。
/// </summary>
public sealed record OrchestrationResult(
    DiagnosticSession Session,
    IReadOnlyList<ProbeResult> Results,
    bool WasCancelled,
    TimeSpan TotalDuration);

/// <summary>
/// 探针编排器。负责并发调度、独立超时、总体超时、取消和进度事件。
/// 对应任务书 §5.2：总体有时间预算和取消令牌；探针超时返回 error/skipped。
/// </summary>
public sealed class ProbeOrchestrator
{
    private readonly IReadOnlyList<IProbe> _probes;
    private readonly int _maxConcurrency;

    /// <summary>
    /// 创建编排器。
    /// </summary>
    /// <param name="probes">要执行的探针列表。</param>
    /// <param name="maxConcurrency">最大并发数。对应任务书 §8.4：并发探针数量有限制。</param>
    public ProbeOrchestrator(IReadOnlyList<IProbe> probes, int maxConcurrency = 4)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        }

        _probes = probes ?? throw new ArgumentNullException(nameof(probes));
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// 执行所有探针，返回结果。
    /// </summary>
    /// <param name="environment">网络环境（真实或 Fake）。</param>
    /// <param name="symptom">用户症状。</param>
    /// <param name="mode">体检模式。</param>
    /// <param name="totalBudget">总体超时预算。超时后未完成的探针被取消。</param>
    /// <param name="externalCancellationToken">外部取消令牌（用户点取消）。</param>
    /// <param name="progress">可选的进度回调。</param>
    public async Task<OrchestrationResult> ExecuteAsync(
        INetworkEnvironment environment,
        SymptomCategory symptom,
        DiagnosticMode mode,
        TimeSpan totalBudget,
        CancellationToken externalCancellationToken,
        IProgress<ProbeProgressEvent>? progress = null)
    {
        var session = DiagnosticSession.Create(symptom, mode);
        var overallSw = Stopwatch.StartNew();

        // 总体超时 CancellationTokenSource，与外部取消链接
        using var totalCts = new CancellationTokenSource(totalBudget);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            totalCts.Token, externalCancellationToken);

        var results = new List<ProbeResult>(_probes.Count);
        var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        var lockObj = new object();

        var tasks = _probes.Select(probe => RunProbeWithTimeoutAsync(
            probe, environment, session, linkedCts.Token, semaphore, progress, lockObj, results)).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 外部取消或总体超时；已完成的探针结果已在 results 中
        }

        overallSw.Stop();

        // 如果是外部取消（而非总体超时），标记 WasCancelled
        bool wasCancelled = externalCancellationToken.IsCancellationRequested;

        // 为未完成的探针补充 Skipped 结果
        var completedIds = results.Select(r => r.Id).ToHashSet();
        foreach (var probe in _probes)
        {
            if (!completedIds.Contains(probe.Id))
            {
                results.Add(ProbeResult.Skip(probe.Id, "probe.skipped.cancelled"));
            }
        }

        var finalSession = session.WithResults(results);
        return new OrchestrationResult(finalSession, results, wasCancelled, overallSw.Elapsed);
    }

    private static async Task RunProbeWithTimeoutAsync(
        IProbe probe,
        INetworkEnvironment environment,
        DiagnosticSession session,
        CancellationToken linkedToken,
        SemaphoreSlim semaphore,
        IProgress<ProbeProgressEvent>? progress,
        object lockObj,
        List<ProbeResult> results)
    {
        // 如果已取消，直接跳过
        if (linkedToken.IsCancellationRequested)
        {
            progress?.Report(new ProbeProgressEvent(probe.Id, ProbeStage.Skipped, DateTimeOffset.UtcNow));
            return;
        }

        await semaphore.WaitAsync(linkedToken).ConfigureAwait(false);
        try
        {
            progress?.Report(new ProbeProgressEvent(probe.Id, ProbeStage.Started, DateTimeOffset.UtcNow));

            var context = new ProbeContext(session, environment);
            var sw = Stopwatch.StartNew();

            // 每个探针独立超时
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
            probeCts.CancelAfter(probe.Timeout);

            try
            {
                var result = await probe.ExecuteAsync(context, probeCts.Token).ConfigureAwait(false);
                sw.Stop();
                // 用实际耗时覆盖（探针可能返回零）
                var finalResult = result with { Duration = sw.Elapsed, StartedAt = DateTimeOffset.UtcNow - sw.Elapsed };
                lock (lockObj)
                {
                    results.Add(finalResult);
                }
                progress?.Report(new ProbeProgressEvent(probe.Id, ProbeStage.Finished, DateTimeOffset.UtcNow));
            }
            catch (OperationCanceledException) when (probeCts.IsCancellationRequested && !linkedToken.IsCancellationRequested)
            {
                // 单探针超时（非总体取消）
                sw.Stop();
                var timeoutResult = ProbeResult.Err(
                    probe.Id,
                    "probe.timeout",
                    new ProbeError("PROBE_TIMEOUT", "probe.timeout"),
                    sw.Elapsed);
                lock (lockObj)
                {
                    results.Add(timeoutResult);
                }
                progress?.Report(new ProbeProgressEvent(probe.Id, ProbeStage.TimedOut, DateTimeOffset.UtcNow));
            }
            catch (OperationCanceledException)
            {
                // 总体取消或外部取消
                sw.Stop();
                lock (lockObj)
                {
                    results.Add(ProbeResult.Skip(probe.Id, "probe.skipped.cancelled"));
                }
                progress?.Report(new ProbeProgressEvent(probe.Id, ProbeStage.Skipped, DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                // 探针自身异常 -> Error 状态
                sw.Stop();
                var errorResult = ProbeResult.Err(
                    probe.Id,
                    "probe.error",
                    new ProbeError("PROBE_EXCEPTION", ex.GetType().Name),
                    sw.Elapsed);
                lock (lockObj)
                {
                    results.Add(errorResult);
                }
                progress?.Report(new ProbeProgressEvent(probe.Id, ProbeStage.Finished, DateTimeOffset.UtcNow));
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}
