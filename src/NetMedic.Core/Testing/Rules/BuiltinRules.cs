using NetMedic.Core.Diagnostics;

namespace NetMedic.Core.Testing.Rules;

/// <summary>
/// 规则：检测失效本地代理。对应任务书 §6.3 首批规则第 1 条。
/// 证据：WinINET 代理开启 + 指向本地回环 + 端口未监听 + 直连成功。
/// 反证：受管理环境、代理程序正在启动。
/// </summary>
public sealed class DeadLocalProxyRule : IDiagnosisRule
{
    public string Id => "finding.dead_local_proxy";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var prx01 = snapshot.Get("PRX-01");
        var web02 = snapshot.Get("WEB-02");

        if (prx01 is null || web02 is null)
        {
            return null;
        }

        // PRX-01 失败（代理指向死本地端口）且直连成功
        if (prx01.Status != ProbeStatus.Failed)
        {
            return null;
        }

        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        // 反证：受保护上下文（域/RDP）。SYS-01 在受保护上下文下为 Warning，
        // 阶段 3 统一改用 Warning 判定受保护上下文（之前为 Failed）。
        var sys01 = snapshot.Get("SYS-01");
        bool protectedContext = sys01?.Status == ProbeStatus.Warning;

        // 受保护上下文下不建议用户自行修改系统代理：降级为可能，并标记降级。
        var confidence = protectedContext ? Confidence.Medium : Confidence.High;
        var actionId = protectedContext ? null : "FIX-PRX-01";

        return Finding.Create(
            id: this.Id,
            confidence: confidence,
            severity: FindingSeverity.High,
            titleKey: "finding.dead_local_proxy.title",
            explanationKey: "finding.dead_local_proxy.explanation",
            userSummaryKey: "finding.dead_local_proxy.summary",
            recommendedActionId: actionId,
            evidenceIds: ["PRX-01", "WEB-02"],
            counterEvidenceIds: protectedContext ? ["SYS-01"] : [],
            protectedContextDowngrade: protectedContext);
    }
}

/// <summary>
/// 规则：检测 DNS 路径异常。对应任务书 §6.3 首批规则第 5 条。
/// 证据：网关/直连 TCP 正常 + 系统 DNS 与配置 DNS 失败。
/// 受保护上下文（域/RDP）下系统 DNS 可能由组策略管理，降级为可能且不自动修复。
/// </summary>
public sealed class DnsFailureRule : IDiagnosisRule
{
    public string Id => "finding.dns_failure";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var dns02 = snapshot.Get("DNS-02");
        var net03 = snapshot.Get("NET-03");
        var web02 = snapshot.Get("WEB-02");

        if (dns02 is null)
        {
            return null;
        }

        // 系统 DNS 解析失败
        if (dns02.Status != ProbeStatus.Failed)
        {
            return null;
        }

        // 网关/路由存在（排除完全没有网络的情况）
        bool gatewayOk = net03?.Status == ProbeStatus.Passed;

        // 受保护上下文：DNS 可能由域策略下发，不建议用户改写。
        var sys01 = snapshot.Get("SYS-01");
        bool protectedContext = sys01?.Status == ProbeStatus.Warning;

        Confidence confidence;
        string? actionId;

        if (protectedContext)
        {
            // 受保护上下文：即使有网关，也降级为可能，且不提供自动修复。
            confidence = gatewayOk ? Confidence.Medium : Confidence.Insufficient;
            actionId = null;
        }
        else
        {
            confidence = gatewayOk ? Confidence.High : Confidence.Medium;
            actionId = "FIX-DNS-01";
        }

        return Finding.Create(
            id: this.Id,
            confidence: confidence,
            severity: FindingSeverity.High,
            titleKey: "finding.dns_failure.title",
            explanationKey: "finding.dns_failure.explanation",
            userSummaryKey: "finding.dns_failure.summary",
            recommendedActionId: actionId,
            evidenceIds: ["DNS-02", "NET-03"],
            counterEvidenceIds: protectedContext
                ? ["SYS-01"]
                : (web02?.Status == ProbeStatus.Passed ? ["WEB-02"] : []),
            protectedContextDowngrade: protectedContext);
    }
}

