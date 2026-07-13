using System.Collections.Immutable;
using System.Net;
using System.Net.NetworkInformation;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// DNS-01: 当前 DNS 服务器配置探针。
/// 读取每个活动接口的 DNS 服务器列表。
/// </summary>
public sealed class DnsConfigProbe : WindowsProbeBase
{
    public override string Id => "DNS-01";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();
        var dnsServers = new List<string>();

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var adapter in adapters)
        {
            var props = adapter.GetIPProperties();
            foreach (var dns in props.DnsAddresses)
            {
                var addr = dns.ToString();
                if (!dnsServers.Contains(addr))
                {
                    dnsServers.Add(addr);
                }
            }
        }

        evidence["dns_server_count"] = dnsServers.Count;
        evidence["dns_servers"] = dnsServers;

        if (dnsServers.Count == 0)
        {
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.dns.config.none",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.dns.config.ok",
            evidence: evidence.AsReadOnly()));
    }
}

/// <summary>
/// DNS-02: 系统名称解析探针。
/// 使用系统解析器查询一个健康域名，判断系统 DNS 是否工作。
/// 对应任务书 §5.5：不能因单项失败下结论，但 DNS 解析是核心信号。
/// </summary>
public sealed class DnsResolveProbe : WindowsProbeBase
{
    public override string Id => "DNS-02";

    private const string TestDomain = "www.cloudflare.com";

    protected override async Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        try
        {
            // 使用系统解析器（Dns.GetHostAddressesAsync）
            var addresses = await Dns.GetHostAddressesAsync(TestDomain, cancellationToken).ConfigureAwait(false);

            evidence["test_domain"] = TestDomain;
            evidence["resolved_addresses"] = addresses.Select(a => a.ToString()).ToList();
            evidence["address_count"] = addresses.Length;

            if (addresses.Length == 0)
            {
                return ProbeResult.Fail(this.Id, "probe.dns.resolve.empty",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
            }

            return ProbeResult.Pass(this.Id, "probe.dns.resolve.ok",
                evidence: evidence.AsReadOnly());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            evidence["test_domain"] = TestDomain;
            evidence["error"] = ex.GetType().Name;
            return ProbeResult.Fail(this.Id, "probe.dns.resolve.fail",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High);
        }
    }
}
