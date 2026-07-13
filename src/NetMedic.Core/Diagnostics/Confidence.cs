namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 诊断置信度。对用户只显示这三个等级，不显示虚假百分比。
/// 对应任务书 §6.2。
/// </summary>
public enum Confidence
{
    /// <summary>高可信：有直接证据且已排除主要反证。</summary>
    High,

    /// <summary>可能：有证据但反证不足或证据间接。</summary>
    Medium,

    /// <summary>证据不足：无法形成可靠结论，需要深度体检。</summary>
    Insufficient,
}
