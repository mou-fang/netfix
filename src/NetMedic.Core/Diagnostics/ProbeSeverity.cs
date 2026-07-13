namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 探针严重程度。对应任务书 §5.2 的 severity 字段。
/// </summary>
public enum ProbeSeverity
{
    /// <summary>信息性，无需关注。</summary>
    Info,

    /// <summary>低影响，可能需要留意。</summary>
    Low,

    /// <summary>中等影响，可能影响部分网络功能。</summary>
    Medium,

    /// <summary>高影响，很可能导致网络故障。</summary>
    High,
}
