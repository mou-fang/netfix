namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复事务。对应任务书 §7.4 事务模型。
/// 不可变记录，由 <see cref="RepairTransactionEngine"/> 在每次状态转换后生成新实例。
/// </summary>
public sealed record RepairTransaction(
    string TransactionId,
    string ActionId,
    RepairContext Context,
    RepairTransactionState State,
    RepairPlan? Plan,
    RepairSnapshot? Snapshot,
    RepairVerificationResult? VerificationResult,
    RepairAuditEntry AuditEntry,
    RepairFailure? Failure)
{
    /// <summary>创建事务初始状态（Created）。</summary>
    public static RepairTransaction Create(
        string transactionId,
        string actionId,
        RepairContext context,
        RepairPrivilegeRequirement privilegeRequirement)
    {
        var audit = RepairAuditEntry.Start(
            context.CorrelationId,
            actionId,
            context.ExecutionMode,
            context.StartedAt,
            privilegeRequirement,
            context.IsElevated);

        return new RepairTransaction(
            TransactionId: transactionId,
            ActionId: actionId,
            Context: context,
            State: RepairTransactionState.Created,
            Plan: null,
            Snapshot: null,
            VerificationResult: null,
            AuditEntry: audit,
            Failure: null);
    }
}
