namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 诊断规则接口。从快照事实生成 Finding。
/// 对应任务书 §6.1：规则实现为简单 C# 类。
/// </summary>
public interface IDiagnosisRule
{
    /// <summary>规则唯一标识。</summary>
    string Id { get; }

    /// <summary>评估快照，返回 Finding 或 null（不命中）。</summary>
    Finding? Evaluate(DiagnosticSnapshot snapshot);
}

/// <summary>
/// 固定规则注册表。对应任务书 §6.1：RuleRegistry 使用固定列表注册规则，
/// V1 不使用 YAML/JSON 解析器，不从网络下载规则，不动态加载 DLL。
/// </summary>
public sealed class RuleRegistry
{
    private readonly List<IDiagnosisRule> _rules = [];

    public IReadOnlyList<IDiagnosisRule> Rules => _rules;

    public RuleRegistry Add(IDiagnosisRule rule)
    {
        _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        return this;
    }

    /// <summary>
    /// 评估所有规则，返回命中的 Finding 列表，按推荐排序。
    /// 排序对应任务书 §6.4：
    /// 1. 高可信 > 中可信 > 证据不足
    /// 2. 有推荐动作的优先
    /// 3. 保护上下文降级的排后面
    /// 4. 同等级按 Id 字母序（保证稳定可重复）
    /// 同一故障不能产生多个互相矛盾的第一结论。
    /// </summary>
    public IReadOnlyList<Finding> EvaluateAll(DiagnosticSnapshot snapshot)
    {
        var findings = new List<Finding>();
        foreach (var rule in _rules)
        {
            var finding = rule.Evaluate(snapshot);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return findings
            .OrderByDescending(f => f.Confidence)
            .ThenByDescending(f => f.RecommendedActionId is not null)
            .ThenBy(f => f.ProtectedContextDowngrade)
            .ThenBy(f => f.Id)
            .ToList();
    }
}
