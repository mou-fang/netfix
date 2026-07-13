using System.Collections.Immutable;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// PRX-01: 当前用户 WinINET 系统代理配置探针。
/// 修正（阶段 2.2）：PRX-01 只读取 WinINET 配置，不连接端口。
/// 端口检测由 PRX-04 负责，避免两个探针重复连接。
/// </summary>
public sealed class WininetProxyProbe : WindowsProbeBase
{
    public override string Id => "PRX-01";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(3);

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["proxy_layer"] = "WinINET",
        };

        // 读取当前用户 Internet Settings（WinINET 层）
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        if (key is null)
        {
            return Task.FromResult(ProbeResult.Err(this.Id, "probe.prx.wininet",
                new ProbeError("REGISTRY_READ_FAILED", "Cannot read Internet Settings")));
        }

        int proxyEnable = (int)(key.GetValue("ProxyEnable") ?? 0);
        string? proxyServer = key.GetValue("ProxyServer") as string;
        string? autoConfigUrl = key.GetValue("AutoConfigURL") as string;

        bool proxyEnabled = proxyEnable != 0;
        evidence["proxy_enabled"] = proxyEnabled;
        // 阶段 2.4 修正：proxy_server 和 auto_config_url 必须经过生产脱敏函数
        // 禁止把原始 AutoConfigURL 或可能含凭据的 proxy_server 写入 evidence
        evidence["proxy_server"] = UrlSanitizer.SanitizeProxyServer(proxyServer);
        evidence["auto_config_url"] = UrlSanitizer.SanitizeUrl(autoConfigUrl);

        // PRX-01 只读取配置，不检测端口
        // 阶段 3.0 隐私加固：proxy_host 必须从脱敏结果安全解析，不能从含凭据的原始 ProxyServer 解析
        // 使用共享 ProxyEndpointParser，确保 PRX-01/PRX-04/Fake 证据一致（含 IPv6 支持）
        if (proxyEnabled && !string.IsNullOrWhiteSpace(proxyServer))
        {
            var sanitizedProxy = UrlSanitizer.SanitizeProxyServer(proxyServer);
            var endpoint = ProxyEndpointParser.TryParse(sanitizedProxy);
            evidence["proxy_host"] = endpoint?.Host ?? string.Empty;
            evidence["proxy_port"] = endpoint?.Port ?? 0;
            evidence["is_loopback"] = endpoint?.IsLoopback ?? false;
        }

        if (!proxyEnabled)
        {
            return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.wininet.off",
                evidence: evidence.AsReadOnly()));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.wininet.active",
            evidence: evidence.AsReadOnly()));
    }
}

