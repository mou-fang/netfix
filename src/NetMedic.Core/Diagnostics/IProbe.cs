using NetMedic.Core.Abstractions;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 探针接口。每个探针只读检查一个方面，不修改系统。
/// 对应任务书 §5.2：探针不得直接修改系统。
/// </summary>
public interface IProbe
{
    /// <summary>探针唯一标识，如 "NET-01"。</summary>
    string Id { get; }

    /// <summary>该探针的独立超时。对应任务书 §5.2：每个探针有 2-8 秒独立超时。</summary>
    TimeSpan Timeout { get; }

    /// <summary>该探针是否需要管理员权限。</summary>
    bool RequiresAdmin { get; }

    /// <summary>执行检查。必须尊重传入的取消令牌。</summary>
    Task<ProbeResult> ExecuteAsync(ProbeContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 探针执行上下文。携带会话信息和模拟环境引用。
/// </summary>
public sealed record ProbeContext(
    DiagnosticSession Session,
    INetworkEnvironment Environment);
