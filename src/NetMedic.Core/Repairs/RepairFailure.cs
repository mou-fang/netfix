namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复失败信息。区分失败代码（用于程序判断）和消息 key（用于本地化显示）。
/// </summary>
public sealed record RepairFailure(
    string Code,
    string MessageKey,
    string? Detail = null)
{
    /// <summary>创建一个标准失败。</summary>
    public static RepairFailure Create(string code, string messageKey, string? detail = null)
        => new(code, messageKey, detail);
}
