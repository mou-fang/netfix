using System.Diagnostics;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// Windows 只读探针基类。提供计时和结果构建辅助。
/// 所有 Windows 探针必须只读、非管理员可运行。
/// </summary>
public abstract class WindowsProbeBase : IProbe
{
    public abstract string Id { get; }

    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(5);

    public virtual bool RequiresAdmin => false;

    public async Task<ProbeResult> ExecuteAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await ExecuteCoreAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // 让编排器处理超时/取消
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ProbeResult.Err(this.Id, "probe.error",
                new ProbeError("PROBE_EXCEPTION", ex.GetType().Name), sw.Elapsed);
        }
    }

    protected abstract Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken);
}
