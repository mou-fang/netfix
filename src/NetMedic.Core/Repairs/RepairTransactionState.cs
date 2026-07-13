namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复事务状态机。对应任务书 §7.4 事务生命周期。
/// 状态转换由 <see cref="RepairTransactionEngine"/> 严格强制。
/// </summary>
public enum RepairTransactionState
{
    /// <summary>事务已创建，尚未开始规划。</summary>
    Created,

    /// <summary>正在生成修复计划。</summary>
    Planning,

    /// <summary>计划已就绪，等待用户确认。</summary>
    PlanReady,

    /// <summary>已请求用户确认，等待用户响应。</summary>
    AwaitingConfirmation,

    /// <summary>正在捕获执行前快照。</summary>
    CapturingSnapshot,

    /// <summary>正在执行修复步骤。</summary>
    Executing,

    /// <summary>正在执行验证探针，确认修复是否生效。</summary>
    Verifying,

    /// <summary>修复成功完成。</summary>
    Succeeded,

    /// <summary>修复失败（且不可回滚或回滚未尝试）。</summary>
    Failed,

    /// <summary>正在执行回滚。</summary>
    RollingBack,

    /// <summary>回滚成功完成。</summary>
    RolledBack,

    /// <summary>回滚失败（系统可能处于不一致状态）。</summary>
    RollbackFailed,

    /// <summary>用户在执行前取消。</summary>
    Cancelled,
}
