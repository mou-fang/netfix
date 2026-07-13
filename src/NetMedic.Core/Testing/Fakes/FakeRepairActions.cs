using System.Collections.Immutable;
using NetMedic.Core.Repairs;

namespace NetMedic.Core.Testing.Fakes;

/// <summary>
/// 阶段 4.0 Fake 修复动作集合。
/// 所有 Fake 动作均为 IsSystemMutating=false、WillModifySystem=false，
/// 用于测试事务引擎的状态机转换、回滚、验证和取消逻辑。
/// 无任何真实系统修改。
/// </summary>
public static class FakeRepairActions
{
    /// <summary>注册全部 8 个 Fake 修复动作到指定目录。</summary>
    public static void RegisterAll(RepairActionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.Register(new FakeSuccessfulRepairAction());
        catalog.Register(new FakeExecutionFailureRepairAction());
        catalog.Register(new FakeVerificationFailureRepairAction());
        catalog.Register(new FakeRollbackFailureRepairAction());
        catalog.Register(new FakeSnapshotFailureRepairAction());
        catalog.Register(new FakeNonRollbackableAction());
        catalog.Register(new FakeAdminRequiredAction());
        catalog.Register(new FakeSlowAction());
    }

    /// <summary>所有 Fake 动作 ID。</summary>
    public static readonly IReadOnlySet<string> AllActionIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "FAKE-SUCCESS",
        "FAKE-EXEC-FAIL",
        "FAKE-VERIFY-FAIL",
        "FAKE-ROLLBACK-FAIL",
        "FAKE-SNAPSHOT-FAIL",
        "FAKE-NON-ROLLBACK",
        "FAKE-ADMIN",
        "FAKE-SLOW",
    };
}

// === 共用辅助 ===

/// <summary>所有 Fake 动作共用的资源 key 前缀构建器。</summary>
internal static class FakeRepairKeys
{
    public static string Title(string id) => $"repair.fake.{id.ToLowerInvariant()}.title";
    public static string Desc(string id) => $"repair.fake.{id.ToLowerInvariant()}.desc";
    public static string Confirm(string id) => $"repair.fake.{id.ToLowerInvariant()}.confirm";
}

/// <summary>Fake 动作基类，提供共用计划与快照逻辑。</summary>
public abstract class FakeRepairActionBase : IRepairAction
{
    protected const string SimulationNoteDryRun = "DryRun: no system modification.";

    public abstract RepairActionMetadata Metadata { get; }

    public virtual ValueTask<RepairPlanResult> CreatePlanAsync(
        RepairContext context, CancellationToken cancellationToken)
    {
        var plan = RepairPlan.Create(
            planId: $"plan-{Metadata.ActionId}-{Guid.NewGuid():N}",
            actionId: Metadata.ActionId,
            steps: BuildSteps(),
            riskLevel: Metadata.RiskLevel,
            privilegeRequirement: Metadata.PrivilegeRequirement,
            willModifySystem: false,
            requiresRestart: false,
            requiresNetworkReconnect: false,
            verificationProbeIds: Metadata.VerificationProbeIds,
            userVisibleWarnings: []);
        return new(RepairPlanResult.Ok(plan));
    }

    public virtual ValueTask<RepairSnapshotResult> CaptureSnapshotAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
    {
        var snapshot = RepairSnapshot.DryRunEmpty(
            snapshotId: $"snap-{Metadata.ActionId}-{Guid.NewGuid():N}",
            actionId: Metadata.ActionId,
            capturedAt: DateTimeOffset.UtcNow);
        return new(RepairSnapshotResult.Ok(snapshot));
    }

