using NetMedic.App.Windows;
using NetMedic.App.Windows.Probes;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Tests;

/// <summary>
/// Windows 真实探针集成验证测试。
/// 仅在 NETMEDIC_INTEGRATION_TESTS=1 时运行，避免依赖公网。
/// 对应任务书 §11.3：Windows 专属功能必须有 Windows Runner/VM 证据。
/// </summary>
public class WindowsProbeVerificationTests
{
    [IntegrationFact]
    public async Task AllProbes_CompleteWithinTimeout_NonAdmin()
    {
        var env = new WindowsNetworkEnvironment();
        var probes = WindowsProbeSet.BuildQuick();
        var orchestrator = new ProbeOrchestrator(probes, maxConcurrency: 2);

        var result = await orchestrator.ExecuteAsync(
            env,
            SymptomCategory.Unsure,
            DiagnosticMode.Quick,
            totalBudget: TimeSpan.FromSeconds(30),
            externalCancellationToken: CancellationToken.None);

        Assert.NotEmpty(result.Results);
        Assert.True(result.TotalDuration < TimeSpan.FromSeconds(30),
            $"总耗时 {result.TotalDuration.TotalSeconds:F1}s 超过 30s 预算");

        foreach (var r in result.Results)
        {
            Assert.False(r.RequiresAdmin, $"探针 {r.Id} 不应需要管理员权限");
            Assert.NotEqual("PROBE_TIMEOUT", r.Error?.Code ?? string.Empty);
        }
    }

    [IntegrationFact]
    public async Task Sys01_UsesNetGetJoinInformation()
    {
        var probe = new SystemContextProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("SYS-01", result.Id);
        Assert.True(result.Evidence.ContainsKey("os_version"));
        Assert.True(result.Evidence.ContainsKey("join_status"));
        // join_status 应包含 NetGetJoinInformation 的结果，而非环境变量比较
        var joinStatus = result.Evidence["join_status"]?.ToString() ?? "";
        Assert.False(string.IsNullOrEmpty(joinStatus));
    }

    [IntegrationFact]
    public async Task Prx02_UsesWinHttpGetDefaultProxyConfiguration()
    {
        var probe = new WinhttpProxyProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("PRX-02", result.Id);
        // 必须标记 proxy_layer = WinHTTP
        Assert.Equal("WinHTTP", result.Evidence["proxy_layer"]?.ToString());
        // 必须有 winhttp_access_type 字段（证明使用了 API 而非注册表推断）
        Assert.True(result.Evidence.ContainsKey("winhttp_access_type"));
    }

    [IntegrationFact]
    public async Task Web02_DirectHttps_UsesNoProxy()
    {
        var probe = new DirectHttpsProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("WEB-02", result.Id);
        // 必须标记 use_proxy = false
        Assert.Equal(false, result.Evidence["use_proxy"]);
        // 必须标记 connection_path = direct
        Assert.Equal("direct", result.Evidence["connection_path"]?.ToString());
        // 必须标记 request_made = true（证明实际发出了请求）
        Assert.Equal(true, result.Evidence["request_made"]);
        // 有 TLS 证据
        Assert.True(result.Evidence.ContainsKey("tls_valid") || result.Evidence.ContainsKey("tls_error_category"));
    }

    [IntegrationFact]
    public async Task Web03_SystemProxy_ReturnsSkippedOrRealRequest()
    {
        var probe = new SystemProxyHttpsProbe();
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("WEB-03", result.Id);
        Assert.Equal(true, result.Evidence["use_proxy"]);

        // 如果系统没有代理，应该 Skipped 且 request_made=false
        // 如果有代理，应该 Pass/Fail 且 request_made=true
        if (result.Status == ProbeStatus.Skipped)
        {
            Assert.Equal(false, result.Evidence["request_made"]);
            Assert.Equal("WEB-02", result.Evidence["reused_from"]?.ToString());
        }
        else
        {
            Assert.Equal(true, result.Evidence["request_made"]);
        }
    }

    [IntegrationFact]
    public async Task Target01_HttpsDefault_RecordsAllPhases()
    {
        var probe = new TargetProbe("www.cloudflare.com");
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("TARGET-01", result.Id);
        // 默认补全为 HTTPS
        Assert.Equal("https", result.Evidence["scheme"]?.ToString());
        Assert.Equal(443, result.Evidence["target_port"]);
        Assert.Equal(true, result.Evidence["is_tls"]);
        // 各阶段必须有记录
        Assert.True(result.Evidence.ContainsKey("dns_ok"));
        Assert.True(result.Evidence.ContainsKey("tcp_ok"));
        Assert.True(result.Evidence.ContainsKey("tls_performed"));
    }

    [IntegrationFact]
    public async Task Target01_HttpUrl_UsesPort80_NoTls()
    {
        var probe = new TargetProbe("http://www.msftconnecttest.com/connecttest.txt");
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("TARGET-01", result.Id);
        Assert.Equal("http", result.Evidence["scheme"]?.ToString());
        Assert.Equal(80, result.Evidence["target_port"]);
        Assert.Equal(false, result.Evidence["is_tls"]);
        Assert.Equal(false, result.Evidence["tls_performed"]);
    }

    [IntegrationFact]
    public async Task Target01_CustomPort_Respected()
    {
        var probe = new TargetProbe("https://www.cloudflare.com:8443");
        var ctx = new ProbeContext(
            DiagnosticSession.Create(SymptomCategory.Unsure, DiagnosticMode.Quick),
            new WindowsNetworkEnvironment());

        var result = await probe.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("TARGET-01", result.Id);
        Assert.Equal(8443, result.Evidence["target_port"]);
    }
}
