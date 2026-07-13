namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复动作风险等级。对应任务书 §7.1。
/// 与 <see cref="RepairRisk"/> 枚举语义一致，但用于运行时事务执行模型（阶段 4）。
/// 阶段 1 的 <see cref="RepairRisk"/> 保留用于描述符，不破坏既有引用。
/// </summary>
public enum RepairRiskLevel
{
    /// <summary>低风险：影响小、可回滚、通常不断网。</summary>
    Low,

    /// <summary>中风险：可能短暂断网或改变行为，但可备份恢复。</summary>
    Medium,

    /// <summary>高风险：可能需重启、影响多个网卡/协议、不能完整自动回滚。</summary>
    High,
}
