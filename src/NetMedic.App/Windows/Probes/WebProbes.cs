using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// WEB-01: Windows NCSI 正文验证探针。
/// 修正（阶段 2.1）：使用 Windows 官方 NCSI 目标 http://www.msftconnecttest.com/connecttest.txt，
/// 关闭自动重定向并验证预期正文 "Microsoft Connect Test"。
/// 主要用于认证门户检测。NCSI 仅作为辅助信号，不作为唯一结论。
/// </summary>
public sealed class NcsiProbe : WindowsProbeBase
{
    public override string Id => "WEB-01";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    private static readonly HttpClientHandler Handler = new()
    {
        UseProxy = false,
        AllowAutoRedirect = false, // 关闭重定向：认证门户会重定向
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
    };

    private static readonly HttpClient Client = new(Handler)
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        var target = HealthTargetCatalog.NcsiTarget;

        try
        {
            // NCSI 使用 HTTP（非 HTTPS），因为认证门户检测需要看原始响应
            var url = $"http://{target.Host}{target.ExpectedPath}";
            var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            evidence["target_host"] = target.Host;
            evidence["status_code"] = (int)response.StatusCode;
            evidence["redirected"] = (int)response.StatusCode is >= 300 and < 400;

            // 读取正文并验证预期内容
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            evidence["body_length"] = body.Length;
            bool contentMatches = body.Contains(target.ExpectedContentFragment, StringComparison.OrdinalIgnoreCase);
            evidence["content_matches"] = contentMatches;

            if (contentMatches)
            {
                return ProbeResult.Pass(this.Id, "probe.web.ncsi.ok",
                    evidence: evidence.AsReadOnly());
            }

            // 内容不匹配或被重定向 = 可能被认证门户拦截
            if ((int)response.StatusCode is >= 300 and < 400 || !contentMatches)
            {
                return ProbeResult.Fail(this.Id, "probe.web.ncsi.captive",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
            }

            return ProbeResult.Pass(this.Id, "probe.web.ncsi.signal",
                evidence: evidence.AsReadOnly());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["error"] = ex.GetType().Name;
            // 网络错误不等于断网，NCSI 仅作为辅助信号
            return ProbeResult.Skip(this.Id, "probe.web.ncsi.skip");
        }
    }
}

/// <summary>
/// WEB-02: 直连 HTTPS 探针。
/// 修正（阶段 2.1）：必须明确使用 UseProxy=false，真正进行直连请求。
/// 使用独立 HTTPS 目标（Cloudflare trace）。每个目标单独记录结果。
/// 任一目标失败不能直接判定断网。
/// </summary>
public sealed class DirectHttpsProbe : WindowsProbeBase
{
    public override string Id => "WEB-02";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    // 明确 UseProxy=false：不使用任何代理，真正的直连
    private static readonly HttpClientHandler Handler = new()
    {
        UseProxy = false,
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
    };

    private static readonly HttpClient Client = new(Handler)
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["use_proxy"] = false, // 明确标记：直连，不使用代理
            ["proxy_source"] = "none",
        };

        // 使用独立 HTTPS 目标
        var target = HealthTargetCatalog.IndependentTarget;
        var perTargetResults = new List<string>();
        int successCount = 0;

        // 对独立 HTTPS 目标执行请求
        try
        {
            var url = $"https://{target.Host}{target.ExpectedPath}";
            var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            string result;
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                bool contentOk = string.IsNullOrEmpty(target.ExpectedContentFragment) ||
                    body.Contains(target.ExpectedContentFragment, StringComparison.OrdinalIgnoreCase);
                if (contentOk)
                {
                    successCount++;
                    result = $"{target.Host}: OK ({(int)response.StatusCode})";
                }
                else
                {
                    result = $"{target.Host}: content mismatch ({(int)response.StatusCode})";
                }
            }
            else
            {
                result = $"{target.Host}: HTTP {(int)response.StatusCode}";
            }

            perTargetResults.Add(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            perTargetResults.Add($"{target.Host}: {ex.GetType().Name}");
        }

        evidence["target_results"] = perTargetResults;
        evidence["success_count"] = successCount;
        evidence["request_made"] = true; // 证明实际发出了请求

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
/// 修正（阶段 2.1）：使用系统默认代理路径，明确记录实际使用的代理来源。
/// 如果系统没有代理并决定复用 WEB-02 结果，返回 Skipped 或明确的 Reused 证据，
/// 不以 <1ms Pass 冒充一次独立 HTTPS 请求。
/// </summary>
public sealed class SystemProxyHttpsProbe : WindowsProbeBase
{
    public override string Id => "WEB-03";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    // 使用系统默认代理
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
        var evidence = new Dictionary<string, object?>
        {
            ["use_proxy"] = true, // 明确标记：使用系统代理
        };

