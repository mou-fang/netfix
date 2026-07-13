namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复计划生成结果。
/// 成功时携带 <see cref="Plan"/>；失败时携带 <see cref="Failure"/>。
/// </summary>
public sealed record RepairPlanResult(
    bool Success,
    RepairPlan? Plan,
    RepairFailure? Failure)
{
    /// <summary>成功结果。</summary>
    public static RepairPlanResult Ok(RepairPlan plan) => new(true, plan, null);

    /// <summary>失败结果。</summary>
    public static RepairPlanResult Err(RepairFailure failure) => new(false, null, failure);
}
