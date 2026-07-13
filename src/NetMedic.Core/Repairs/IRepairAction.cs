namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复动作接口。对应任务书 §7.3。
/// 每个实现封装一种具体修复操作的计划生成、快照、执行、验证和回滚逻辑。
/// 阶段 4.0：无生产实现，仅 Testing 命名空间下的 Fake 实现。
/// </summary>
public interface IRepairAction
{
    /// <summary>该动作的运行时元数据。</summary>
    RepairActionMetadata Metadata { get; }

    /// <summary>
    /// 生成修复计划。基于当前诊断结果决定执行哪些步骤。
    /// 对应任务书 §7.4 计划阶段。
    /// </summary>
    ValueTask<RepairPlanResult> CreatePlanAsync(RepairContext context, CancellationToken cancellationToken);

    /// <summary>
    /// 捕获执行前系统状态快照，用于回滚。
    /// DryRun 模式下可返回空快照。
    /// 对应任务书 §7.4 快照阶段。
    /// </summary>
    ValueTask<RepairSnapshotResult> CaptureSnapshotAsync(RepairContext context, RepairPlan plan, CancellationToken cancellationToken);

    /// <summary>
    /// 执行修复步骤。
    /// 对应任务书 §7.4 执行阶段。
    /// 阶段 4.0：DryRun 模式不修改系统。
    /// </summary>
    ValueTask<RepairStepResult> ExecuteAsync(RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// 验证修复是否生效。
    /// 对应任务书 §7.4 验证阶段。
    /// </summary>
    ValueTask<RepairVerificationResult> VerifyAsync(RepairContext context, RepairPlan plan, CancellationToken cancellationToken);

    /// <summary>
    /// 回滚到快照记录的状态。
    /// 仅当 <see cref="RepairActionMetadata.SupportsRollback"/> 为 true 时调用。
    /// 对应任务书 §7.4 回滚阶段。
    /// </summary>
    ValueTask<RepairStepResult> RollbackAsync(RepairContext context, RepairSnapshot snapshot, CancellationToken cancellationToken);
}
