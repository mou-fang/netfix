using System.Collections.Immutable;

namespace NetMedic.Core.Repairs;

/// <summary>
/// 执行前系统状态快照。对应任务书 §7.4 快照捕获。
/// 用于回滚时恢复系统状态。DryRun 模式下快照可为空字典（无真实状态可捕获）。
/// </summary>
public sealed record RepairSnapshot(
    string SnapshotId,
    string ActionId,
    DateTimeOffset CapturedAt,
    int SchemaVersion,
    IReadOnlyDictionary<string, string> Values,
    bool IsComplete,
    IReadOnlyList<string> ValidationErrors)
{
    /// <summary>创建一个完整快照。</summary>
    public static RepairSnapshot Complete(
        string snapshotId,
        string actionId,
        DateTimeOffset capturedAt,
        IReadOnlyDictionary<string, string>? values = null,
        int schemaVersion = 1)
        => new(
            SnapshotId: snapshotId,
            ActionId: actionId,
            CapturedAt: capturedAt,
            SchemaVersion: schemaVersion,
            Values: values ?? ImmutableDictionary<string, string>.Empty,
            IsComplete: true,
            ValidationErrors: []);

    /// <summary>创建一个 DryRun 空快照（无真实状态）。</summary>
    public static RepairSnapshot DryRunEmpty(
        string snapshotId,
        string actionId,
        DateTimeOffset capturedAt)
        => new(
            SnapshotId: snapshotId,
            ActionId: actionId,
            CapturedAt: capturedAt,
            SchemaVersion: 1,
            Values: ImmutableDictionary<string, string>.Empty,
            IsComplete: true,
            ValidationErrors: []);
}
