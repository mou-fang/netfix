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

        // 反证：受管理环境
        var sys01 = snapshot.Get("SYS-01");
        var isManaged = sys01?.Status == ProbeStatus.Failed;

        var confidence = isManaged ? Confidence.Insufficient : Confidence.High;

        return Finding.Create(
            id: this.Id,
            confidence: confidence,
            titleKey: "finding.dead_local_proxy.title",
            explanationKey: "finding.dead_local_proxy.explanation",
            recommendedActionId: isManaged ? null : "FIX-PRX-01",
            evidenceIds: ["PRX-01", "WEB-02"],
            counterEvidenceIds: isManaged ? ["SYS-01"] : []);
    }
}

/// <summary>
/// 规则：检测 DNS 路径异常。对应任务书 §6.3 首批规则第 5 条。
/// 证据：网关/直连 TCP 正常 + 系统 DNS 与配置 DNS 失败。
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

        return Finding.Create(
            id: this.Id,
            confidence: gatewayOk ? Confidence.High : Confidence.Medium,
            titleKey: "finding.dns_failure.title",
            explanationKey: "finding.dns_failure.explanation",
            recommendedActionId: "FIX-DNS-01",
            evidenceIds: ["DNS-02", "NET-03"],
            counterEvidenceIds: web02?.Status == ProbeStatus.Passed ? ["WEB-02"] : []);
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

        // NCSI 报无网，但直连 HTTPS 成功
        if (web01.Status != ProbeStatus.Failed)
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
            titleKey: "finding.ncsi_mismatch.title",
            explanationKey: "finding.ncsi_mismatch.explanation",
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
            titleKey: "finding.single_site_issue.title",
            explanationKey: "finding.single_site_issue.explanation",
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
            titleKey: "finding.network_healthy.title",
            explanationKey: "finding.network_healthy.explanation",
            recommendedActionId: null,
            evidenceIds: coreIds,
            counterEvidenceIds: []);
    }
}
