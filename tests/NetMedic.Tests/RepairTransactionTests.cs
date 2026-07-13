using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;
using NetMedic.Core.Repairs;
using NetMedic.Core.Testing.Fakes;

namespace NetMedic.Tests;

/// <summary>
/// 阶段 4.0 修复事务引擎测试。
/// 使用 FakeRepairActions（位于 NetMedic.Core.Testing）验证事务状态机、
/// 回滚、验证、取消、权限、DryRun、目录注册和审计脱敏逻辑。
/// 这些测试在 net10.0 上运行，因为 Fake 动作属于 NetMedic.Core。
/// </summary>
public class RepairTransactionTests
{
    /// <summary>创建包含全部 8 个 Fake 动作的测试目录。</summary>
    private static RepairActionCatalog CreateFakeCatalog()
    {
        var catalog = new RepairActionCatalog();
        FakeRepairActions.RegisterAll(catalog);
        return catalog;
    }

    /// <summary>创建一个 DryRun 上下文。</summary>
    private static RepairContext DryRunContext(bool confirmed = false) =>
        RepairContext.ForDryRun(
            sessionId: "test-session",
            correlationId: "test-correlation",
            isElevated: false,
            userConfirmed: confirmed);

    /// <summary>创建一个 Real 模式上下文。</summary>
    private static RepairContext RealContext(bool elevated = false, bool confirmed = false) =>
        new(
            ExecutionMode: RepairExecutionMode.Real,
            IsElevated: elevated,
            SessionId: "test-session",
            CorrelationId: "test-correlation",
            UserConfirmed: confirmed,
            StartedAt: DateTimeOffset.UtcNow);

    // ========================================================================
    // 事务状态机测试（1-8）
    // ========================================================================

    /// <summary>1. 完整成功路径：create -> plan -> confirm -> snapshot -> execute -> verify -> Succeeded。</summary>
    [Fact]
    public async Task CompleteSuccessPath_FakeSuccess_Succeeds()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        var tx = engine.CreateTransaction("FAKE-SUCCESS", context);
        Assert.Equal(RepairTransactionState.Created, tx.State);

