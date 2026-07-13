namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复事务引擎。对应任务书 §7.4 事务状态机。
/// 严格强制状态转换规则，确保：
/// - 无确认不执行
/// - 无快照不执行
/// - 执行失败自动回滚（若支持回滚）
/// - Real 模式在生产目录为空时拒绝
/// 阶段 4.0：仅支持 DryRun（模拟/空跑）。
/// </summary>
public sealed class RepairTransactionEngine
{
    /// <summary>失败代码常量。</summary>
    public const string FailureActionNotFound = "ACTION_NOT_FOUND";
    public const string FailurePlanGeneration = "PLAN_GENERATION_FAILED";
    public const string FailureSnapshotCapture = "SNAPSHOT_CAPTURE_FAILED";
    public const string FailureExecution = "EXECUTION_FAILED";
    public const string FailureVerification = "VERIFICATION_FAILED";
    public const string FailureRollback = "ROLLBACK_FAILED";
    public const string FailureRealExecutionNotEnabled = "REAL_EXECUTION_NOT_ENABLED";
    public const string FailureElevationRequired = "ELEVATION_REQUIRED";
    public const string FailureElevationDenied = "ELEVATION_DENIED";
    public const string FailureNotConfirmed = "NOT_CONFIRMED";
    public const string FailureNoSnapshot = "NO_SNAPSHOT";

    private readonly RepairActionCatalog _catalog;
    private RepairTransaction? _transaction;
    private readonly Func<string> _idGenerator;

    /// <summary>当前事务（可能为 null）。</summary>
    public RepairTransaction? Transaction => _transaction;

    /// <summary>使用指定目录和 ID 生成器创建引擎。</summary>
    public RepairTransactionEngine(RepairActionCatalog catalog, Func<string>? idGenerator = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _idGenerator = idGenerator ?? DefaultIdGenerator;
    }

    private static string DefaultIdGenerator() => Guid.NewGuid().ToString("N");

    // === 状态转换 ===

    /// <summary>
    /// 创建事务，进入 Created 状态。
    /// </summary>
    public RepairTransaction CreateTransaction(string actionId, RepairContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var action = _catalog.GetAction(actionId);
        if (action is null)
        {
            throw new InvalidOperationException(
                $"Repair action '{actionId}' is not registered in the catalog.");
        }

        _transaction = RepairTransaction.Create(
            transactionId: _idGenerator(),
            actionId: actionId,
            context: context,
            privilegeRequirement: action.Metadata.PrivilegeRequirement);

        return _transaction;
    }

    /// <summary>
    /// 生成修复计划。Created -> Planning -> PlanReady/Failed。
    /// </summary>
    public async ValueTask<RepairTransaction> PlanAsync(CancellationToken cancellationToken)
    {
        var tx = RequireTransaction();
        AssertState(tx.State, RepairTransactionState.Created, nameof(PlanAsync));

        var action = _catalog.GetRequiredAction(tx.ActionId);

        // Real 模式在目录为空时拒绝
        if (tx.Context.ExecutionMode == RepairExecutionMode.Real && _catalog.IsEmpty)
        {
            _transaction = FailTransaction(tx, FailureRealExecutionNotEnabled, "repair.failure.real_not_enabled");
            return _transaction;
        }

        // 权限检查
        var elevation = PrivilegeEvaluator.Evaluate(
            action.Metadata, tx.Context, realExecutionEnabled: !_catalog.IsEmpty);
        if (elevation == ElevationDecision.NotEnabled)
        {
            _transaction = FailTransaction(tx, FailureRealExecutionNotEnabled, "repair.failure.real_not_enabled");
            return _transaction;
        }
        if (elevation == ElevationDecision.Required)
        {
            _transaction = FailTransaction(tx, FailureElevationRequired, "repair.failure.elevation_required");
            return _transaction;
        }
        if (elevation == ElevationDecision.Denied)
        {
            _transaction = FailTransaction(tx, FailureElevationDenied, "repair.failure.elevation_denied");
            return _transaction;
        }

        _transaction = tx with { State = RepairTransactionState.Planning };
        tx = _transaction;

        var planResult = await action.CreatePlanAsync(tx.Context, cancellationToken).ConfigureAwait(false);

        if (!planResult.Success || planResult.Plan is null)
        {
            var failure = planResult.Failure
                ?? RepairFailure.Create(FailurePlanGeneration, "repair.failure.plan_failed");
            _transaction = FailTransaction(tx, failure.Code, failure.MessageKey, failure.Detail);
            return _transaction;
        }

        _transaction = tx with
        {
            State = RepairTransactionState.PlanReady,
            Plan = planResult.Plan,
        };
        return _transaction;
    }

