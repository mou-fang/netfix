using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Core.Testing;

/// <summary>
/// 模拟探针。根据 FakeNetworkEnvironment 的状态返回预定义结果。
/// 对应任务书 §5.3 快速探针目录，但此处为阶段 1 的 Fake 实现。
/// 支持注入延迟，用于测试超时和取消行为。
/// </summary>
public sealed class FakeProbe : IProbe
{
    private readonly Func<FakeNetworkEnvironment, ProbeResult> _evaluator;
    private readonly TimeSpan? _injectedDelay;

    public FakeProbe(
        string id,
        Func<FakeNetworkEnvironment, ProbeResult> evaluator,
        TimeSpan? timeout = null,
        bool requiresAdmin = false,
        TimeSpan? injectedDelay = null)
    {
        this.Id = id;
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        this.Timeout = timeout ?? TimeSpan.FromSeconds(5);
        this.RequiresAdmin = requiresAdmin;
        _injectedDelay = injectedDelay;
    }

    public string Id { get; }

    public TimeSpan Timeout { get; }

    public bool RequiresAdmin { get; }

    public async Task<ProbeResult> ExecuteAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        if (context.Environment is not FakeNetworkEnvironment fake)
        {
            throw new InvalidOperationException(
                $"FakeProbe requires {nameof(FakeNetworkEnvironment)}, got {context.Environment.GetType().Name}.");
        }

        // 如果注入了延迟，等待（但尊重取消；测试用极短延迟）
        if (_injectedDelay is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return _evaluator(fake);
    }

    /// <summary>便捷工厂：创建一个总是返回指定状态的探针。</summary>
    public static FakeProbe Always(string id, ProbeResult result, TimeSpan? timeout = null)
        => new(id, _ => result, timeout);
}