/// <summary>
/// 规则：检测 NCSI 状态指示异常。对应任务书 §6.3 规则第 10 条。
/// 证据：NCSI 失败但多个 HTTPS 成功 -> Windows 状态指示错误，不重置网络。
/// </summary>
public sealed class NcsiMismatchRule : IDiagnosisRule
{
    public string Id => "finding.ncsi_mismatch";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var web01 = snapshot.Get("WEB-01");
        var web02 = snapshot.Get("WEB-02");

        if (web01 is null || web02 is null)
        {
            return null;
        }

        // NCSI 不正常（Skipped/Warning/Failed 均算），但直连 HTTPS 成功
        // 阶段 2.3 修正：WEB-01 不再返回 Failed，而是 Skipped/Warning
        if (web01.Status == ProbeStatus.Passed)
        {
            return null;
        }

        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id,
            confidence: Confidence.High,
            severity: FindingSeverity.Low,
            titleKey: "finding.ncsi_mismatch.title",
            explanationKey: "finding.ncsi_mismatch.explanation",
            userSummaryKey: "finding.ncsi_mismatch.summary",
            recommendedActionId: null,
            evidenceIds: ["WEB-01", "WEB-02"],
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：检测单一目标故障。对应任务书 §6.3 规则第 8 条。
/// 证据：健康目标正常 + 只有指定目标失败 -> 单站/目标服务问题。
/// </summary>
public sealed class SingleSiteIssueRule : IDiagnosisRule
{
    public string Id => "finding.single_site_issue";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var target01 = snapshot.Get("TARGET-01");
        var web02 = snapshot.Get("WEB-02");

        if (target01 is null || web02 is null)
        {
            return null;
        }

        // 指定目标失败，但健康目标正常
        if (target01.Status != ProbeStatus.Failed)
        {
            return null;
        }

        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id,
            confidence: Confidence.High,
            severity: FindingSeverity.Info,
            titleKey: "finding.single_site_issue.title",
            explanationKey: "finding.single_site_issue.explanation",
            userSummaryKey: "finding.single_site_issue.summary",
            recommendedActionId: null,
            evidenceIds: ["TARGET-01", "WEB-02"],
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：本机网络正常。对应任务书 §6.3 默认结论。
/// 当所有核心探针通过时，返回"本机正常"。
/// </summary>
public sealed class NetworkHealthyRule : IDiagnosisRule
{
    public string Id => "finding.network_healthy";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var coreIds = new[] { "NET-01", "NET-02", "NET-03", "DNS-02", "WEB-02" };
        var allPass = true;

        foreach (var id in coreIds)
        {
            var result = snapshot.Get(id);
            if (result is null || result.Status != ProbeStatus.Passed)
            {
                allPass = false;
                break;
            }
        }

        if (!allPass)
        {
            return null;
        }

        // 如果指定目标失败，说明存在局部问题，不判定为完全健康
        // （让 SingleSiteIssueRule 等具体规则优先命中）
        var target01 = snapshot.Get("TARGET-01");
        if (target01 is { Status: ProbeStatus.Failed })
        {
            return null;
        }

