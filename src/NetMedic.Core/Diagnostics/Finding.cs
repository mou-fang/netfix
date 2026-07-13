using System.Collections.Immutable;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// Finding 严重程度。
/// </summary>
public enum FindingSeverity
{
    /// <summary>信息性：网络正常或仅为辅助信号。</summary>
    Info,

    /// <summary>低：可能影响部分功能，但有明确安全修复方案。</summary>
    Low,

    /// <summary>中：影响网络访问，需要用户关注。</summary>
    Medium,

    /// <summary>高：很可能导致网络故障，需要优先处理。</summary>
    High,
}

/// <summary>
/// 诊断结论。对应任务书 §6.2 Finding 模型。
/// 用户只看到高可信/可能/证据不足三个等级，不显示虚假百分比。
/// 阶段 3 扩展：增加严重程度、普通用户文案 key、保护上下文降级标记。
/// </summary>
public sealed record Finding(
    string Id,
    Confidence Confidence,
    FindingSeverity Severity,
    string TitleKey,
    string ExplanationKey,
    string UserSummaryKey,
    string? GuidanceKey,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> CounterEvidenceIds,
    string? RecommendedActionId = null,
    bool ProtectedContextDowngrade = false)
{
    public static Finding Create(
        string id,
        Confidence confidence,
        FindingSeverity severity,
        string titleKey,
        string explanationKey,
        string userSummaryKey,
        string? guidanceKey = null,
        string? recommendedActionId = null,
        IEnumerable<string>? evidenceIds = null,
        IEnumerable<string>? counterEvidenceIds = null,
        bool protectedContextDowngrade = false)
        => new(
            Id: id,
            Confidence: confidence,
            Severity: severity,
            TitleKey: titleKey,
            ExplanationKey: explanationKey,
            UserSummaryKey: userSummaryKey,
            GuidanceKey: guidanceKey,
            EvidenceIds: (evidenceIds ?? []).ToImmutableList(),
            CounterEvidenceIds: (counterEvidenceIds ?? []).ToImmutableList(),
            RecommendedActionId: recommendedActionId,
            ProtectedContextDowngrade: protectedContextDowngrade);
}
