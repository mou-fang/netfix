namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复验证结果。验证探针是否确认修复生效。
/// </summary>
public sealed record RepairVerificationResult(
    bool Verified,
    RepairFailure? Failure)
{
    /// <summary>验证通过。</summary>
    public static RepairVerificationResult Pass() => new(true, null);

    /// <summary>验证失败。</summary>
    public static RepairVerificationResult Fail(RepairFailure failure) => new(false, failure);
}
