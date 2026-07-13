using System.Collections.Immutable;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 诊断会话模式。对应任务书 §5.1 两档体检。
/// </summary>
public enum DiagnosticMode
{
    /// <summary>快速体检：10-20 秒，只运行基础探针。</summary>
    Quick,

    /// <summary>深度体检：触发更多探针，通常 60 秒内。</summary>
    Deep,
}

/// <summary>
/// 用户选择的症状类型。对应任务书 §4.2 首页症状选择器。
/// </summary>
public enum SymptomCategory
{
    /// <summary>什么都打不开。</summary>
    NothingWorks,

    /// <summary>只有部分网站打不开。</summary>
    SomeSitesDown,

    /// <summary>只有一个应用/游戏打不开。</summary>
    SingleAppDown,

    /// <summary>代理或 VPN 关闭后不能上网。</summary>
    ProxyVpnOff,

    /// <summary>我不确定。</summary>
    Unsure,
}

/// <summary>
/// 诊断会话。记录症状、模式、时间和所有探针结果。
/// 对应任务书 §7.7 数据模型 DiagnosticSession。
/// </summary>
public sealed record DiagnosticSession(
    Guid Id,
    SymptomCategory Symptom,
    DiagnosticMode Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<ProbeResult> Results)
{
    public static DiagnosticSession Create(SymptomCategory symptom, DiagnosticMode mode)
        => new(
            Id: Guid.NewGuid(),
            Symptom: symptom,
            Mode: mode,
            StartedAt: DateTimeOffset.UtcNow,
            FinishedAt: DateTimeOffset.UtcNow,
            Results: ImmutableList<ProbeResult>.Empty);

    public DiagnosticSession WithResults(IReadOnlyList<ProbeResult> results)
        => this with { Results = results, FinishedAt = DateTimeOffset.UtcNow };
}

/// <summary>
/// 诊断快照。所有探针结果的聚合视图，供规则引擎评估。
/// 对应任务书 §7.7 数据模型 + §6.1 规则输入。
/// </summary>
public sealed record DiagnosticSnapshot(
    DiagnosticSession Session,
    IReadOnlyDictionary<string, ProbeResult> ResultsByProbeId)
{
    public static DiagnosticSnapshot From(DiagnosticSession session)
        => new(
            Session: session,
            ResultsByProbeId: session.Results.ToDictionary(r => r.Id, r => r));

    /// <summary>获取指定探针的结果；不存在返回 null。</summary>
    public ProbeResult? Get(string probeId)
        => ResultsByProbeId.TryGetValue(probeId, out var r) ? r : null;

    /// <summary>获取指定探针的状态；不存在返回 null。</summary>
    public ProbeStatus? GetStatus(string probeId)
        => Get(probeId)?.Status;
}