    public abstract ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken);

    public abstract ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken);

    public virtual ValueTask<RepairStepResult> RollbackAsync(
        RepairContext context, RepairSnapshot snapshot, CancellationToken cancellationToken)
    {
        return new(RepairStepResult.Ok());
    }

    protected IReadOnlyList<RepairStep> BuildSteps()
    {
        return new[]
        {
            new RepairStep(
                Order: 1,
                TitleKey: FakeRepairKeys.Title(Metadata.ActionId),
                IsSystemMutating: false,
                IsRollbackable: Metadata.SupportsRollback,
                PrivilegeRequirement: Metadata.PrivilegeRequirement,
                SimulationNote: SimulationNoteDryRun),
        }.ToImmutableList();
    }

    protected RepairActionMetadata BuildMetadata(
        string actionId,
        RepairRiskLevel risk,
        RepairPrivilegeRequirement priv,
        bool supportsRollback,
        IEnumerable<string> findingIds)
    {
        return RepairActionMetadata.Create(
            actionId: actionId,
            titleKey: FakeRepairKeys.Title(actionId),
            descriptionKey: FakeRepairKeys.Desc(actionId),
            confirmationKey: FakeRepairKeys.Confirm(actionId),
            riskLevel: risk,
            privilegeRequirement: priv,
            supportsRollback: supportsRollback,
            isSystemMutating: false,
            applicableFindingIds: findingIds,
            verificationProbeIds: [],
            estimatedDuration: TimeSpan.FromMilliseconds(100));
    }
}

// === 8 个 Fake 动作 ===

/// <summary>1. 全部步骤成功。execute + verify + rollback 均成功。</summary>
public sealed class FakeSuccessfulRepairAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-SUCCESS",
        titleKey: FakeRepairKeys.Title("FAKE-SUCCESS"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-SUCCESS"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-SUCCESS"),
        riskLevel: RepairRiskLevel.Low,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.dead_local_proxy"],
        verificationProbeIds: ["PRX-04"],
        estimatedDuration: TimeSpan.FromMilliseconds(100));

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Ok());

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());
}

/// <summary>2. 执行失败，回滚成功。execute 返回失败，rollback 成功。</summary>
public sealed class FakeExecutionFailureRepairAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-EXEC-FAIL",
        titleKey: FakeRepairKeys.Title("FAKE-EXEC-FAIL"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-EXEC-FAIL"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-EXEC-FAIL"),
        riskLevel: RepairRiskLevel.Low,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.dead_local_proxy"],
        verificationProbeIds: ["PRX-04"]);

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Err(RepairFailure.Create("EXEC_FAILED", "repair.fake.exec_fail.error")));

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());
}

/// <summary>3. 执行成功，验证失败，回滚成功。</summary>
public sealed class FakeVerificationFailureRepairAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-VERIFY-FAIL",
        titleKey: FakeRepairKeys.Title("FAKE-VERIFY-FAIL"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-VERIFY-FAIL"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-VERIFY-FAIL"),
        riskLevel: RepairRiskLevel.Low,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.dns_failure"],
        verificationProbeIds: ["DNS-02"]);

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Ok());

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Fail(
            RepairFailure.Create("VERIFY_FAILED", "repair.fake.verify_fail.error")));
}

/// <summary>4. 执行失败，回滚也失败。execute 失败，rollback 失败。</summary>
public sealed class FakeRollbackFailureRepairAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-ROLLBACK-FAIL",
        titleKey: FakeRepairKeys.Title("FAKE-ROLLBACK-FAIL"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-ROLLBACK-FAIL"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-ROLLBACK-FAIL"),
        riskLevel: RepairRiskLevel.Medium,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.pac_unreachable"],
        verificationProbeIds: ["PRX-03"]);

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Err(RepairFailure.Create("EXEC_FAILED", "repair.fake.rollback_fail.exec_error")));

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());

    public override ValueTask<RepairStepResult> RollbackAsync(
        RepairContext context, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Err(RepairFailure.Create("ROLLBACK_FAILED", "repair.fake.rollback_fail.rollback_error")));
}

