using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Core.Testing;

/// <summary>
/// 根据 FakeNetworkEnvironment 构建快速体检探针集。
/// 对应任务书 §5.3 快速探针目录，阶段 1 使用 Fake 实现。
/// 阶段 3 更新：从分组对象读取状态，支持新增场景。
/// </summary>
public static class FakeProbeSet
{
    /// <summary>
    /// 构建快速体检所需的全部探针。
    /// 每个探针从 FakeNetworkEnvironment 的分组对象读取对应字段并返回 Pass/Fail/Warning。
    /// </summary>
    public static IReadOnlyList<IProbe> BuildQuick(FakeNetworkEnvironment env)
    {
        _ = env;
        return
        [
            // SYS-01: 系统上下文（保护上下文检测）
            new FakeProbe("SYS-01", e => e.System.IsRdpSession || e.System.IsManagedEnvironment
                ? new ProbeResult("SYS-01", ProbeStatus.Warning, ProbeSeverity.Medium, "probe.sys.protected_context",
                    new Dictionary<string, object?> { ["is_rdp_session"] = e.System.IsRdpSession, ["is_domain_joined"] = e.System.IsManagedEnvironment }.AsReadOnly(),
                    TimeSpan.Zero, DateTimeOffset.MinValue)
                : ProbeResult.Pass("SYS-01", "probe.sys.ok")),

            // NET-01: 活动网卡（证据结构与真实探针对齐）
            // Fake 恒为单网卡：HasActiveAdapter 决定 Pass/Fail，不会产生 Warning。
            new FakeProbe("NET-01", e =>
            {
                if (!e.Adapter.HasActiveAdapter)
                {
                    return ProbeResult.Fail("NET-01", "probe.net.adapter.none",
                        evidence: new Dictionary<string, object?>
                        {
                            ["up_adapter_count"] = 0,
                            ["adapter_names"] = new List<string>(),
                        }.AsReadOnly(),
                        severity: ProbeSeverity.High);
                }

                // 健康时返回 Passed，证据字段与真实探针对齐
                return ProbeResult.Pass("NET-01", "probe.net.adapter.ok",
                    evidence: new Dictionary<string, object?>
                    {
                        ["up_adapter_count"] = 1,
                        ["adapter_names"] = new List<string> { "fake-adapter" },
                        ["candidates_with_gateway"] = new List<string> { "fake-adapter" },
                        ["candidates_without_gateway"] = new List<string>(),
                        ["primary_adapter"] = "fake-adapter",
                    }.AsReadOnly());
            }),

            // NET-02: IP 地址（含 APIPA 检测）
            new FakeProbe("NET-02", e =>
            {
                if (e.Adapter.HasApiPAAddress && !e.Adapter.HasValidIpv4)
                {
                    return ProbeResult.Fail("NET-02", "probe.net.ip.apipa",
                        evidence: new Dictionary<string, object?>
                        {
                            ["has_apipa"] = true,
                            ["has_valid_ipv4"] = false,
                        }.AsReadOnly(),
                        severity: ProbeSeverity.High);
                }

                if (e.Adapter.HasValidIpv4)
                {
                    return ProbeResult.Pass("NET-02", "probe.net.ip.ok");
                }

                return ProbeResult.Fail("NET-02", "probe.net.ip.none",
                    evidence: new Dictionary<string, object?>
                    {
                        ["has_apipa"] = false,
                        ["has_valid_ipv4"] = false,
                    }.AsReadOnly(),
                    severity: ProbeSeverity.High);
            }),

            // NET-03: 默认网关与路由
            MakeBoolProbe("NET-03", "probe.net.gateway", e => e.Adapter.HasDefaultGateway && e.Adapter.HasDefaultRoute),

            // DNS-01: DNS 配置
            MakeBoolProbe("DNS-01", "probe.dns.config", e => e.Dns.Configured),

            // DNS-02: 系统解析
            MakeBoolProbe("DNS-02", "probe.dns.resolve", e => e.Dns.SystemResolves, failSeverity: ProbeSeverity.High),

            // PRX-01: WinINET 代理（只读配置，不检测端口）
            new FakeProbe("PRX-01", e =>
            {
                if (!e.Proxy.WininetEnabled)
                {
                    return ProbeResult.Pass("PRX-01", "probe.prx.wininet.off",
                        evidence: new Dictionary<string, object?>
                        {
                            ["proxy_enabled"] = false,
                        }.AsReadOnly());
                }

                // 代理启用 -> Passed。PRX-01 只读配置，端口检测由 PRX-04 负责。
                var addr = e.Proxy.WininetAddress ?? "unknown";
                string? host = null;
                int? port = null;
                if (!string.IsNullOrEmpty(addr))
                {
                    var colon = addr.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(addr[(colon + 1)..], out var p))
                    {
                        host = addr[..colon];
                        port = p;
                    }
                    else
                    {
                        host = addr;
                    }
                }

                return ProbeResult.Pass("PRX-01", "probe.prx.wininet.active",
                    evidence: new Dictionary<string, object?>
                    {
                        ["proxy_enabled"] = true,
                        ["is_loopback"] = e.Proxy.WininetIsLoopback,
                        ["proxy_host"] = host,
                        ["proxy_port"] = port,
                    }.AsReadOnly());
            }),

            // PRX-02: WinHTTP 代理（只读配置，无可达性探针）
            new FakeProbe("PRX-02", e =>
            {
                if (!e.Proxy.WinhttpEnabled)
                {
                    return ProbeResult.Pass("PRX-02", "probe.prx.winhttp.direct",
                        evidence: new Dictionary<string, object?>
                        {
                            ["winhttp_has_proxy"] = false,
                        }.AsReadOnly());
                }

                return ProbeResult.Pass("PRX-02", "probe.prx.winhttp.custom",
                    evidence: new Dictionary<string, object?>
                    {
                        ["winhttp_has_proxy"] = true,
                    }.AsReadOnly());
            }),

            // PRX-03: PAC
            new FakeProbe("PRX-03", e =>
            {
                if (!e.Proxy.PacEnabled)
                {
                    return ProbeResult.Pass("PRX-03", "probe.prx.pac.off");
                }

                return e.Proxy.PacReachable
                    ? ProbeResult.Pass("PRX-03", "probe.prx.pac.ok")
                    : ProbeResult.Fail("PRX-03", "probe.prx.pac.unreachable", severity: ProbeSeverity.Medium);
            }),

            // PRX-04: 本地代理端口（负责端口检测）
            new FakeProbe("PRX-04", e =>
            {
                if (!e.Proxy.WininetEnabled)
                {
                    return ProbeResult.Skip("PRX-04", "probe.prx.port.no_proxy");
                }

                // 解析代理地址，与 PRX-01 保持一致，确保 host/port 证据可比对
                var addr = e.Proxy.WininetAddress ?? "unknown";
                string? host = null;
                int? port = null;
                if (!string.IsNullOrEmpty(addr))
                {
                    var colon = addr.LastIndexOf(':');
                    if (colon > 0 && int.TryParse(addr[(colon + 1)..], out var p))
                    {
                        host = addr[..colon];
                        port = p;
                    }
                    else
                    {
                        host = addr;
                    }
                }

                if (e.Proxy.WininetIsLoopback && !e.Proxy.WininetPortListening)
                {
                    return ProbeResult.Fail("PRX-04", "probe.prx.port.dead",
                        evidence: new Dictionary<string, object?>
                        {
                            ["is_loopback"] = true,
                            ["port_listening"] = false,
                            ["proxy_host"] = host,
                            ["proxy_port"] = port,
                        }.AsReadOnly(),
                        severity: ProbeSeverity.High);
                }

                return ProbeResult.Skip("PRX-04", "probe.prx.port.not_loopback");
            }),

            // WEB-01: NCSI（使用 NcsiSemanticEvaluator 语义）
            new FakeProbe("WEB-01", e =>
            {
                bool contentOk = e.Web.NcsiConnected;
                bool redirected = e.Web.CaptivePortalDetected;
                var eval = NcsiSemanticEvaluator.Evaluate(contentOk, e.Web.NcsiConnected, redirected, !contentOk);
                var evidence = new Dictionary<string, object?>();
                if (eval.Signal is not null) evidence["ncsi_signal"] = eval.Signal;
                return new ProbeResult("WEB-01", eval.Status, eval.Severity, eval.SummaryKey,
                    evidence.AsReadOnly(), TimeSpan.Zero, DateTimeOffset.MinValue);
            }),

            // WEB-02: 直连 HTTPS
            MakeBoolProbe("WEB-02", "probe.web.direct", e => e.Web.DirectHttpsOk),

            // WEB-03: 系统代理 HTTPS
            new FakeProbe("WEB-03", e =>
            {
                if (!e.Proxy.WininetEnabled && !e.Proxy.WinhttpEnabled && !e.Proxy.PacEnabled)
                {
                    return new ProbeResult("WEB-03", ProbeStatus.Skipped, ProbeSeverity.Info,
                        "probe.web.proxy.skipped_no_proxy",
                        new Dictionary<string, object?> { ["request_made"] = false, ["reused_from"] = "WEB-02" }.AsReadOnly(),
                        TimeSpan.Zero, DateTimeOffset.MinValue);
                }

                return e.Web.SystemProxyHttpsOk
                    ? ProbeResult.Pass("WEB-03", "probe.web.proxy.ok")
                    : ProbeResult.Fail("WEB-03", "probe.web.proxy.fail", severity: ProbeSeverity.Medium);
            }),

            // WEB-04: 认证门户
            new FakeProbe("WEB-04", e =>
            {
                return e.Web.CaptivePortalDetected
                    ? ProbeResult.Fail("WEB-04", "probe.web.captive.detected", severity: ProbeSeverity.High)
                    : ProbeResult.Pass("WEB-04", "probe.web.captive.none");
            }),

            // TARGET-01: 用户指定目标
            new FakeProbe("TARGET-01", e =>
            {
                return e.Web.TargetSiteResolves && e.Web.TargetSiteConnects
                    ? ProbeResult.Pass("TARGET-01", "probe.target.ok")
                    : ProbeResult.Fail("TARGET-01", "probe.target.fail", severity: ProbeSeverity.Medium);
            }),
        ];
    }

    private static FakeProbe MakeBoolProbe(
        string id,
        string summaryKey,
        Func<FakeNetworkEnvironment, bool> condition,
        ProbeSeverity failSeverity = ProbeSeverity.High)
    {
        return new FakeProbe(id, env =>
        {
            bool ok = condition(env);
            return ok
                ? ProbeResult.Pass(id, summaryKey + ".ok")
                : ProbeResult.Fail(id, summaryKey + ".fail", severity: failSeverity);
        });
    }
}
