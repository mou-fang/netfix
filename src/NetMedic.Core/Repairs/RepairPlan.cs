using System.Collections.Immutable;

namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复计划。对应任务书 §7.4。
/// 由 <see cref="IRepairAction.CreatePlanAsync"/> 生成，供事务引擎和用户确认使用。
/// </summary>
public sealed record RepairPlan(
    string PlanId,
    string ActionId,
    IReadOnlyList<RepairStep> Steps,
    RepairRiskLevel RiskLevel,
    RepairPrivilegeRequirement PrivilegeRequirement,
    bool WillModifySystem,
    bool RequiresRestart,
    bool RequiresNetworkReconnect,
    IReadOnlyList<string> VerificationProbeIds,
    IReadOnlyList<string> UserVisibleWarnings)
{
    /// <summary>创建计划的便捷工厂，接受可枚举参数。</summary>
    public static RepairPlan Create(
        string planId,
        string actionId,
        IEnumerable<RepairStep> steps,
        RepairRiskLevel riskLevel,
        RepairPrivilegeRequirement privilegeRequirement,
        bool willModifySystem,
        bool requiresRestart = false,
        bool requiresNetworkReconnect = false,
        IEnumerable<string>? verificationProbeIds = null,
        IEnumerable<string>? userVisibleWarnings = null)
        => new(
            PlanId: planId,
            ActionId: actionId,
            Steps: steps.ToImmutableList(),
            RiskLevel: riskLevel,
            PrivilegeRequirement: privilegeRequirement,
            WillModifySystem: willModifySystem,
            RequiresRestart: requiresRestart,
            RequiresNetworkReconnect: requiresNetworkReconnect,
            VerificationProbeIds: (verificationProbeIds ?? []).ToImmutableList(),
            UserVisibleWarnings: (userVisibleWarnings ?? []).ToImmutableList());
}