/// <summary>5. 快照捕获失败，不执行。</summary>
public sealed class FakeSnapshotFailureRepairAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-SNAPSHOT-FAIL",
        titleKey: FakeRepairKeys.Title("FAKE-SNAPSHOT-FAIL"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-SNAPSHOT-FAIL"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-SNAPSHOT-FAIL"),
        riskLevel: RepairRiskLevel.Low,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.apipa_dhcp"],
        verificationProbeIds: ["NET-02"]);

    public override ValueTask<RepairSnapshotResult> CaptureSnapshotAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairSnapshotResult.Err(
            RepairFailure.Create("SNAPSHOT_FAILED", "repair.fake.snapshot_fail.error")));

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Ok());

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());
}

/// <summary>6. 不支持回滚。执行失败时直接进入 Failed 状态。</summary>
public sealed class FakeNonRollbackableAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-NON-ROLLBACK",
        titleKey: FakeRepairKeys.Title("FAKE-NON-ROLLBACK"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-NON-ROLLBACK"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-NON-ROLLBACK"),
        riskLevel: RepairRiskLevel.Low,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: false,
        isSystemMutating: false,
        applicableFindingIds: ["finding.ncsi_mismatch"],
        verificationProbeIds: ["WEB-01"]);

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
        => new(RepairStepResult.Err(RepairFailure.Create("EXEC_FAILED", "repair.fake.non_rollback.error")));

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());
}

/// <summary>7. 需要管理员权限。在非提权 Real 模式下失败。</summary>
public sealed class FakeAdminRequiredAction : FakeRepairActionBase
{
    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-ADMIN",
        titleKey: FakeRepairKeys.Title("FAKE-ADMIN"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-ADMIN"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-ADMIN"),
        riskLevel: RepairRiskLevel.Medium,
        privilegeRequirement: RepairPrivilegeRequirement.Administrator,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.winhttp_proxy_config"],
        verificationProbeIds: ["PRX-02"]);

    public override ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
    {
        // DryRun 模式总是成功；Real 模式需提权（引擎在 PlanAsync 阶段已检查）
        if (context.ExecutionMode == RepairExecutionMode.DryRun)
        {
            return new(RepairStepResult.Ok());
        }

        // Real 模式：未提权则失败
        if (!context.IsElevated)
        {
            return new(RepairStepResult.Err(
                RepairFailure.Create("ELEVATION_REQUIRED", "repair.fake.admin.not_elevated")));
        }

        return new(RepairStepResult.Ok());
    }

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());
}

/// <summary>8. 慢动作。支持取消，execute 有延迟。</summary>
public sealed class FakeSlowAction : FakeRepairActionBase
{
    private readonly TimeSpan _executeDelay;

    public FakeSlowAction(TimeSpan? executeDelay = null)
    {
        _executeDelay = executeDelay ?? TimeSpan.FromSeconds(2);
    }

    public override RepairActionMetadata Metadata { get; } = RepairActionMetadata.Create(
        actionId: "FAKE-SLOW",
        titleKey: FakeRepairKeys.Title("FAKE-SLOW"),
        descriptionKey: FakeRepairKeys.Desc("FAKE-SLOW"),
        confirmationKey: FakeRepairKeys.Confirm("FAKE-SLOW"),
        riskLevel: RepairRiskLevel.Low,
        privilegeRequirement: RepairPrivilegeRequirement.CurrentUser,
        supportsRollback: true,
        isSystemMutating: false,
        applicableFindingIds: ["finding.target_unreachable"],
        verificationProbeIds: ["TARGET-01"],
        estimatedDuration: TimeSpan.FromSeconds(2));

    public override async ValueTask<RepairStepResult> ExecuteAsync(
        RepairContext context, RepairPlan plan, RepairSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_executeDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        return RepairStepResult.Ok();
    }

    public override ValueTask<RepairVerificationResult> VerifyAsync(
        RepairContext context, RepairPlan plan, CancellationToken cancellationToken)
        => new(RepairVerificationResult.Pass());
}
