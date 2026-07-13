namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 目标 URI 构建器。纯函数，TargetProbe 和单元测试共同调用。
/// 确保使用 NormalizedTarget 的 PathAndQuery 构建真实请求地址。
/// </summary>
public static class TargetUriBuilder
{
    /// <summary>
    /// 根据 NormalizedTarget 构建请求 URL。
    /// 使用 scheme://host:port/pathAndQuery 格式。
    /// </summary>
    public static string BuildRequestUrl(NormalizedTarget target)
    {
        return $"{target.Scheme}://{target.Host}:{target.Port}{target.PathAndQuery}";
    }
}
