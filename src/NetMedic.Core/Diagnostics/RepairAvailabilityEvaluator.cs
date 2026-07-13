namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 纯函数：判断一个 Finding 是否可以执行安全修复。
/// 阶段 3：ExecutableRepairActions 为空，结果恒为 false。
/// 阶段 4：由真实 IRepairAction 注册后才加入集合。
/// </summary>
public static class RepairAvailabilityEvaluator
{
    public static bool CanExecute(Finding? finding, IReadOnlySet<string> executableActionIds)
    {
        if (finding is null) return false;
        if (finding.RecommendedActionId is null) return false;
        return executableActionIds.Contains(finding.RecommendedActionId);
    }
}
