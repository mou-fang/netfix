namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复事务审计条目。对应任务书 §7.5 审计日志。
/// 记录事务完整生命周期的关键事件，用于事后追溯和合规检查。
/// 所有字段均可空或默认值，以便在事务各阶段逐步填充。
/// </summary>
public sealed record RepairAuditEntry(
    string CorrelationId,
    string ActionId,
    RepairExecutionMode ExecutionMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    RepairTransactionState InitialState,
    RepairTransactionState FinalState,
    RepairPrivilegeRequirement PrivilegeRequirement,
    bool WasElevated,
    bool SnapshotCaptured,
    bool ExecutionAttempted,
    bool VerificationAttempted,
    bool RollbackAttempted,
    string? FailureCode)
{
    /// <summary>创建一个事务开始时的初始审计条目。</summary>
    public static RepairAuditEntry Start(
        string correlationId,
        string actionId,
        RepairExecutionMode executionMode,
        DateTimeOffset startedAt,
        RepairPrivilegeRequirement privilegeRequirement,
        bool wasElevated)
        => new(
            CorrelationId: correlationId,
            ActionId: actionId,
            ExecutionMode: executionMode,
            StartedAt: startedAt,
            CompletedAt: null,
            InitialState: RepairTransactionState.Created,
            FinalState: RepairTransactionState.Created,
            PrivilegeRequirement: privilegeRequirement,
            WasElevated: wasElevated,
            SnapshotCaptured: false,
            ExecutionAttempted: false,
            VerificationAttempted: false,
            RollbackAttempted: false,
            FailureCode: null);
}
