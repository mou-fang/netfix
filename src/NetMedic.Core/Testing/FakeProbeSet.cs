using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Core.Testing;

/// <summary>
/// 根据 FakeNetworkEnvironment 构建快速体检探针集。
/// 对应任务书 §5.3 快速探针目录，阶段 1 使用 Fake 实现。
/// </summary>
public static class FakeProbeSet
{
    /// <summary>
    /// 构建快速体检所需的全部探针。
    /// 每个探针从 FakeNetworkEnvironment 读取对应字段并返回 Pass/Fail。
    /// </summary>
    public static IReadOnlyList<IProbe> BuildQuick(FakeNetworkEnvironment env)
    {
        return
        [
            MakeBoolProbe("SYS-01", "probe.sys", env.IsRdpSession, failIfTrue: true, severity: ProbeSeverity.Medium),
            MakeBoolProbe("NET-01", "probe.net.adapter", env.HasActiveAdapter),
            MakeBoolProbe("NET-02", "probe.net.ip", env.HasValidIpv4, failSeverity: ProbeSeverity.High,
                failIfApiPA: env.HasApiPAAddress),
            MakeBoolProbe("NET-03", "probe.net.gateway", env.HasDefaultGateway && env.HasDefaultRoute),
            MakeBoolProbe("DNS-01", "probe.dns.config", env.DnsConfigured),
            MakeBoolProbe("DNS-02", "probe.dns.resolve", env.SystemDnsResolves, failSeverity: ProbeSeverity.High),
            MakeProxyProbe(env),
            MakeBoolProbe("PRX-02", "probe.prx.winhttp", !env.WinhttpProxyEnabled || env.WinhttpProxyReachable,
                failIfTrue: false),
            MakeBoolProbe("PRX-03", "probe.prx.pac", !env.PacEnabled || env.PacReachable, failIfTrue: false),
            MakeBoolProbe("WEB-01", "probe.web.ncsi", env.NcsiConnected, severity: ProbeSeverity.Info),
            MakeBoolProbe("WEB-02", "probe.web.direct", env.DirectHttpsOk),
            MakeBoolProbe("WEB-03", "probe.web.proxy", env.SystemProxyHttpsOk, failIfTrue: false),
            MakeBoolProbe("WEB-04", "probe.web.captive", !env.CaptivePortalDetected, failIfTrue: true,
                severity: ProbeSeverity.High),
            MakeBoolProbe("TARGET-01", "probe.target", env.TargetSiteResolves && env.TargetSiteConnects,
                failIfTrue: false, severity: ProbeSeverity.Medium),
        ];
    }

    private static FakeProbe MakeBoolProbe(
        string id,
        string summaryKey,
        bool condition,
        bool failIfTrue = false,
        ProbeSeverity severity = ProbeSeverity.Info,
        ProbeSeverity failSeverity = ProbeSeverity.High,
        bool failIfApiPA = false)
    {
        return new FakeProbe(id, env =>
        {
            bool isFail = failIfTrue ? condition : !condition;
            if (failIfApiPA)
            {
                return ProbeResult.Fail(id, "probe.net.apipa", severity: ProbeSeverity.High);
            }

            return isFail
                ? ProbeResult.Fail(id, summaryKey + ".fail", severity: failSeverity)
                : ProbeResult.Pass(id, summaryKey + ".ok");
        });
    }

    private static FakeProbe MakeProxyProbe(FakeNetworkEnvironment env)
    {
        _ = env; // 探针在执行时从 ProbeContext 获取环境，此处不需要
        return new FakeProbe("PRX-01", e =>
        {
            if (!e.WininetProxyEnabled)
            {
                return ProbeResult.Pass("PRX-01", "probe.prx.wininet.off");
            }

            if (e.WininetProxyIsLoopback && !e.WininetProxyPortListening)
            {
                return ProbeResult.Fail("PRX-01", "probe.prx.wininet.dead_local",
                    evidence: new Dictionary<string, object?>
                    {
                        ["proxy_address"] = e.WininetProxyAddress ?? "unknown",
                        ["is_loopback"] = true,
                        ["port_listening"] = false,
                    }.AsReadOnly(),
                    severity: ProbeSeverity.High);
            }

            return ProbeResult.Pass("PRX-01", "probe.prx.wininet.active");
        });
    }
}
