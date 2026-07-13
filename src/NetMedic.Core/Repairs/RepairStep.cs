namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复计划中的单个步骤。对应任务书 §7.4 计划步骤。
/// </summary>
public sealed record RepairStep(
    int Order,
    string TitleKey,
    bool IsSystemMutating,
    bool IsRollbackable,
    RepairPrivilegeRequirement PrivilegeRequirement,
    string? SimulationNote = null);