/// <summary>
/// PRX-02: WinHTTP 代理探针。
/// 使用 WinHttpGetDefaultProxyConfiguration API 读取。
/// </summary>
public sealed class WinhttpProxyProbe : WindowsProbeBase
{
    public override string Id => "PRX-02";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(3);

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["proxy_layer"] = "WinHTTP",
        };

        var config = new WinHttpProxyInfo();
        int result = WinHttpGetDefaultProxyConfiguration(ref config);

        if (result == 0)
        {
            int error = Marshal.GetLastWin32Error();
            // 即使失败也要释放可能分配的缓冲区
            FreeWinHttpStrings(config.lpszProxy, config.lpszProxyBypass);
            evidence["winhttp_api_error"] = error;
            return Task.FromResult(ProbeResult.Err(this.Id, "probe.prx.winhttp",
                new ProbeError("WINHTTP_API_FAILED", $"WinHttpGetDefaultProxyConfiguration failed: {error}")));
        }

        try
        {
            bool hasProxy = config.dwAccessType == 3;
            string? proxyString = config.lpszProxy != IntPtr.Zero
                ? Marshal.PtrToStringUni(config.lpszProxy)
                : null;
            string? bypassString = config.lpszProxyBypass != IntPtr.Zero
                ? Marshal.PtrToStringUni(config.lpszProxyBypass)
                : null;

            evidence["winhttp_access_type"] = config.dwAccessType;
            evidence["winhttp_has_proxy"] = hasProxy;
            // 阶段 2.4 修正：代理地址可能包含凭据，使用共享脱敏函数
            evidence["winhttp_proxy"] = UrlSanitizer.SanitizeProxyServer(proxyString);
            evidence["winhttp_bypass"] = bypassString ?? string.Empty;

            if (!hasProxy)
            {
                return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.winhttp.direct",
                    evidence: evidence.AsReadOnly()));
            }

            return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.winhttp.custom",
                evidence: evidence.AsReadOnly()));
        }
        finally
        {
            // 成功和异常路径都必须释放，防止内存泄漏
            FreeWinHttpStrings(config.lpszProxy, config.lpszProxyBypass);
        }
    }

    /// <summary>
    /// 释放 WinHttpGetDefaultProxyConfiguration 返回的字符串缓冲区。
    /// WinHTTP 使用 GlobalAlloc 分配，必须用 GlobalFree 释放。
    /// </summary>
    private static void FreeWinHttpStrings(IntPtr lpszProxy, IntPtr lpszProxyBypass)
    {
        if (lpszProxy != IntPtr.Zero)
        {
            GlobalFree(lpszProxy);
        }

        if (lpszProxyBypass != IntPtr.Zero)
        {
            GlobalFree(lpszProxyBypass);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinHttpProxyInfo
    {
        public int dwAccessType;
        public IntPtr lpszProxy;
        public IntPtr lpszProxyBypass;
    }

    [DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WinHttpGetDefaultProxyConfiguration(ref WinHttpProxyInfo pProxyInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}

/// <summary>
/// PRX-03: PAC 自动代理脚本探针。
/// 修正（阶段 2.2）：
/// - 分别记录 pac_configured、pac_reachable、状态码、重定向和内容检查。
/// - 只允许安全 HTTP/HTTPS PAC 地址，短超时，最大响应体 256KB。
/// - 只读取检查基本 PAC 特征，不执行 PAC JavaScript。
/// - "已配置但未检查"不得返回 PAC 正常。
/// </summary>
public sealed class PacProxyProbe : WindowsProbeBase
{
    public override string Id => "PRX-03";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(5);

    private const int MaxResponseBytes = 256 * 1024; // 256KB

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["proxy_layer"] = "PAC",
        };

        // 1. 读取 PAC 配置
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        string? autoConfigUrl = key?.GetValue("AutoConfigURL") as string;
        bool pacConfigured = !string.IsNullOrWhiteSpace(autoConfigUrl);

        evidence["pac_configured"] = pacConfigured;
        evidence["pac_url"] = pacConfigured ? UrlSanitizer.SanitizeUrl(autoConfigUrl) : string.Empty;

        if (!pacConfigured)
        {
            evidence["pac_reachable"] = false;
            evidence["pac_checked"] = true;
            return ProbeResult.Pass(this.Id, "probe.prx.pac.off",
                evidence: evidence.AsReadOnly());
        }

        // 2. 安全检查 PAC URL：只允许 HTTP/HTTPS
        if (!IsSafePacUrl(autoConfigUrl))
        {
            evidence["pac_reachable"] = false;
            evidence["pac_checked"] = false;
            evidence["pac_url_unsafe"] = true;
            // 已配置但 URL 不安全，不得返回正常
            return ProbeResult.Fail(this.Id, "probe.prx.pac.unsafe_url",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        // 3. 尝试获取 PAC 内容（不执行脚本）
        bool pacReachable = false;
        bool pacContentValid = false;
        int statusCode = 0;
        bool redirected = false;

        try
        {
            using var handler = new System.Net.Http.HttpClientHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
            };
            using var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(3),
                MaxResponseContentBufferSize = MaxResponseBytes,
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await client.GetAsync(autoConfigUrl, cts.Token).ConfigureAwait(false);
            statusCode = (int)response.StatusCode;
            redirected = statusCode is >= 300 and < 400;

            evidence["pac_http_status"] = statusCode;
            evidence["pac_redirected"] = redirected;

            if (response.IsSuccessStatusCode)
            {
                pacReachable = true;

                // 读取内容（限制大小）
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength is > MaxResponseBytes)
                {
                    evidence["pac_oversized"] = true;
                    pacReachable = false;
                }
                else
                {
                    // 读取内容检查基本 PAC 特征（不执行 JavaScript）
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    evidence["pac_body_length"] = body.Length;

                    // 基本特征检查：PAC 文件通常包含 FindProxyForURL
                    pacContentValid = body.Contains("FindProxyForURL", StringComparison.OrdinalIgnoreCase);
                    evidence["pac_has_findproxy"] = pacContentValid;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["pac_fetch_error"] = ex.GetType().Name;
        }

        evidence["pac_reachable"] = pacReachable;
        evidence["pac_checked"] = true;

        // "已配置但未检查/不可达"不得返回正常
        if (!pacReachable)
        {
            return ProbeResult.Fail(this.Id, "probe.prx.pac.unreachable",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        if (!pacContentValid)
        {
            // 可达但内容不像 PAC
            return ProbeResult.Fail(this.Id, "probe.prx.pac.invalid_content",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        return ProbeResult.Pass(this.Id, "probe.prx.pac.ok",
            evidence: evidence.AsReadOnly());
    }

    private static bool IsSafePacUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // 只允许 HTTP/HTTPS
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

}

/// <summary>
/// PRX-04: 本地代理端口探针。
/// 修正（阶段 2.2）：使用异步连接（ConnectAsync），不使用同步 .Wait()。
/// 负责端口检测（PRX-01 只读配置，不重复连接）。
/// </summary>
public sealed class LocalProxyPortProbe : WindowsProbeBase
{
    public override string Id => "PRX-04";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(3);

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>
        {
            ["proxy_layer"] = "LocalPort",
        };

        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        int proxyEnable = (int)(key?.GetValue("ProxyEnable") ?? 0);
        string? proxyServer = key?.GetValue("ProxyServer") as string;

        if (proxyEnable == 0 || string.IsNullOrWhiteSpace(proxyServer))
        {
            evidence["proxy_active"] = false;
            return ProbeResult.Skip(this.Id, "probe.prx.port.no_proxy");
        }

        var sanitizedProxy = UrlSanitizer.SanitizeProxyServer(proxyServer);
        var endpoint = ProxyEndpointParser.TryParse(sanitizedProxy);
        if (endpoint is null)
        {
            evidence["proxy_active"] = false;
            return ProbeResult.Skip(this.Id, "probe.prx.port.no_proxy");
        }

        string host = endpoint.Host;
        int port = endpoint.Port;

        evidence["proxy_host"] = host;
        evidence["proxy_port"] = port;
        evidence["is_loopback"] = endpoint.IsLoopback;

        if (!endpoint.IsLoopback)
        {
            return ProbeResult.Skip(this.Id, "probe.prx.port.not_loopback");
        }

        // 异步连接检测端口（不使用同步 .Wait()）
        bool listening = false;
        try
        {
            using var tcpClient = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(1000));
            await tcpClient.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            listening = tcpClient.Connected;
        }
        catch (OperationCanceledException)
        {
            listening = false;
        }
        catch
        {
            listening = false;
        }

        evidence["port_listening"] = listening;

        if (!listening)
        {
            return ProbeResult.Fail(this.Id, "probe.prx.port.dead",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
        }

        return ProbeResult.Pass(this.Id, "probe.prx.port.ok",
            evidence: evidence.AsReadOnly());
    }
}