        return Finding.Create(
            id: this.Id,
            confidence: Confidence.High,
            severity: FindingSeverity.Info,
            titleKey: "finding.network_healthy.title",
            explanationKey: "finding.network_healthy.explanation",
            userSummaryKey: "finding.network_healthy.summary",
            recommendedActionId: null,
            evidenceIds: coreIds,
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：检测 WinHTTP 代理异常。对应阶段 3 新增规则。
/// 证据：PRX-02 失败（WinHTTP 代理指向不可达地址）+ 直连 HTTPS 成功。
/// WinHTTP 影响系统服务/更新等非交互流量，严重程度为高。
/// </summary>
public sealed class WinHttpProxyMismatchRule : IDiagnosisRule
{
    public string Id => "finding.winhttp_proxy_mismatch";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var prx02 = snapshot.Get("PRX-02");
        var web02 = snapshot.Get("WEB-02");

        if (prx02 is null || web02 is null)
        {
            return null;
        }

        // PRX-02 失败（WinHTTP 代理异常），且直连 HTTPS 正常 -> 代理配置有误
        if (prx02.Status != ProbeStatus.Failed)
        {
            return null;
        }

        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        // 受保护上下文：WinHTTP 代理常由组策略管理，降级且不自动修复。
        var sys01 = snapshot.Get("SYS-01");
        bool protectedContext = sys01?.Status == ProbeStatus.Warning;

        var confidence = protectedContext ? Confidence.Medium : Confidence.High;
        var actionId = protectedContext ? null : "FIX-PRX-03";

        return Finding.Create(
            id: this.Id,
            confidence: confidence,
            severity: FindingSeverity.High,
            titleKey: "finding.winhttp_proxy_mismatch.title",
            explanationKey: "finding.winhttp_proxy_mismatch.explanation",
            userSummaryKey: "finding.winhttp_proxy_mismatch.summary",
            recommendedActionId: actionId,
            evidenceIds: ["PRX-02", "WEB-02"],
            counterEvidenceIds: protectedContext ? ["SYS-01"] : [],
            protectedContextDowngrade: protectedContext);
    }
}

/// <summary>
/// 规则：检测 PAC 脚本不可达。对应阶段 3 新增规则。
/// 证据：PRX-03 失败（PAC 启用但不可达）。
/// 反证：受保护上下文（企业域）下 PAC 常由组策略下发，可能是预期的临时离线，
///       降级为可能且不自动修复。
/// </summary>
public sealed class PacUnreachableRule : IDiagnosisRule
{
    public string Id => "finding.pac_unreachable";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var prx03 = snapshot.Get("PRX-03");

        if (prx03 is null)
        {
            return null;
        }

        // PRX-03 失败（PAC 启用但不可达）
        if (prx03.Status != ProbeStatus.Failed)
        {
            return null;
        }

        // 反证：受保护上下文（企业域/RDP）。PAC 通常由企业 IT 统一管理，
        // 不可达可能是临时的或预期的，降级为可能，且不提供自动修复。
        var sys01 = snapshot.Get("SYS-01");
        bool protectedContext = sys01?.Status == ProbeStatus.Warning;

        var confidence = protectedContext ? Confidence.Medium : Confidence.High;
        var actionId = protectedContext ? null : "FIX-PRX-02";

        return Finding.Create(
            id: this.Id,
            confidence: confidence,
            severity: FindingSeverity.Medium,
            titleKey: "finding.pac_unreachable.title",
            explanationKey: "finding.pac_unreachable.explanation",
            userSummaryKey: "finding.pac_unreachable.summary",
            recommendedActionId: actionId,
            evidenceIds: ["PRX-03"],
            counterEvidenceIds: protectedContext ? ["SYS-01"] : [],
            protectedContextDowngrade: protectedContext);
    }
}

/// <summary>
/// 规则：检测 APIPA 自动私有地址（DHCP 失效）。对应阶段 3 新增规则。
/// 证据：NET-02 失败（APIPA 地址 169.254.x.x）+ 网卡存在（NET-01 正常）。
/// APIPA 说明 DHCP 未分配地址，本机无法正常上网。
/// </summary>
public sealed class ApipaDhcpRule : IDiagnosisRule
{
    public string Id => "finding.apipa_dhcp";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var net01 = snapshot.Get("NET-01");
        var net02 = snapshot.Get("NET-02");

        if (net01 is null || net02 is null)
        {
            return null;
        }

        // 网卡存在但 IP 为 APIPA（NET-02 失败）。FakeProbeSet 在 APIPA 时
        // 以 summaryKey "probe.net.apipa" 标记；这里仅按状态判定。
        if (net02.Status != ProbeStatus.Failed)
        {
            return null;
        }

        // 网卡必须存在，否则属于另一类故障（无网卡）。
        if (net01.Status != ProbeStatus.Passed)
        {
            return null;
        }

