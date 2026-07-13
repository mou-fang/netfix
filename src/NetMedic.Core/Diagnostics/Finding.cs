using System.Collections.Immutable;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 诊断结论。对应任务书 §6.2 Finding 模型。
/// 用户只看到高可信/可能/证据不足三个等级，不显示虚假百分比。
/// </summary>
public sealed record Finding(
    string Id,
    Confidence Confidence,
    string TitleKey,
    string ExplanationKey,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> CounterEvidenceIds,
    string? RecommendedActionId = null)
{
    public static Finding Create(
        string id,
        Confidence confidence,
        string titleKey,
        string explanationKey,
        string? recommendedActionId = null,
        IEnumerable<string>? evidenceIds = null,
        IEnumerable<string>? counterEvidenceIds = null)
        => new(
            Id: id,
            Confidence: confidence,
            TitleKey: titleKey,
            ExplanationKey: explanationKey,
            EvidenceIds: (evidenceIds ?? []).ToImmutableList(),
            CounterEvidenceIds: (counterEvidenceIds ?? []).ToImmutableList(),
            RecommendedActionId: recommendedActionId);
}
