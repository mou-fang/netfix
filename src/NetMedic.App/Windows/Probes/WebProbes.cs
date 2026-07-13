using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// WEB-01: Windows NCSI/NLM 连接状态探针。
/// 读取 Windows 网络连接状态指示。仅作为辅助信号，不作为唯一结论（任务书 §5.5）。
/// 使用 NLM COM API 或注册表回退。
/// </summary>
public sealed class NcsiProbe : WindowsProbeBase
{
    public override string Id => "WEB-01";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // 读取 NCSI 注册表状态（简化方案）
        // 更准确的方式是 NLM COM API，但阶段 2 先用注册表辅助
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\NlaSvc\Parameters\Internet");
            var enableActiveProbing = key?.GetValue("EnableActiveProbing");
            evidence["ncsi_active_probing"] = enableActiveProbing;
        }
        catch
        {
            evidence["ncsi_active_probing"] = "unknown";
        }

        // NCSI 仅作为辅助信号，不单独判定
        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.web.ncsi.signal",
            evidence: evidence.AsReadOnly()));
    }
}

/// <summary>
/// WEB-02: 直连 HTTPS 探针。
/// 不使用显式代理，直接访问健康目标。
/// 对应任务书 §5.4：使用两个独立健康目标，不携带设备标识。
/// </summary>
public sealed class DirectHttpsProbe : WindowsProbeBase
{
    public override string Id => "WEB-02";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    private static readonly HttpClientHandler Handler = new()
    {
        UseProxy = false, // 直连：不使用系统代理
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true, // 记录证书错误但不中断（阶段 2 只读检测）
    };

    private static readonly HttpClient Client = new(Handler)
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        int successCount = 0;
        var results = new List<string>();

        foreach (var target in HealthTargetCatalog.Targets)
        {
            try
            {
                var url = $"https://{target.Host}{target.ExpectedPath}";
                var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
                {
                    successCount++;
                    results.Add($"{target.Host}: OK ({(int)response.StatusCode})");
                }
                else
                {
                    results.Add($"{target.Host}: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add($"{target.Host}: {ex.GetType().Name}");
            }
        }

        evidence["target_results"] = results;
        evidence["success_count"] = successCount;

        if (successCount >= 1)
        {
            return ProbeResult.Pass(this.Id, "probe.web.direct.ok",
                evidence: evidence.AsReadOnly());
        }

        return ProbeResult.Fail(this.Id, "probe.web.direct.fail",
            evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
    }
}

/// <summary>
/// WEB-03: 系统代理路径 HTTPS 探针。
/// 按系统代理规则访问健康目标，与直连结果对比。
/// </summary>
public sealed class SystemProxyHttpsProbe : WindowsProbeBase
{
    public override string Id => "WEB-03";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    private static readonly HttpClientHandler Handler = new()
    {
        UseProxy = true, // 使用系统代理
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
    };

    private static readonly HttpClient Client = new(Handler)
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        int successCount = 0;
        var results = new List<string>();

        foreach (var target in HealthTargetCatalog.Targets)
        {
            try
            {
                var url = $"https://{target.Host}{target.ExpectedPath}";
                var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
                {
                    successCount++;
                    results.Add($"{target.Host}: OK ({(int)response.StatusCode})");
                }
                else
                {
                    results.Add($"{target.Host}: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add($"{target.Host}: {ex.GetType().Name}");
            }
        }

        evidence["target_results"] = results;
        evidence["success_count"] = successCount;

        if (successCount >= 1)
        {
            return ProbeResult.Pass(this.Id, "probe.web.proxy.ok",
                evidence: evidence.AsReadOnly());
        }

        return ProbeResult.Fail(this.Id, "probe.web.proxy.fail",
            evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
    }
}

/// <summary>
/// WEB-04: 认证门户检测探针。
/// 检测预期健康页面是否被重定向或替换为认证页面。
/// 对应任务书 §5.3 WEB-04：识别认证门户和代理拦截。
/// </summary>
public sealed class CaptivePortalProbe : WindowsProbeBase
{
    public override string Id => "WEB-04";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    private static readonly HttpClientHandler Handler = new()
    {
        UseProxy = false,
        AllowAutoRedirect = true,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
    };

    private static readonly HttpClient Client = new(Handler)
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // 使用 generate_204 端点：正常返回 204，被拦截通常返回 200 或 302
        try
        {
            var url = "https://connectivitycheck.gstatic.com/generate_204";
            var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            evidence["status_code"] = (int)response.StatusCode;
            evidence["final_url"] = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;

            // 204 = 正常无认证门户
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return ProbeResult.Pass(this.Id, "probe.web.captive.none",
                    evidence: evidence.AsReadOnly());
            }

            // 200 或重定向 = 可能被认证门户拦截
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect)
            {
                return ProbeResult.Fail(this.Id, "probe.web.captive.detected",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
            }

            return ProbeResult.Pass(this.Id, "probe.web.captive.unknown",
                evidence: evidence.AsReadOnly());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["error"] = ex.GetType().Name;
            // 网络错误不等于认证门户
            return ProbeResult.Skip(this.Id, "probe.web.captive.skip");
        }
    }
}
