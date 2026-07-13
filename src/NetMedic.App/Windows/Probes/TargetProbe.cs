using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// TARGET-01: 用户指定目标的 DNS/TCP/TLS 探针。
/// 对应任务书 §9.1 输入安全：只允许 HTTP/HTTPS 目标，安全规范化用户输入，
/// 拒绝凭据、换行、命令字符和异常长度。
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
        var host = UrlNormalizer.NormalizeToHost(_userInput);

        if (host is null)
        {
            evidence["input_rejected"] = true;
            return ProbeResult.Fail(this.Id, "probe.target.invalid_input",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        evidence["target_host"] = host;
        evidence["input_rejected"] = false;

        // 阶段 1: DNS 解析
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            evidence["resolved_addresses"] = addresses.Select(a => a.ToString()).ToList();

            if (addresses.Length == 0)
            {
                return ProbeResult.Fail(this.Id, "probe.target.dns_fail",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["dns_error"] = ex.GetType().Name;
            return ProbeResult.Fail(this.Id, "probe.target.dns_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        // 阶段 2: TCP 443 连接
        bool tcpOk = false;
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(addresses[0], 443, cancellationToken).ConfigureAwait(false);
            tcpOk = tcpClient.Connected;
            evidence["tcp_443"] = tcpOk;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["tcp_443"] = false;
            evidence["tcp_error"] = ex.GetType().Name;
            return ProbeResult.Fail(this.Id, "probe.target.tcp_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        if (!tcpOk)
        {
            return ProbeResult.Fail(this.Id, "probe.target.tcp_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }

        // 阶段 3: TLS/HTTPS 请求
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
                {
                    // 记录证书错误但不中断（只读检测）
                    if (errors != System.Net.Security.SslPolicyErrors.None)
                    {
                        evidence["tls_cert_errors"] = errors.ToString();
                    }

                    return true; // 继续以获取 HTTP 状态
                },
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

            var url = $"https://{host}/";
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            evidence["http_status"] = (int)response.StatusCode;

            // 任何 HTTP 响应都说明目标可达
            return ProbeResult.Pass(this.Id, "probe.target.ok",
                evidence: evidence.AsReadOnly());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["https_error"] = ex.GetType().Name;
            // TCP 成功但 HTTPS 失败 = TLS 层问题
            return ProbeResult.Fail(this.Id, "probe.target.tls_fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium);
        }
    }
}
