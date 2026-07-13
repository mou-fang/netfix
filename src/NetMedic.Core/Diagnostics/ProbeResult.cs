using System.Collections.Immutable;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 单个探针的执行结果。对应任务书 §5.2 统一输出结构。
/// </summary>
public sealed record ProbeResult(
    string Id,
    ProbeStatus Status,
    ProbeSeverity Severity,
    string SummaryKey,
    IReadOnlyDictionary<string, object?> Evidence,
    TimeSpan Duration,
    DateTimeOffset StartedAt,
    bool RequiresAdmin = false,
    ProbeError? Error = null,
    IReadOnlySet<string>? SensitiveFields = null)
{
    /// <summary>
    /// 创建一个 Passed 状态的快捷结果，供 Fake 探针使用。
    /// </summary>
    public static ProbeResult Pass(
        string id,
        string summaryKey,
        IReadOnlyDictionary<string, object?>? evidence = null,
        TimeSpan? duration = null)
        => new(
            Id: id,
            Status: ProbeStatus.Passed,
            Severity: ProbeSeverity.Info,
            SummaryKey: summaryKey,
            Evidence: evidence ?? ImmutableDictionary<string, object?>.Empty,
            Duration: duration ?? TimeSpan.Zero,
            StartedAt: DateTimeOffset.MinValue);

    /// <summary>
    /// 创建一个 Failed 状态的快捷结果，供 Fake 探针使用。
    /// </summary>
    public static ProbeResult Fail(
        string id,
        string summaryKey,
        IReadOnlyDictionary<string, object?>? evidence = null,
        ProbeSeverity severity = ProbeSeverity.High,
        TimeSpan? duration = null)
        => new(
            Id: id,
            Status: ProbeStatus.Failed,
            Severity: severity,
            SummaryKey: summaryKey,
            Evidence: evidence ?? ImmutableDictionary<string, object?>.Empty,
            Duration: duration ?? TimeSpan.Zero,
            StartedAt: DateTimeOffset.MinValue);

    /// <summary>
    /// 创建一个 Skipped 状态的快捷结果。
    /// </summary>
    public static ProbeResult Skip(string id, string summaryKey)
        => new(
            Id: id,
            Status: ProbeStatus.Skipped,
            Severity: ProbeSeverity.Info,
            SummaryKey: summaryKey,
            Evidence: ImmutableDictionary<string, object?>.Empty,
            Duration: TimeSpan.Zero,
            StartedAt: DateTimeOffset.MinValue);

    /// <summary>
    /// 创建一个 Error 状态的快捷结果（探针自身未能完成）。
    /// </summary>
    public static ProbeResult Err(
        string id,
        string summaryKey,
        ProbeError error,
        TimeSpan? duration = null)
        => new(
            Id: id,
            Status: ProbeStatus.Error,
            Severity: ProbeSeverity.Info,
            SummaryKey: summaryKey,
            Evidence: ImmutableDictionary<string, object?>.Empty,
            Duration: duration ?? TimeSpan.Zero,
            StartedAt: DateTimeOffset.MinValue,
            Error: error);
}
