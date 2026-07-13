namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复执行上下文。携带会话、关联、执行模式与权限信息。
/// </summary>
public sealed record RepairContext(
    RepairExecutionMode ExecutionMode,
    bool IsElevated,
    string SessionId,
    string CorrelationId,
    bool UserConfirmed,
    DateTimeOffset StartedAt)
{
    /// <summary>创建一个 DryRun 上下文的便捷工厂。</summary>
    public static RepairContext ForDryRun(
        string sessionId,
        string correlationId,
        bool isElevated = false,
        bool userConfirmed = false)
        => new(
            ExecutionMode: RepairExecutionMode.DryRun,
            IsElevated: isElevated,
            SessionId: sessionId,
            CorrelationId: correlationId,
            UserConfirmed: userConfirmed,
            StartedAt: DateTimeOffset.UtcNow);
}
