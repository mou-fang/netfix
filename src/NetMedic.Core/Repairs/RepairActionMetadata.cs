using System.Collections.Immutable;

namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复动作运行时元数据。对应任务书 §7.3。
/// 阶段 1 的 <see cref="RepairActionDescriptor"/> 保留用于 UI 显示；
/// 本元数据供事务引擎在执行时使用。
/// </summary>
public sealed record RepairActionMetadata(
    string ActionId,
    string TitleKey,
    string DescriptionKey,
    string ConfirmationKey,
    RepairRiskLevel RiskLevel,
    RepairPrivilegeRequirement PrivilegeRequirement,
    bool SupportsRollback,
    bool IsSystemMutating,
    IReadOnlyList<string> ApplicableFindingIds,
    IReadOnlyList<string> VerificationProbeIds,
    TimeSpan EstimatedDuration)
{
    /// <summary>创建元数据的便捷工厂，接受可枚举参数。</summary>
    public static RepairActionMetadata Create(
        string actionId,
        string titleKey,
        string descriptionKey,
        string confirmationKey,
        RepairRiskLevel riskLevel,
        RepairPrivilegeRequirement privilegeRequirement,
        bool supportsRollback,
        bool isSystemMutating,
        IEnumerable<string>? applicableFindingIds = null,
        IEnumerable<string>? verificationProbeIds = null,
        TimeSpan? estimatedDuration = null)
        => new(
            ActionId: actionId,
            TitleKey: titleKey,
            DescriptionKey: descriptionKey,
            ConfirmationKey: confirmationKey,
            RiskLevel: riskLevel,
            PrivilegeRequirement: privilegeRequirement,
            SupportsRollback: supportsRollback,
            IsSystemMutating: isSystemMutating,
            ApplicableFindingIds: (applicableFindingIds ?? []).ToImmutableList(),
            VerificationProbeIds: (verificationProbeIds ?? []).ToImmutableList(),
            EstimatedDuration: estimatedDuration ?? TimeSpan.FromSeconds(1));
}
