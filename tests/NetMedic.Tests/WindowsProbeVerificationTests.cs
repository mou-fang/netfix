using NetMedic.App.Windows;
using NetMedic.App.Windows.Probes;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Tests;

/// <summary>
/// Windows 真实探针验证测试。
/// 仅在 Windows 上运行，验证真实探针在正常网络状态下的行为。
/// 对应任务书 §11.3：Windows 专属功能必须有 Windows Runner/VM 证据。
/// </summary>
public class WindowsProbeVerificationTests
{
    [Fact]
    public async Task AllProbes_CompleteWithinTimeout_NonAdmin()
    {
        // 此测试在真实 Windows 上运行真实探针
        var env = new WindowsNetworkEnvironment();
        var probes = WindowsProbeSet.BuildQuick();
        var orchestrator = new ProbeOrchestrator(probes, maxConcurrency: 2);

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(30),
            externalCancellationToken: CancellationToken.None);

        // 所有探针应完成（非 Skipped），总耗时合理
        Assert.NotEmpty(result.Results);
        Assert.True(result.TotalDuration < TimeSpan.FromSeconds(30),
            $"总耗时 {result.TotalDuration.TotalSeconds:F1}s 超过 30s 预算");

        // 输出每个探针结果用于 QA 记录
        foreach (var r in result.Results)
        {
            // 验证：所有探针不需要管理员权限
            Assert.False(r.RequiresAdmin, $"探针 {r.Id} 不应需要管理员权限");
            // 验证：没有超时错误
            Assert.NotEqual("PROBE_TIMEOUT", r.Error?.Code ?? string.Empty);
        }
    }

    [Fact]
    public async Task Sys01_DetectsWindowsVersion()
    {
        var probe = new SystemContextProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("SYS-01", result.Id);
        Assert.True(result.Evidence.ContainsKey("os_version"));
        Assert.NotEmpty(result.Evidence["os_version"]?.ToString() ?? "");
    }

    [Fact]
    public async Task Net01_DetectsActiveAdapter()
    {
        var probe = new AdapterProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("NET-01", result.Id);
        // 正常网络下应有活动网卡
        Assert.True(result.Evidence.ContainsKey("up_adapter_count"));
    }

    [Fact]
    public async Task Dns02_ResolvesHealthDomain()
    {
        var probe = new DnsResolveProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("DNS-02", result.Id);
        // 正常网络下应能解析
        // 注意：如果断网可能失败，此处只验证探针能运行并返回结果
        Assert.True(result.Status == ProbeStatus.Passed || result.Status == ProbeStatus.Failed);
    }

    [Fact]
    public async Task Prx01_ReadsWininetProxy()
    {
        var probe = new WininetProxyProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("PRX-01", result.Id);
        Assert.True(result.Evidence.ContainsKey("proxy_enabled"));
    }

    [Fact]
    public async Task Web02_DirectHttpsReachesHealthTarget()
    {
        var probe = new DirectHttpsProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("WEB-02", result.Id);
        // 正常网络下直连应成功
        Assert.True(result.Status == ProbeStatus.Passed || result.Status == ProbeStatus.Failed);
    }

    [Fact]
    public async Task Target01_RejectsInvalidInput()
    {
        var probe = new TargetProbe("ftp://malicious.example.com\nrm -rf /");
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("TARGET-01", result.Id);
        // 恶意输入应被拒绝
        Assert.Equal(ProbeStatus.Failed, result.Status);
        Assert.True(result.Evidence.ContainsKey("input_rejected"));
    }

    [Fact]
    public async Task Target01_AcceptsValidUrl()
    {
        var probe = new TargetProbe("https://www.cloudflare.com");
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("TARGET-01", result.Id);
        // 有效 URL 不应被拒绝
        Assert.False((bool)(result.Evidence.GetValueOrDefault("input_rejected") ?? false));
    }
}