        // 受保护上下文：域/RDP 环境下 DHCP 通常由基础设施管理，
        // 本机手动操作风险较高，降级为可能且不自动修复。
        var sys01 = snapshot.Get("SYS-01");
        bool protectedContext = sys01?.Status == ProbeStatus.Warning;

        var confidence = protectedContext ? Confidence.Medium : Confidence.High;
        var actionId = protectedContext ? null : "FIX-DHCP-01";

        return Finding.Create(
            id: this.Id,
            confidence: confidence,
            severity: FindingSeverity.High,
            titleKey: "finding.apipa_dhcp.title",
            explanationKey: "finding.apipa_dhcp.explanation",
            userSummaryKey: "finding.apipa_dhcp.summary",
            recommendedActionId: actionId,
            evidenceIds: ["NET-02", "NET-01"],
            counterEvidenceIds: protectedContext ? ["SYS-01"] : [],
            protectedContextDowngrade: protectedContext);
    }
}

/// <summary>
/// 规则：检测强制门户（Captive Portal）。对应阶段 3 新增规则。
/// 证据：WEB-01 Warning（ncsi_signal=captive_portal_redirect，被重定向到认证页）
///       或 WEB-04 Failed（独立门户探针确认）。
/// 不提供自动修复：需要用户在浏览器中完成认证，仅给出引导。
/// </summary>
public sealed class CaptivePortalRule : IDiagnosisRule
{
    public string Id => "finding.captive_portal";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var web01 = snapshot.Get("WEB-01");
        var web04 = snapshot.Get("WEB-04");

        // 信号 A：NCSI 重定向（WEB-01 Warning + ncsi_signal=captive_portal_redirect）
        bool ncsiCaptiveSignal = false;
        if (web01 is { Status: ProbeStatus.Warning })
        {
            if (web01.Evidence.TryGetValue("ncsi_signal", out var signalObj)
                && signalObj is string signal
                && signal == "captive_portal_redirect")
            {
                ncsiCaptiveSignal = true;
            }
        }

        // 信号 B：独立门户探针确认（WEB-04 Failed）
        bool captiveProbeFailed = web04 is { Status: ProbeStatus.Failed };

        if (!ncsiCaptiveSignal && !captiveProbeFailed)
        {
            return null;
        }

        var evidenceIds = new List<string>();
        if (ncsiCaptiveSignal)
        {
            evidenceIds.Add("WEB-01");
        }

        if (captiveProbeFailed)
        {
            evidenceIds.Add("WEB-04");
        }

        // 强制门户不是本机网络故障，无需受保护上下文降级。
        return Finding.Create(
            id: this.Id,
            confidence: Confidence.High,
            severity: FindingSeverity.High,
            titleKey: "finding.captive_portal.title",
            explanationKey: "finding.captive_portal.explanation",
            userSummaryKey: "finding.captive_portal.summary",
            recommendedActionId: null,
            evidenceIds: evidenceIds,
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：目标外部服务故障。对应阶段 3 新增规则。
/// 证据：WEB-02 Passed（本机直连健康目标正常）+ TARGET-01 Failed（指定目标不可达）。
/// 说明本机网络正常，问题来自目标网站自身。
/// 与 SingleSiteIssueRule 区别：本规则面向普通用户，强调"问题不在你这边"，
/// 不提供修复动作。SingleSiteIssueRule 保留给高级/诊断视图。
/// </summary>
public sealed class ExternalServiceRule : IDiagnosisRule
{
    public string Id => "finding.external_service";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var web02 = snapshot.Get("WEB-02");
        var target01 = snapshot.Get("TARGET-01");

        if (web02 is null || target01 is null)
        {
            return null;
        }

        // 本机直连正常，但指定目标失败 -> 目标网站问题
        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        if (target01.Status != ProbeStatus.Failed)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id,
            confidence: Confidence.High,
            severity: FindingSeverity.Info,
            titleKey: "finding.external_service.title",
            explanationKey: "finding.external_service.explanation",
            userSummaryKey: "finding.external_service.summary",
            recommendedActionId: null,
            evidenceIds: ["WEB-02", "TARGET-01"],
            counterEvidenceIds: []);
    }
}