    /// <summary>
    /// 用户确认计划。PlanReady -> AwaitingConfirmation -> CapturingSnapshot（或 Cancelled）。
    /// </summary>
    public RepairTransaction Confirm()
    {
        var tx = RequireTransaction();
        AssertState(tx.State, RepairTransactionState.PlanReady, nameof(Confirm));

        // 转入 AwaitingConfirmation，然后立即进入 CapturingSnapshot
        // （用户已确认即代表同意执行）
        var confirmedContext = tx.Context with { UserConfirmed = true };

        _transaction = tx with
        {
            Context = confirmedContext,
            State = RepairTransactionState.AwaitingConfirmation,
        };

        // 立即推进到 CapturingSnapshot
        _transaction = _transaction with { State = RepairTransactionState.CapturingSnapshot };
        return _transaction;
    }

    /// <summary>
    /// 捕获执行前快照。CapturingSnapshot -> Executing/Failed。
    /// </summary>
    public async ValueTask<RepairTransaction> CaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        var tx = RequireTransaction();
        AssertState(tx.State, RepairTransactionState.CapturingSnapshot, nameof(CaptureSnapshotAsync));

        // 强制：无确认不执行
        if (!tx.Context.UserConfirmed)
        {
            _transaction = FailTransaction(tx, FailureNotConfirmed, "repair.failure.not_confirmed");
            return _transaction;
        }

        var action = _catalog.GetRequiredAction(tx.ActionId);
        var plan = tx.Plan ?? throw new InvalidOperationException("Plan must not be null during snapshot capture.");

        var snapshotResult = await action.CaptureSnapshotAsync(tx.Context, plan, cancellationToken).ConfigureAwait(false);

        if (!snapshotResult.Success || snapshotResult.Snapshot is null)
        {
            var failure = snapshotResult.Failure
                ?? RepairFailure.Create(FailureSnapshotCapture, "repair.failure.snapshot_failed");
            _transaction = FailTransaction(tx, failure.Code, failure.MessageKey, failure.Detail);
            return _transaction;
        }

