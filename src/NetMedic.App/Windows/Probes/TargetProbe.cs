using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// TARGET-01: 用户指定目标的 DNS/TCP/TLS/HTTP 探针。
/// 修正（阶段 2.1）：
/// - http 默认使用 80 端口且不执行 TLS。
/// - https 默认使用 443 端口并执行 TLS。
/// - 尊重 URL 显式端口，例如 https://example.com:8443。
/// - 用户不输入协议时默认补全为 HTTPS。
/// - DNS、TCP、TLS、HTTP 各阶段分别记录，不只返回"URL被接受"。
/// 对应任务书 §9.1 输入安全。
/// </summary>
public sealed class TargetProbe : WindowsProbeBase
{
    public override string Id => "TARGET-01";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(8);

    private readonly string? _userInput;

    public TargetProbe(string? userInput)
    {
        _userInput = userInput;
    }

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // 安全规范化用户输入
        var target = UrlNormalizer.Normalize(_userInput);

        if (target is null)
        {
            evidence["input_rejected"] = true;
            return ProbeResult.Fail(this.Id, "probe.target.invalid_input",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        evidence["input_rejected"] = false;
        evidence["scheme"] = target.Scheme;
        evidence["target_host"] = target.Host;
        evidence["target_port"] = target.Port;
        evidence["is_tls"] = target.IsTls;

        // 阶段 1: DNS 解析
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(target.Host, cancellationToken).ConfigureAwait(false);
            evidence["dns_addresses"] = addresses.Select(a => a.ToString()).ToList();
            evidence["dns_ok"] = true;

            if (addresses.Length == 0)
            {
                evidence["dns_ok"] = false;
                return ProbeResult.Fail(this.Id, "probe.target.dns_fail",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["dns_ok"] = false;
            evidence["dns_error"] = ex.GetType().Name;
            return ProbeResult.Fail(this.Id, "probe.target.dns_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        // 阶段 2: TCP 连接（使用正确的端口）
        bool tcpOk = false;
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(addresses[0], target.Port, cancellationToken).ConfigureAwait(false);
            tcpOk = tcpClient.Connected;
            evidence["tcp_ok"] = tcpOk;
            evidence["tcp_port"] = target.Port;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["tcp_ok"] = false;
            evidence["tcp_error"] = ex.GetType().Name;
            return ProbeResult.Fail(this.Id, "probe.target.tcp_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        if (!tcpOk)
        {
            return ProbeResult.Fail(this.Id, "probe.target.tcp_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        // 阶段 3: HTTP/HTTPS 请求
        // http: 不执行 TLS，直接 HTTP
        // https: 执行 TLS，然后 HTTP
        if (!target.IsTls)
        {
            // HTTP 模式：不执行 TLS
            evidence["tls_performed"] = false;
            try
            {
                using var handler = new HttpClientHandler { UseProxy = false };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

                var url = $"http://{target.Host}:{target.Port}/";
                var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                evidence["http_status"] = (int)response.StatusCode;
                evidence["http_ok"] = true;
                evidence["phase"] = "HTTP (no TLS)";

                return ProbeResult.Pass(this.Id, "probe.target.ok",
                    evidence: evidence.AsReadOnly());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                evidence["http_ok"] = false;
                evidence["http_error"] = ex.GetType().Name;
                return ProbeResult.Fail(this.Id, "probe.target.http_fail",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
            }
        }

        // HTTPS 模式：执行 TLS + HTTP
        evidence["tls_performed"] = true;
        try
        {
            using var handler = new HttpClientHandler
            {
                UseProxy = false,
                ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
                {
                    if (errors != SslPolicyErrors.None)
                    {
                        evidence["tls_cert_errors"] = errors.ToString();
                    }

                    return true; // 记录错误但继续以获取 HTTP 状态
                },
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

            var url = $"https://{target.Host}:{target.Port}/";
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            evidence["http_status"] = (int)response.StatusCode;
            evidence["tls_ok"] = true;
            evidence["http_ok"] = true;
            evidence["phase"] = "TLS+HTTP";

            return ProbeResult.Pass(this.Id, "probe.target.ok",
                evidence: evidence.AsReadOnly());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["tls_ok"] = ex is not HttpRequestException;
            evidence["http_ok"] = false;
            evidence["https_error"] = ex.GetType().Name;

            // TCP 成功但 HTTPS 失败 = TLS 层问题
            return ProbeResult.Fail(this.Id, "probe.target.tls_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }
    }
}