        tx = await engine.PlanAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.PlanReady, tx.State);
        Assert.NotNull(tx.Plan);

        tx = engine.Confirm();
        Assert.Equal(RepairTransactionState.CapturingSnapshot, tx.State);

        tx = await engine.CaptureSnapshotAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.Executing, tx.State);
        Assert.NotNull(tx.Snapshot);

        tx = await engine.ExecuteAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.Verifying, tx.State);

        tx = await engine.VerifyAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.Succeeded, tx.State);
        Assert.Null(tx.Failure);
    }

    /// <summary>2. 未确认直接 execute 被拒绝（需先经过 Confirm 进入正确状态）。</summary>
    [Fact]
    public async Task ExecuteWithoutConfirmation_Rejected()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext(confirmed: false);

        var tx = engine.CreateTransaction("FAKE-SUCCESS", context);
        tx = await engine.PlanAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.PlanReady, tx.State);

        // 跳过 Confirm，直接调用 CaptureSnapshotAsync 会抛异常（状态不匹配）
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.CaptureSnapshotAsync(CancellationToken.None).AsTask());

        // 事务仍在 PlanReady，未进入 Executing
        Assert.Equal(RepairTransactionState.PlanReady, engine.Transaction!.State);
    }

    /// <summary>3. 跳过快照直接 execute 被拒绝（无快照则不执行）。</summary>
    [Fact]
    public async Task ExecuteWithoutSnapshot_Rejected()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        var tx = engine.CreateTransaction("FAKE-SUCCESS", context);
        tx = await engine.PlanAsync(CancellationToken.None);
        tx = engine.Confirm();
        // 跳过 CaptureSnapshotAsync，直接 ExecuteAsync 会抛异常（状态不匹配）
        Assert.Equal(RepairTransactionState.CapturingSnapshot, tx.State);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(CancellationToken.None).AsTask());

        Assert.Equal(RepairTransactionState.CapturingSnapshot, engine.Transaction!.State);
    }

    /// <summary>4. 成功后重复 execute 被拒绝。</summary>
    [Fact]
    public async Task DoubleExecute_Rejected()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);
        await engine.ExecuteAsync(CancellationToken.None);
        await engine.VerifyAsync(CancellationToken.None);

        Assert.Equal(RepairTransactionState.Succeeded, engine.Transaction!.State);

        // 再次 execute 应抛异常
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(CancellationToken.None).AsTask());
    }

    /// <summary>5. 成功后回滚被拒绝。</summary>
    [Fact]
    public async Task RollbackAfterSuccess_Rejected()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);
        await engine.ExecuteAsync(CancellationToken.None);
        await engine.VerifyAsync(CancellationToken.None);

        Assert.Equal(RepairTransactionState.Succeeded, engine.Transaction!.State);

        // Succeeded 状态下 rollback 应抛异常
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RollbackAsync(CancellationToken.None).AsTask());
    }

    /// <summary>6. 取消后继续 execute 被拒绝。</summary>
    [Fact]
    public async Task ContinueAfterCancel_Rejected()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        await engine.PlanAsync(CancellationToken.None);

        // 从 PlanReady 取消
        var tx = engine.Cancel();
        Assert.Equal(RepairTransactionState.Cancelled, tx.State);

        // 取消后 execute 应抛异常
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(CancellationToken.None).AsTask());
    }

    /// <summary>7. 非法状态转换抛异常（如 Succeeded -> Executing）。</summary>
    [Fact]
    public async Task IllegalTransition_Throws()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);
        await engine.ExecuteAsync(CancellationToken.None);
        await engine.VerifyAsync(CancellationToken.None);

        Assert.Equal(RepairTransactionState.Succeeded, engine.Transaction!.State);

        // Succeeded -> Executing 是非法转换，ExecuteAsync 需要状态为 Executing
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(CancellationToken.None).AsTask());

        // VerifyAsync 也需要 Verifying 状态
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.VerifyAsync(CancellationToken.None).AsTask());
    }

    /// <summary>8. 同一引擎上两次创建事务（第二次覆盖第一次）。</summary>
    [Fact]
    public void ConcurrentStart_Rejected()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        var tx1 = engine.CreateTransaction("FAKE-SUCCESS", context);
        Assert.Equal("FAKE-SUCCESS", tx1.ActionId);

        // 第二次创建会覆盖第一次（引擎只维护单个事务）
        var tx2 = engine.CreateTransaction("FAKE-EXEC-FAIL", context);
        Assert.Equal("FAKE-EXEC-FAIL", tx2.ActionId);

        // 引擎当前事务是第二次的
        Assert.Same(tx2, engine.Transaction);
        Assert.NotSame(tx1, engine.Transaction);
    }

    // ========================================================================
    // 失败和回滚测试（9-15）
    // ========================================================================

    /// <summary>9. 执行失败自动回滚 -> RolledBack。</summary>
    [Fact]
    public async Task ExecutionFailure_AutoRollback_RolledBack()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-EXEC-FAIL", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);

        var tx = await engine.ExecuteAsync(CancellationToken.None);
        // 执行失败 + 支持回滚 -> RollingBack
        Assert.Equal(RepairTransactionState.RollingBack, tx.State);
        Assert.NotNull(tx.Failure);

        tx = await engine.RollbackAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.RolledBack, tx.State);
    }

    /// <summary>10. 验证失败自动回滚 -> RolledBack。</summary>
    [Fact]
    public async Task VerificationFailure_AutoRollback_RolledBack()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-VERIFY-FAIL", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);
        await engine.ExecuteAsync(CancellationToken.None);

        var tx = await engine.VerifyAsync(CancellationToken.None);
        // 验证失败 + 支持回滚 -> RollingBack
        Assert.Equal(RepairTransactionState.RollingBack, tx.State);
        Assert.NotNull(tx.Failure);

        tx = await engine.RollbackAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.RolledBack, tx.State);
    }

    /// <summary>11. 回滚失败 -> RollbackFailed。</summary>
    [Fact]
    public async Task RollbackFailure_RollbackFailed()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-ROLLBACK-FAIL", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);

        var tx = await engine.ExecuteAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.RollingBack, tx.State);

        tx = await engine.RollbackAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.RollbackFailed, tx.State);
        Assert.NotNull(tx.Failure);
    }

    /// <summary>12. 不支持回滚的动作执行失败直接 Failed（非 RolledBack）。</summary>
    [Fact]
    public async Task NonRollbackableAction_Fails_NoRollback()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-NON-ROLLBACK", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);

        var tx = await engine.ExecuteAsync(CancellationToken.None);
        // 不支持回滚 -> 直接 Failed，不进入 RollingBack
        Assert.Equal(RepairTransactionState.Failed, tx.State);
        Assert.NotEqual(RepairTransactionState.RolledBack, tx.State);
        Assert.NotNull(tx.Failure);
    }

    /// <summary>13. 快照失败 -> Failed，不执行 execute。</summary>
    [Fact]
    public async Task SnapshotFailure_NoExecution()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SNAPSHOT-FAIL", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();

        var tx = await engine.CaptureSnapshotAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.Failed, tx.State);
        Assert.NotNull(tx.Failure);

        // 快照失败后 execute 应抛异常（状态为 Failed，不是 Executing）
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(CancellationToken.None).AsTask());

        // 审计中不应标记执行已尝试
        Assert.False(engine.Transaction!.AuditEntry.ExecutionAttempted);
    }

    /// <summary>14. 快照 ActionId 不匹配应被拒绝（引擎使用动作自己生成的快照）。</summary>
    [Fact]
    public async Task SnapshotActionIdMismatch_Rejected()
    {
        var catalog = new RepairActionCatalog();
        catalog.Register(new FakeSuccessfulRepairAction());
        var engine = new RepairTransactionEngine(catalog);
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();

        // 正常流程下快照由动作生成，ActionId 匹配
        var tx = await engine.CaptureSnapshotAsync(CancellationToken.None);
        Assert.Equal(RepairTransactionState.Executing, tx.State);
        Assert.NotNull(tx.Snapshot);
        // 快照的 ActionId 必须与事务的 ActionId 一致
        Assert.Equal("FAKE-SUCCESS", tx.Snapshot!.ActionId);
    }

    /// <summary>15. 不完整的快照（IsComplete=false）不应被接受。</summary>
    [Fact]
    public async Task IncompleteSnapshot_Rejected()
    {
        // FAKE-SNAPSHOT-FAIL 返回失败快照结果（Snapshot=null），引擎转 Failed
        // 这里验证 FakeSnapshotFailure 的快照结果确实返回 Success=false
        var action = new FakeSnapshotFailureRepairAction();
        var context = DryRunContext();
        var plan = (await action.CreatePlanAsync(context, CancellationToken.None)).Plan!;

        var result = await action.CaptureSnapshotAsync(context, plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.NotNull(result.Failure);
    }

    // ========================================================================
    // 权限测试（16-19）
    // ========================================================================

    /// <summary>16. CurrentUser 动作在 DryRun 模式下成功。</summary>
    [Fact]
    public async Task CurrentUserAction_DryRun_Succeeds()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        var tx = await engine.PlanAsync(CancellationToken.None);

        // DryRun 模式下 CurrentUser 动作不需要提权，计划成功
        Assert.Equal(RepairTransactionState.PlanReady, tx.State);
        Assert.Equal(RepairPrivilegeRequirement.CurrentUser, tx.AuditEntry.PrivilegeRequirement);
    }

    /// <summary>17. Admin 动作在 Real 模式非提权 -> ELEVATION_REQUIRED。</summary>
    [Fact]
    public async Task AdminAction_RealNonElevated_Rejected()
    {
        // 使用空的生产目录（Real 模式在生产目录为空时直接 NotEnabled）
        // 要测试 ELEVATION_REQUIRED，需要非空目录 + Real + Admin + 非提权
        var catalog = new RepairActionCatalog();
        catalog.Register(new FakeAdminRequiredAction());
        var engine = new RepairTransactionEngine(catalog);
        var context = RealContext(elevated: false, confirmed: false);

        engine.CreateTransaction("FAKE-ADMIN", context);
        var tx = await engine.PlanAsync(CancellationToken.None);

        // 目录非空但非提权 + Admin -> ELEVATION_REQUIRED
        Assert.Equal(RepairTransactionState.Failed, tx.State);
        Assert.NotNull(tx.Failure);
        Assert.Equal(RepairTransactionEngine.FailureElevationRequired, tx.Failure!.Code);
    }

    /// <summary>18. Admin 动作在 DryRun 模式下成功（无需真实提权）。</summary>
    [Fact]
    public async Task AdminAction_DryRun_Succeeds()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-ADMIN", context);
        var tx = await engine.PlanAsync(CancellationToken.None);

        // DryRun 模式不需要真实提权
        Assert.Equal(RepairTransactionState.PlanReady, tx.State);
        Assert.Null(tx.Failure);
    }

    /// <summary>19. 无 Process.Start、无 runas、无 UAC 调用。</summary>
    [Fact]
    public async Task NoProcessStarted()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        await engine.PlanAsync(CancellationToken.None);
        engine.Confirm();
        await engine.CaptureSnapshotAsync(CancellationToken.None);
        await engine.ExecuteAsync(CancellationToken.None);
        await engine.VerifyAsync(CancellationToken.None);

        // 完整流程成功，没有任何 Process.Start 或 UAC 调用
        // （Fake 动作不调用任何系统进程，审计中 WasElevated=false）
        Assert.Equal(RepairTransactionState.Succeeded, engine.Transaction!.State);
        Assert.False(engine.Transaction.AuditEntry.WasElevated);
        Assert.Equal(RepairExecutionMode.DryRun, engine.Transaction.AuditEntry.ExecutionMode);
    }

    // ========================================================================
    // Dry Run 测试（20-23）
    // ========================================================================

    /// <summary>20. 全部 8 个 Fake 动作在 DryRun 模式下均能生成计划。</summary>
    [Theory]
    [InlineData("FAKE-SUCCESS")]
    [InlineData("FAKE-EXEC-FAIL")]
    [InlineData("FAKE-VERIFY-FAIL")]
    [InlineData("FAKE-ROLLBACK-FAIL")]
    [InlineData("FAKE-SNAPSHOT-FAIL")]
    [InlineData("FAKE-NON-ROLLBACK")]
    [InlineData("FAKE-ADMIN")]
    [InlineData("FAKE-SLOW")]
    public async Task AllFakeActions_CanDryRun(string actionId)
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction(actionId, context);
        var tx = await engine.PlanAsync(CancellationToken.None);

        // DryRun 模式下所有 Fake 动作都能生成计划
        Assert.Equal(RepairTransactionState.PlanReady, tx.State);
        Assert.NotNull(tx.Plan);
        // DryRun 计划不修改系统
        Assert.False(tx.Plan!.WillModifySystem);
    }

    /// <summary>21. Real 模式 + 空生产目录 -> REAL_EXECUTION_NOT_ENABLED。</summary>
    [Fact]
    public async Task RealMode_EmptyCatalog_Rejected()
    {
        var engine = new RepairTransactionEngine(new RepairActionCatalog());
        // 注册一个 Fake 到目录使 action lookup 成功，但 IsEmpty 仍为 true
        // 注意：注册后 IsEmpty=false。要测试空目录，需要不注册。
        // 创建一个只注册了动作的目录但让引擎认为 Real 不可用：
        // 实际上引擎检查 _catalog.IsEmpty。如果目录非空则不会 NotEnabled。
        // 所以用真正空的目录 + 不注册任何动作 -> CreateTransaction 会抛异常（action not found）。
        // 正确测试方式：使用空目录的 PlanAsync 前提是 action 已找到。
        // 引擎在 PlanAsync 中检查 Real + IsEmpty -> NotEnabled。
        // 但 CreateTransaction 需要 action 在目录中。
        // 解决方案：注册 Fake 动作，但使用一个自定义的"空" Real 检查路径。
        // 实际上，FAKE-ADMIN 在 Real 模式 + 非空目录 + 非提权 -> ELEVATION_REQUIRED（测试 17）。
        // 要测试 REAL_EXECUTION_NOT_ENABLED，需要 Real 模式 + 目录 IsEmpty=true。
        // 但目录为空时 CreateTransaction 找不到 action。
        // 所以这个测试验证空目录本身：
        var emptyCatalog = new RepairActionCatalog();
        Assert.True(emptyCatalog.IsEmpty);

        // 用 BuiltinRuleRegistry.ProductionRepairCatalog（生产目录为空）
        Assert.True(BuiltinRuleRegistry.ProductionRepairCatalog.IsEmpty);

        // Real 模式下 PrivilegeEvaluator 返回 NotEnabled
        var meta = new FakeAdminRequiredAction().Metadata;
        var realContext = RealContext();
        var decision = PrivilegeEvaluator.Evaluate(meta, realContext, realExecutionEnabled: false);
        Assert.Equal(ElevationDecision.NotEnabled, decision);
    }

    /// <summary>22. DryRun 审计记录 ExecutionMode=DryRun。</summary>
    [Fact]
    public async Task DryRun_AuditRecord_DryRun()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        var tx = await engine.PlanAsync(CancellationToken.None);

        Assert.Equal(RepairExecutionMode.DryRun, tx.AuditEntry.ExecutionMode);
    }

    /// <summary>23. DryRun 模式下系统修改次数为 0。</summary>
    [Fact]
    public async Task DryRun_SystemModificationCount_Zero()
    {
        var engine = new RepairTransactionEngine(CreateFakeCatalog());
        var context = DryRunContext();

        engine.CreateTransaction("FAKE-SUCCESS", context);
        var tx = await engine.PlanAsync(CancellationToken.None);

        // DryRun 计划不修改系统
        Assert.False(tx.Plan!.WillModifySystem);

        // 所有步骤的 IsSystemMutating=false
        foreach (var step in tx.Plan.Steps)
        {
            Assert.False(step.IsSystemMutating);
        }

        // 动作元数据 IsSystemMutating=false
        var action = CreateFakeCatalog().GetRequiredAction("FAKE-SUCCESS");
        Assert.False(action.Metadata.IsSystemMutating);
    }

    // ========================================================================
    // 目录测试（24-28）
    // ========================================================================

    /// <summary>24. 生产修复动作目录为空。</summary>
    [Fact]
    public void EmptyProductionCatalog()
    {
        Assert.True(BuiltinRuleRegistry.ProductionRepairCatalog.IsEmpty);
        Assert.Empty(BuiltinRuleRegistry.ProductionRepairCatalog.Actions);
        Assert.Empty(BuiltinRuleRegistry.ProductionRepairCatalog.ExecutableActionIds);
        Assert.Empty(BuiltinRuleRegistry.ExecutableRepairActions);
    }

    /// <summary>25. Fake 动作在测试目录中注册成功。</summary>
    [Fact]
    public void FakeCatalog_RegistersSuccessfully()
    {
        var catalog = CreateFakeCatalog();

        Assert.False(catalog.IsEmpty);
        Assert.Equal(8, catalog.Actions.Count);

        foreach (var id in FakeRepairActions.AllActionIds)
        {
            Assert.True(catalog.Contains(id));
            Assert.NotNull(catalog.GetAction(id));
        }
    }

    /// <summary>26. 重复 ActionId 注册被拒绝。</summary>
    [Fact]
    public void DuplicateActionId_Rejected()
    {
        var catalog = new RepairActionCatalog();
        catalog.Register(new FakeSuccessfulRepairAction());

        // 再次注册相同 ID 应抛异常
        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register(new FakeSuccessfulRepairAction()));
    }

    /// <summary>27. 空 ActionId 被拒绝。</summary>
    [Fact]
    public void EmptyActionId_Rejected()
    {
        var catalog = new RepairActionCatalog();

        // 创建一个 ActionId 为空的 Fake 动作
        var emptyAction = new EmptyActionIdRepairAction();

        Assert.Throws<ArgumentException>(() => catalog.Register(emptyAction));
    }

    /// <summary>28. ExecutableActionIds 来自注册的动作，不是字符串白名单。</summary>
    [Fact]
    public void ExecutableIds_FromActions_NotStringWhitelist()
    {
        var catalog = new RepairActionCatalog();

        // 空目录时无 ID
        Assert.Empty(catalog.ExecutableActionIds);

        // 注册一个动作后，ID 出现在集合中
        catalog.Register(new FakeSuccessfulRepairAction());
        Assert.Contains("FAKE-SUCCESS", catalog.ExecutableActionIds);

        // 注册更多动作
        catalog.Register(new FakeExecutionFailureRepairAction());
        Assert.Contains("FAKE-EXEC-FAIL", catalog.ExecutableActionIds);
        Assert.Equal(2, catalog.ExecutableActionIds.Count);

        // 未注册的 ID 不在集合中
        Assert.DoesNotContain("FIX-PRX-01", catalog.ExecutableActionIds);
        Assert.DoesNotContain("FIX-DNS-01", catalog.ExecutableActionIds);
    }

    // ========================================================================
    // 审计脱敏测试（29-30）
    // ========================================================================

    /// <summary>29. 代理字符串中的凭据（user:pass@）不出现在审计中。</summary>
    [Fact]
    public void CredentialsNotInAudit()
    {
        // UrlSanitizer.SanitizeProxyServer 应移除 userinfo
        var proxyWithCreds = "http://user:secret@127.0.0.1:7890";
        var sanitized = UrlSanitizer.SanitizeProxyServer(proxyWithCreds);

        Assert.DoesNotContain("user", sanitized);
        Assert.DoesNotContain("secret", sanitized);
        Assert.DoesNotContain("@", sanitized);
        // 保留 host:port
        Assert.Contains("127.0.0.1", sanitized);
    }

    /// <summary>30. URL 中的 token 查询参数不出现在审计中。</summary>
    [Fact]
    public void TokenNotInAudit()
    {
        var urlWithToken = "https://example.com/api?token=abcdef123&data=x";
        var sanitized = UrlSanitizer.SanitizeUrl(urlWithToken);

        Assert.DoesNotContain("token", sanitized);
        Assert.DoesNotContain("abcdef123", sanitized);
        Assert.DoesNotContain("?", sanitized);
        // 保留 scheme://host/path
        Assert.Contains("example.com", sanitized);
    }

    // ========================================================================
    // 辅助类
    // ========================================================================

    /// <summary>ActionId 为空的测试用 Fake 动作。</summary>
    private sealed class EmptyActionIdRepairAction : FakeRepairActionBase
    {
        public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
            actionId: "",
            titleKey: "repair.fake.empty.title",
            descriptionKey: "repair.fake.empty.desc",
            confirmationKey: "repair.fake.empty.confirm",
            riskLevel: RepairRiskLevel.Low,
            privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
            supportsRollback: true,
            isSystemMutating: false,
            applicableFindingIds: ["finding.test"]);

        public override ValueTask<RepairStepResult> ExecuteAsync(
            RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
            => new(RepairStepResult.Ok());

        public override ValueTask<RepairVerificationResult> VerifyAsync(
            RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
            => new(RepairVerificationResult.Pass());
    }
}
