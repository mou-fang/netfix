namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复执行模式。对应任务书 §7.4。
/// 阶段 4.0：仅 DryRun（模拟/空跑），不执行任何真实系统修改。
/// </summary>
public enum RepairExecutionMode
{
    /// <summary>
    /// 干跑/模拟模式：执行计划、快照、验证流程，但不修改系统。
    /// 阶段 4.0 唯一允许的模式。
    /// </summary>
    DryRun,

    /// <summary>
    /// 真实执行模式：调用真实系统修改逻辑。
    /// 阶段 4.0：生产修复动作目录为空，此模式不可用。
    /// </summary>
    Real,
}
