using NetMedic.App.Windows;
using NetMedic.App.Windows.Probes;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows;

/// <summary>
/// 阶段 2 Windows 探针验证工具。
/// 在正常网络状态下运行所有真实探针，输出结果供 QA 记录。
/// 仅用于开发验证，不包含在正式发布中。
/// </summary>
public static class ProbeVerificationRunner
{
    public static async Task RunAsync(string? targetHost = null)
    {
        var env = new WindowsNetworkEnvironment();
        var probes = WindowsProbeSet.BuildQuick(targetHost);
        var orchestrator = new ProbeOrchestrator(probes, maxConcurrency: 2);

        Console.WriteLine($"=== NetMedic Windows 探针验证 ===");
        Console.WriteLine($"时间: {DateTimeOffset.Now:O}");
        Console.WriteLine($"探针数: {probes.Count}");
        Console.WriteLine();

        var progress = new Progress<ProbeProgressEvent>(e =>
        {
            if (e.Stage == ProbeStage.Finished || e.Stage == ProbeStage.TimedOut || e.Stage == ProbeStage.Skipped)
            {
                Console.WriteLine($"  [{e.Stage}] {e.ProbeId}");
            }
        });

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(60),
            externalCancellationToken: CancellationToken.None,
            progress: progress);

        Console.WriteLine();
        Console.WriteLine($"=== 结果汇总 ===");
        Console.WriteLine($"总耗时: {result.TotalDuration.TotalSeconds:F1}s");
        Console.WriteLine($"已取消: {result.WasCancelled}");
        Console.WriteLine();

        foreach (var r in result.Results)
        {
            Console.WriteLine($"--- {r.Id} ---");
            Console.WriteLine($"  状态: {r.Status}");
            Console.WriteLine($"  严重: {r.Severity}");
            Console.WriteLine($"  耗时: {r.Duration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"  需管理员: {r.RequiresAdmin}");
            if (r.Error is not null)
            {
                Console.WriteLine($"  错误: {r.Error.Code} - {r.Error.MessageKey}");
            }

            if (r.Evidence.Count > 0)
            {
                Console.WriteLine($"  证据:");
                foreach (var kv in r.Evidence)
                {
                    Console.WriteLine($"    {kv.Key} = {FormatValue(kv.Value)}");
                }
            }

            Console.WriteLine();
        }
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
            {
                items.Add(item?.ToString() ?? "null");
            }

            return $"[{string.Join(", ", items)}]";
        }

        return value.ToString() ?? "null";
    }
}