        _transaction = tx with
        {
            Snapshot = snapshotResult.Snapshot,
            State = RepairTransactionState.Executing,
            AuditEntry = tx.AuditEntry with { SnapshotCaptured = true },
        };
        return _transaction;
    }

    /// <summary>
    /// 执行修复。Executing -> Verifying/RollingBack/Failed。
    /// </summary>
    public async ValueTask<RepairTransaction> ExecuteAsync(CancellationToken cancellationToken)
    {
        var tx = RequireTransaction();
        AssertState(tx.State, RepairTransactionState.Executing, nameof(ExecuteAsync));

        // 强制：无确认不执行
        if (!tx.Context.UserConfirmed)
        {
            _transaction = FailTransaction(tx, FailureNotConfirmed, "repair.failure.not_confirmed");
            return _transaction;
        }

        // 强制：无快照不执行
        if (tx.Snapshot is null)
        {
            _transaction = FailTransaction(tx, FailureNoSnapshot, "repair.failure.no_snapshot");
            return _transaction;
        }

        var action = _catalog.GetRequiredAction(tx.ActionId);
        var plan = tx.Plan ?? throw new InvalidOperationException("Plan must not be null during execution.");

        _transaction = tx with
        {
            AuditEntry = tx.AuditEntry with { ExecutionAttempted = true },
        };
        tx = _transaction;

        var execResult = await action.ExecuteAsync(tx.Context, plan, tx.Snapshot, cancellationToken).ConfigureAwait(false);

        if (!execResult.Success)
        {
            var failure = execResult.Failure
                ?? RepairFailure.Create(FailureExecution, "repair.failure.execution_failed");

            // 自动回滚（若支持回滚）
            if (action.Metadata.SupportsRollback)
            {
                _transaction = tx with
                {
                    State = RepairTransactionState.RollingBack,
                    Failure = failure,
                    AuditEntry = tx.AuditEntry with { RollbackAttempted = true },
                };
                return _transaction;
            }

            // 不支持回滚，直接失败
            _transaction = FailTransaction(tx, failure.Code, failure.MessageKey, failure.Detail);
            return _transaction;
        }

        _transaction = tx with { State = RepairTransactionState.Verifying };
        return _transaction;
    }

    /// <summary>
    /// 验证修复结果。Verifying -> Succeeded/RollingBack。
    /// </summary>
    public async ValueTask<RepairTransaction> VerifyAsync(CancellationToken cancellationToken)
    {
        var tx = RequireTransaction();
        AssertState(tx.State, RepairTransactionState.Verifying, nameof(VerifyAsync));

        var action = _catalog.GetRequiredAction(tx.ActionId);
        var plan = tx.Plan ?? throw new InvalidOperationException("Plan must not be null during verification.");

        _transaction = tx with
        {
            AuditEntry = tx.AuditEntry with { VerificationAttempted = true },
        };
        tx = _transaction;

        var verifyResult = await action.VerifyAsync(tx.Context, plan, cancellationToken).ConfigureAwait(false);

        _transaction = tx with { VerificationResult = verifyResult };

        if (!verifyResult.Verified)
        {
            var failure = verifyResult.Failure
                ?? RepairFailure.Create(FailureVerification, "repair.failure.verification_failed");

            // 验证失败：若支持回滚则自动回滚
            if (action.Metadata.SupportsRollback)
            {
                _transaction = _transaction with
                {
                    State = RepairTransactionState.RollingBack,
                    Failure = failure,
                    AuditEntry = tx.AuditEntry with { RollbackAttempted = true },
                };
                return _transaction;
            }

            _transaction = FailTransaction(tx, failure.Code, failure.MessageKey, failure.Detail);
            return _transaction;
        }

        _transaction = CompleteTransaction(tx, RepairTransactionState.Succeeded);
        return _transaction;
    }

    /// <summary>
    /// 执行回滚。RollingBack -> RolledBack/RollbackFailed。
    /// </summary>
    public async ValueTask<RepairTransaction> RollbackAsync(CancellationToken cancellationToken)
    {
        var tx = RequireTransaction();
        AssertState(tx.State, RepairTransactionState.RollingBack, nameof(RollbackAsync));

        var action = _catalog.GetRequiredAction(tx.ActionId);
        var snapshot = tx.Snapshot
            ?? throw new InvalidOperationException("Snapshot must not be null during rollback.");

        var rollbackResult = await action.RollbackAsync(tx.Context, snapshot, cancellationToken).ConfigureAwait(false);

        if (!rollbackResult.Success)
        {
            var failure = rollbackResult.Failure
                ?? RepairFailure.Create(FailureRollback, "repair.failure.rollback_failed");
            _transaction = FailTransaction(tx, failure.Code, failure.MessageKey, failure.Detail,
                finalState: RepairTransactionState.RollbackFailed);
            return _transaction;
        }

        _transaction = CompleteTransaction(tx, RepairTransactionState.RolledBack);
        return _transaction;
    }

    /// <summary>
    /// 取消事务。仅可从 Created/Planning/PlanReady/AwaitingConfirmation/CapturingSnapshot 取消。
    /// </summary>
    public RepairTransaction Cancel()
    {
        var tx = RequireTransaction();

        var cancellableStates = new[]
        {
            RepairTransactionState.Created,
            RepairTransactionState.Planning,
            RepairTransactionState.PlanReady,
            RepairTransactionState.AwaitingConfirmation,
            RepairTransactionState.CapturingSnapshot,
        };

        if (!cancellableStates.Contains(tx.State))
        {
            throw new InvalidOperationException(
                $"Cannot cancel transaction in state '{tx.State}'. " +
                $"Cancellation is only allowed from Created, Planning, PlanReady, AwaitingConfirmation, or CapturingSnapshot.");
        }

        _transaction = CompleteTransaction(tx, RepairTransactionState.Cancelled);
        return _transaction;
    }

    // === 内部辅助 ===

    private RepairTransaction RequireTransaction()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException(
                "No active transaction. Call CreateTransaction first.");
        }
        return _transaction;
    }

    private static void AssertState(
        RepairTransactionState actual,
        RepairTransactionState expected,
        string methodName)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"{methodName} requires state '{expected}', but transaction is in state '{actual}'.");
        }
    }

    private RepairTransaction FailTransaction(
        RepairTransaction tx,
        string failureCode,
        string messageKey,
        string? detail = null,
        RepairTransactionState? finalState = null)
    {
        var state = finalState ?? RepairTransactionState.Failed;
        return tx with
        {
            State = state,
            Failure = RepairFailure.Create(failureCode, messageKey, detail),
            AuditEntry = tx.AuditEntry with
            {
                FinalState = state,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureCode = failureCode,
            },
        };
    }

    private static RepairTransaction CompleteTransaction(
        RepairTransaction tx,
        RepairTransactionState finalState)
    {
        return tx with
        {
            State = finalState,
            AuditEntry = tx.AuditEntry with
            {
                FinalState = finalState,
                CompletedAt = DateTimeOffset.UtcNow,
            },
        };
    }
}