        // 先检查系统是否配置了代理
        bool systemProxyEnabled = IsSystemProxyEnabled();
        evidence["system_proxy_enabled"] = systemProxyEnabled;

        if (!systemProxyEnabled)
        {
            // 系统没有代理：不冒充独立请求，返回 Skipped + Reused 证据
            evidence["reused_from"] = "WEB-02";
            evidence["request_made"] = false;
            return new ProbeResult(
                Id: this.Id,
                Status: ProbeStatus.Skipped,
                Severity: ProbeSeverity.Info,
                SummaryKey: "probe.web.proxy.skipped_no_proxy",
                Evidence: evidence.AsReadOnly(),
                Duration: TimeSpan.Zero,
                StartedAt: DateTimeOffset.UtcNow);
        }

        // 系统有代理：执行真实系统代理路径请求
        evidence["request_made"] = true;
        var target = HealthTargetCatalog.IndependentTarget;
        var perTargetResults = new List<string>();
        int successCount = 0;

        try
        {
            var url = $"https://{target.Host}{target.ExpectedPath}";
            var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            string result;
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                bool contentOk = string.IsNullOrEmpty(target.ExpectedContentFragment) ||
                    body.Contains(target.ExpectedContentFragment, StringComparison.OrdinalIgnoreCase);
                if (contentOk)
                {
                    successCount++;
                    result = $"{target.Host}: OK ({(int)response.StatusCode})";
                }
                else
                {
                    result = $"{target.Host}: content mismatch ({(int)response.StatusCode})";
                }
            }
            else
            {
                result = $"{target.Host}: HTTP {(int)response.StatusCode}";
            }

            perTargetResults.Add(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            perTargetResults.Add($"{target.Host}: {ex.GetType().Name}");
        }

        evidence["target_results"] = perTargetResults;
        evidence["success_count"] = successCount;

        if (successCount >= 1)
        {
            return ProbeResult.Pass(this.Id, "probe.web.proxy.ok",
                evidence: evidence.AsReadOnly());
        }

        return ProbeResult.Fail(this.Id, "probe.web.proxy.fail",
            evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
    }

    private static bool IsSystemProxyEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            int proxyEnable = (int)(key?.GetValue("ProxyEnable") ?? 0);
            return proxyEnable != 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// WEB-04: 认证门户检测探针。
/// 修正（阶段 2.1）：使用 NCSI 正文验证目标，
/// 检测预期内容被重定向或替换。使用全球服务路径作为辅助。
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

        // 使用全球服务路径目标（Google 204）
        // 注意：此目标失败不得判定断网，仅作为认证门户辅助信号
        var target = HealthTargetCatalog.GlobalPathTarget;

        try
        {
            var url = $"https://{target.Host}{target.ExpectedPath}";
            var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            evidence["target_host"] = target.Host;
            evidence["status_code"] = (int)response.StatusCode;
            evidence["final_url"] = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
            evidence["target_category"] = HealthTargetCategory.GlobalServicePath.ToString();

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
            // 全球服务路径目标失败不等于断网
            return ProbeResult.Skip(this.Id, "probe.web.captive.skip");
        }
    }
}
