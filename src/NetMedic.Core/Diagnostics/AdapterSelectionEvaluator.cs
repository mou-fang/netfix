using System.Collections.Immutable;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// NET-01 多网卡选择的纯函数评估结果。
/// 对应任务书 §5.3 NET-01：当多张网卡同时活动时，根据默认网关确定主接口。
/// 真实 AdapterProbe 与测试共同调用此函数，保证选择逻辑一致。
/// </summary>
public sealed record AdapterSelectionResult(
    string? PrimaryAdapter,
    ProbeStatus Status,
    IReadOnlyList<string> CandidatesWithGateway);

/// <summary>
/// NET-01 多网卡选择纯函数。
/// 输入 (adapterName, hasGateway) 列表，返回主接口与状态：
/// - 恰好一个有网关 -> (adapterName, Passed)
/// - 多个有网关 -> ("ambiguous", Warning)
/// - 没有有网关 -> (null, Failed)
/// </summary>
public static class AdapterSelectionEvaluator
{
    public static AdapterSelectionResult Evaluate(IReadOnlyList<(string Name, bool HasGateway)> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var candidatesWithGateway = adapters
            .Where(a => a.HasGateway)
            .Select(a => a.Name)
            .ToImmutableList();

        return candidatesWithGateway.Count switch
        {
            0 => new AdapterSelectionResult(null, ProbeStatus.Failed, candidatesWithGateway),
            1 => new AdapterSelectionResult(candidatesWithGateway[0], ProbeStatus.Passed, candidatesWithGateway),
            _ => new AdapterSelectionResult("ambiguous", ProbeStatus.Warning, candidatesWithGateway),
        };
    }
}
