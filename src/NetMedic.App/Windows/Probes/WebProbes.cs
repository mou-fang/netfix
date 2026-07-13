using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// WEB-01: Windows NCSI 连接状态探针。
/// 修正（阶段 2.2）：
/// - EnableActiveProbing 只作为配置证据，不代表当前联网状态。
/// - 使用 NLM COM 接口读取当前 Connectivity 状态。
/// - NCSI 失败但 HTTPS 成功时必须能生成"不一致"证据。
/// </summary>
public sealed class NcsiProbe : WindowsProbeBase
{
    public override string Id => "WEB-01";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    // NCSI 正文验证：关闭重定向，验证预期正文
    private static readonly HttpClientHandler NcsiHandler = new()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
    };

    private static readonly HttpClient NcsiClient = new(NcsiHandler)
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // 1. NCSI 配置证据（只记录配置，不代表联网状态）
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\NlaSvc\Parameters\Internet");
            var enableActiveProbing = key?.GetValue("EnableActiveProbing");
            evidence["ncsi_enable_active_probing"] = enableActiveProbing ?? "unknown";
        }
        catch
        {
            evidence["ncsi_enable_active_probing"] = "unknown";
        }

        // 2. NLM COM 读取当前连接状态
        var (nlmConnected, nlmDetail) = ReadNlmConnectivity();
        evidence["nlm_connected"] = nlmConnected;
        evidence["nlm_detail"] = nlmDetail;

        // 3. NCSI 正文验证（辅助信号）
        var target = HealthTargetCatalog.NcsiTarget;
        bool ncsiContentOk = false;
        bool ncsiRedirected = false;

        try
        {
            var url = $"http://{target.Host}{target.ExpectedPath}";
            var response = await NcsiClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            evidence["ncsi_http_status"] = (int)response.StatusCode;
            ncsiRedirected = (int)response.StatusCode is >= 300 and < 400;
            evidence["ncsi_redirected"] = ncsiRedirected;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            evidence["ncsi_body_length"] = body.Length;
            ncsiContentOk = body.Contains(target.ExpectedContentFragment, StringComparison.OrdinalIgnoreCase);
            evidence["ncsi_content_matches"] = ncsiContentOk;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["ncsi_content_error"] = ex.GetType().Name;
        }

        // 4. 综合判断（调用生产纯函数 NcsiSemanticEvaluator）
        var eval = NcsiSemanticEvaluator.Evaluate(
            contentMatches: ncsiContentOk,
            nlmConnected: nlmConnected,
            redirected: ncsiRedirected,
            contentError: evidence.ContainsKey("ncsi_content_error"));

        if (eval.Signal is not null)
        {
            evidence["ncsi_signal"] = eval.Signal;
        }

        return new ProbeResult(
            Id: this.Id,
            Status: eval.Status,
            Severity: eval.Severity,
            SummaryKey: eval.SummaryKey,
            Evidence: evidence.AsReadOnly(),
            Duration: TimeSpan.Zero,
            StartedAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 使用 NLM COM 接口读取当前连接状态。
    /// CLSID: {DCB00C01-570F-4A9B-8D69-199FDBA5723B} (INetworkListManager)
    /// </summary>
    private static (bool connected, string detail) ReadNlmConnectivity()
    {
        try
        {
            // 通过 COM 创建 INetworkListManager
            var nlmType = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
            if (nlmType is null)
            {
                return (false, "NLM_COM unavailable");
            }

            dynamic nlm = Activator.CreateInstance(nlmType)!;
            try
            {
                bool isConnected = nlm.IsConnectedToInternet;
                return (isConnected, $"NLM IsConnectedToInternet={isConnected}");
            }
            finally
            {
                Marshal.ReleaseComObject(nlm);
            }
        }
        catch (Exception ex)
        {
            return (false, $"NLM error: {ex.GetType().Name}");
        }
    }
}

