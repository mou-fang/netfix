namespace NetMedic.Core.Repairs;

/// <summary>
/// 单个修复步骤执行结果。
/// </summary>
public sealed record RepairStepResult(
    bool Success,
    RepairFailure? Failure)
{
    /// <summary>成功结果。</summary>
    public static RepairStepResult Ok() => new(true, null);

    /// <summary>失败结果。</summary>
    public static RepairStepResult Err(RepairFailure failure) => new(false, failure);
}
