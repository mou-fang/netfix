namespace NetMedic.Core.Repairs;

/// <summary>
/// 快照捕获结果。
/// 成功时携带 <see cref="Snapshot"/>；失败时携带 <see cref="Failure"/>。
/// </summary>
public sealed record RepairSnapshotResult(
    bool Success,
    RepairSnapshot? Snapshot,
    RepairFailure? Failure)
{
    /// <summary>成功结果。</summary>
    public static RepairSnapshotResult Ok(RepairSnapshot snapshot) => new(true, snapshot, null);

    /// <summary>失败结果。</summary>
    public static RepairSnapshotResult Err(RepairFailure failure) => new(false, null, failure);
}