/// <summary>
/// WEB-02: 直连 HTTPS 探针。
/// 修正（阶段 2.2）：删除无条件 return true 的证书验证回调。
/// 使用严格 TLS 验证：只在 SslPolicyErrors.None 时接受。
/// 有证书错误时不得标记为 Pass。
/// </summary>
public sealed class DirectHttpsProbe : WindowsProbeBase
{
    public override string Id => "WEB-02";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["use_proxy"] = false,
            ["proxy_source"] = "none",
            ["connection_path"] = "direct",
        };

        var target = HealthTargetCatalog.IndependentTarget;
        var perTargetResults = new List<string>();
        int successCount = 0;
        bool tlsError = false;

        // 每次创建新的 handler 以获取独立的 TLS 错误记录
        var (handler, tlsRecord) = TlsValidationHelper.CreateDirectStrictHandler();
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };

        try
        {
            var url = $"https://{target.Host}{target.ExpectedPath}";
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            // TLS 验证通过（否则会抛异常）
            tlsRecord.WriteTo(evidence);
            evidence["request_made"] = true;

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
            evidence["request_made"] = true;
            // 优先保留 TlsErrorRecord 中回调取得的准确分类
            // 只有 record 为 NotChecked 时才回退到 ClassifyException
            tlsRecord.FallbackFromException(ex);
            tlsRecord.WriteTo(evidence);
            tlsError = true;
            perTargetResults.Add($"{target.Host}: {tlsRecord.ErrorCategory} - {ex.GetType().Name}");
        }

        evidence["target_results"] = perTargetResults;
        evidence["success_count"] = successCount;

        if (tlsError)
        {
            return ProbeResult.Fail(this.Id, "probe.web.direct.tls_error",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
        }

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
/// 修正（阶段 2.2）：同样使用严格 TLS 验证。
/// </summary>
public sealed class SystemProxyHttpsProbe : WindowsProbeBase
{
    public override string Id => "WEB-03";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["use_proxy"] = true,
            ["connection_path"] = "system_proxy",
        };

        bool systemProxyEnabled = IsSystemProxyEnabled();
        evidence["system_proxy_enabled"] = systemProxyEnabled;

        if (!systemProxyEnabled)
        {
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

        evidence["request_made"] = true;
        var target = HealthTargetCatalog.IndependentTarget;
        var perTargetResults = new List<string>();
        int successCount = 0;
        bool tlsError = false;

        var (handler, tlsRecord) = TlsValidationHelper.CreateStrictHandler();
        handler.UseProxy = true;
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };

        try
        {
            var url = $"https://{target.Host}{target.ExpectedPath}";
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            tlsRecord.WriteTo(evidence);

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
            tlsRecord.FallbackFromException(ex);
            tlsRecord.WriteTo(evidence);
            tlsError = true;
            perTargetResults.Add($"{target.Host}: {tlsRecord.ErrorCategory} - {ex.GetType().Name}");
        }

        evidence["target_results"] = perTargetResults;
        evidence["success_count"] = successCount;

        if (tlsError)
        {
            return ProbeResult.Fail(this.Id, "probe.web.proxy.tls_error",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
        }

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
/// 修正（阶段 2.2）：使用严格 TLS 验证。全球服务路径目标失败不判定断网。
/// </summary>
public sealed class CaptivePortalProbe : WindowsProbeBase
{
    public override string Id => "WEB-04";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["target_host"] = HealthTargetCatalog.GlobalPathTarget.Host,
            ["target_category"] = HealthTargetCategory.GlobalServicePath.ToString(),
        };
        var target = HealthTargetCatalog.GlobalPathTarget;

        var (handler, tlsRecord) = TlsValidationHelper.CreateDirectStrictHandler();
        handler.AllowAutoRedirect = true;
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };

        try
        {
            var url = $"https://{target.Host}{target.ExpectedPath}";
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            tlsRecord.WriteTo(evidence);
            evidence["status_code"] = (int)response.StatusCode;
            evidence["final_url"] = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return ProbeResult.Pass(this.Id, "probe.web.captive.none",
                    evidence: evidence.AsReadOnly());
            }

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
            // 阶段 2.4 修正：调用 FallbackFromException 和 WriteTo
            // Skipped/Inconclusive 时不得丢失 evidence
            tlsRecord.FallbackFromException(ex);
            tlsRecord.WriteTo(evidence);
            evidence["error"] = ex.GetType().Name;
            // 全球服务路径目标失败不等于断网或认证门户
            return new ProbeResult(
                Id: this.Id,
                Status: ProbeStatus.Skipped,
                Severity: ProbeSeverity.Info,
                SummaryKey: "probe.web.captive.skip",
                Evidence: evidence.AsReadOnly(),
                Duration: TimeSpan.Zero,
                StartedAt: DateTimeOffset.UtcNow);
        }
    }
}
