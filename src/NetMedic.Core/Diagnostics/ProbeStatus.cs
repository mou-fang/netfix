namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 探针状态。与任务书 §5.2 保持一致。
/// </summary>
public enum ProbeStatus
{
    /// <summary>检查对象状态正常。</summary>
    Passed,

    /// <summary>检查对象存在可疑情况，但不一定阻断网络。</summary>
    Warning,

    /// <summary>检查对象确实不正常。</summary>
    Failed,

    /// <summary>探针被主动跳过（例如因症状不匹配或保护策略）。</summary>
    Skipped,

    /// <summary>探针自身未能完成，不代表检查对象异常。</summary>
    Error,
}
