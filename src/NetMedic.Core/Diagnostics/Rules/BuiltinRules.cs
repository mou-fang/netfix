using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Core.Diagnostics.Rules;

/// <summary>
/// 规则：检测失效本地代理。
/// 证据：PRX-01 配置显示代理启用且 is_loopback=true + PRX-04 端口未监听 Failed + WEB-02 直连成功。
/// 不再要求 PRX-01 Failed -- PRX-01 只读配置，代理启用时仍为 Passed。
/// </summary>
public sealed class DeadLocalProxyRule : IDiagnosisRule
{
    public string Id => "finding.dead_local_proxy";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var prx01 = snapshot.Get("PRX-01");
        var prx04 = snapshot.Get("PRX-04");
        var web02 = snapshot.Get("WEB-02");

        if (prx01 is null || prx04 is null || web02 is null)
        {
            return null;
        }

        // PRX-01 必须显示代理已启用
        if (prx01.Status != ProbeStatus.Passed)
        {
            return null;
        }

        bool proxyEnabled = prx01.Evidence.TryGetValue("proxy_enabled", out var enabledObj) && enabledObj is true;
        if (!proxyEnabled)
        {
            return null;
        }

        // PRX-04 必须为 Failed（端口未监听）
        if (prx04.Status != ProbeStatus.Failed)
        {
            return null;
        }

        // WEB-02 直连必须成功
        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        bool protectedCtx = ProtectedContextEvaluator.IsProtected(snapshot);
        var counterEvidence = ProtectedContextEvaluator.GetProtectedContextEvidenceIds(snapshot);

        var confidence = protectedCtx ? Confidence.Medium : Confidence.High;
        var actionId = protectedCtx ? null : "FIX-PRX-01";

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.High,
            titleKey: "finding.dead_local_proxy.title",
            explanationKey: "finding.dead_local_proxy.explanation",
            userSummaryKey: "finding.dead_local_proxy.summary",
            recommendedActionId: actionId,
            evidenceIds: ["PRX-01", "PRX-04", "WEB-02"],
            counterEvidenceIds: counterEvidence,
            protectedContextDowngrade: protectedCtx);
    }
}

/// <summary>
/// 规则：检测 WinHTTP 自定义代理配置。
/// 当前没有真实 WinHTTP 可达性探针，不能输出 High，不能推荐 FIX-PRX-03。
/// 改为信息性提示：Confidence=Medium，无修复动作。
/// </summary>
public sealed class WinHttpProxyConfigRule : IDiagnosisRule
{
    public string Id => "finding.winhttp_proxy_config";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var prx02 = snapshot.Get("PRX-02");
        if (prx02 is null)
        {
            return null;
        }

        // PRX-02 Passed 且有自定义代理
        if (prx02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        bool hasProxy = prx02.Evidence.TryGetValue("winhttp_has_proxy", out var hpObj) && hpObj is true;
        if (!hasProxy)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id, confidence: Confidence.Medium, severity: FindingSeverity.Low,
            titleKey: "finding.winhttp_proxy_config.title",
            explanationKey: "finding.winhttp_proxy_config.explanation",
            userSummaryKey: "finding.winhttp_proxy_config.summary",
            recommendedActionId: null,
            evidenceIds: ["PRX-02"],
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：检测 PAC 脚本不可达。
/// 证据：PRX-03 Failed（PAC 启用但不可达）。
/// 保护上下文下降级。
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

        if (prx03.Status != ProbeStatus.Failed)
        {
            return null;
        }

        bool protectedCtx = ProtectedContextEvaluator.IsProtected(snapshot);
        var counterEvidence = ProtectedContextEvaluator.GetProtectedContextEvidenceIds(snapshot);

        var confidence = protectedCtx ? Confidence.Medium : Confidence.High;
        var actionId = protectedCtx ? null : "FIX-PRX-02";

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.Medium,
            titleKey: "finding.pac_unreachable.title",
            explanationKey: "finding.pac_unreachable.explanation",
            userSummaryKey: "finding.pac_unreachable.summary",
            recommendedActionId: actionId,
            evidenceIds: ["PRX-03"],
            counterEvidenceIds: counterEvidence,
            protectedContextDowngrade: protectedCtx);
    }
}

/// <summary>
/// 规则：检测 APIPA 自动私有地址（DHCP 失效）。
/// 必须验证 NET-02 evidence 的 has_apipa=true。
/// 只看到 NET-02 Failed 不得命中。
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

        // 网卡必须存在
        if (net01.Status != ProbeStatus.Passed)
        {
            return null;
        }

        // NET-02 必须为 Failed
        if (net02.Status != ProbeStatus.Failed)
        {
            return null;
        }

        // 必须验证 evidence 中的 has_apipa=true
        bool hasApipa = net02.Evidence.TryGetValue("has_apipa", out var apipaObj) && apipaObj is true;
        if (!hasApipa)
        {
            return null;
        }

        bool protectedCtx = ProtectedContextEvaluator.IsProtected(snapshot);
        var counterEvidence = ProtectedContextEvaluator.GetProtectedContextEvidenceIds(snapshot);

        var confidence = protectedCtx ? Confidence.Medium : Confidence.High;
        var actionId = protectedCtx ? null : "FIX-DHCP-01";

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.High,
            titleKey: "finding.apipa_dhcp.title",
            explanationKey: "finding.apipa_dhcp.explanation",
            userSummaryKey: "finding.apipa_dhcp.summary",
            recommendedActionId: actionId,
            evidenceIds: ["NET-02", "NET-01"],
            counterEvidenceIds: counterEvidence,
            protectedContextDowngrade: protectedCtx);
    }
}

/// <summary>
/// 规则：检测 DNS 路径异常。
/// 证据：DNS-02 Failed + NET-03 Passed（有网关）。
/// </summary>
public sealed class DnsFailureRule : IDiagnosisRule
{
    public string Id => "finding.dns_failure";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var dns02 = snapshot.Get("DNS-02");
        var net03 = snapshot.Get("NET-03");

        if (dns02 is null)
        {
            return null;
        }

        if (dns02.Status != ProbeStatus.Failed)
        {
            return null;
        }

        bool gatewayOk = net03?.Status == ProbeStatus.Passed;
        bool protectedCtx = ProtectedContextEvaluator.IsProtected(snapshot);
        var counterEvidence = new List<string>(ProtectedContextEvaluator.GetProtectedContextEvidenceIds(snapshot));

        var web02 = snapshot.Get("WEB-02");
        if (web02?.Status == ProbeStatus.Passed)
        {
            counterEvidence.Add("WEB-02");
        }

        Confidence confidence;
        string? actionId;

        if (protectedCtx)
        {
            confidence = gatewayOk ? Confidence.Medium : Confidence.Insufficient;
            actionId = null;
        }
        else
        {
            confidence = gatewayOk ? Confidence.High : Confidence.Medium;
            actionId = "FIX-DNS-01";
        }

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.High,
            titleKey: "finding.dns_failure.title",
            explanationKey: "finding.dns_failure.explanation",
            userSummaryKey: "finding.dns_failure.summary",
            recommendedActionId: actionId,
            evidenceIds: ["DNS-02", "NET-03"],
            counterEvidenceIds: counterEvidence,
            protectedContextDowngrade: protectedCtx);
    }
}

/// <summary>
/// 规则：检测 NCSI 状态指示异常。
/// 只在 WEB-01 Skipped/Inconclusive + WEB-02 Passed 时命中。
/// WEB-01 带 captive_portal_redirect 信号时不得同时生成 NCSI 不一致。
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

        // WEB-01 必须为 Skipped（Inconclusive），不是 Passed
        if (web01.Status != ProbeStatus.Skipped)
        {
            return null;
        }

        // 不得有 captive_portal_redirect 信号（那是 CaptivePortalRule 的职责）
        if (web01.Evidence.TryGetValue("ncsi_signal", out var signalObj)
            && signalObj is string signal
            && signal == "captive_portal_redirect")
        {
            return null;
        }

        // WEB-02 直连必须成功
        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id, confidence: Confidence.High, severity: FindingSeverity.Low,
            titleKey: "finding.ncsi_mismatch.title",
            explanationKey: "finding.ncsi_mismatch.explanation",
            userSummaryKey: "finding.ncsi_mismatch.summary",
            recommendedActionId: null,
            evidenceIds: ["WEB-01", "WEB-02"],
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：检测强制门户（Captive Portal）。
/// WEB-01 Warning(ncsi_signal) 或 WEB-04 Failed。
/// 两个信号都存在时为 High，只有一个时降为 Medium。
/// </summary>
public sealed class CaptivePortalRule : IDiagnosisRule
{
    public string Id => "finding.captive_portal";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var web01 = snapshot.Get("WEB-01");
        var web04 = snapshot.Get("WEB-04");

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

        bool captiveProbeFailed = web04 is { Status: ProbeStatus.Failed };

        if (!ncsiCaptiveSignal && !captiveProbeFailed)
        {
            return null;
        }

        var evidenceIds = new List<string>();
        if (ncsiCaptiveSignal) evidenceIds.Add("WEB-01");
        if (captiveProbeFailed) evidenceIds.Add("WEB-04");

        // 两个信号都存在时为 High，只有一个时降为 Medium
        bool bothSignals = ncsiCaptiveSignal && captiveProbeFailed;
        var confidence = bothSignals ? Confidence.High : Confidence.Medium;
        var severity = bothSignals ? FindingSeverity.High : FindingSeverity.Medium;

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: severity,
            titleKey: "finding.captive_portal.title",
            explanationKey: "finding.captive_portal.explanation",
            userSummaryKey: bothSignals
                ? "finding.captive_portal.summary"
                : "finding.captive_portal.summary_possible",
            recommendedActionId: null,
            evidenceIds: evidenceIds,
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：目标网站不可达（合并 SingleSiteIssue 和 ExternalService）。
/// 证据：WEB-02 Passed + TARGET-01 Failed。
/// 不允许相同证据生成两个重复 Finding。
/// </summary>
public sealed class TargetUnreachableRule : IDiagnosisRule
{
    public string Id => "finding.target_unreachable";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var web02 = snapshot.Get("WEB-02");
        var target01 = snapshot.Get("TARGET-01");

        if (web02 is null || target01 is null)
        {
            return null;
        }

        if (web02.Status != ProbeStatus.Passed)
        {
            return null;
        }

        if (target01.Status != ProbeStatus.Failed)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id, confidence: Confidence.High, severity: FindingSeverity.Info,
            titleKey: "finding.target_unreachable.title",
            explanationKey: "finding.target_unreachable.explanation",
            userSummaryKey: "finding.target_unreachable.summary",
            recommendedActionId: null,
            evidenceIds: ["WEB-02", "TARGET-01"],
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：本机网络正常。
/// 确保不会与代理、DNS、认证门户、单站故障同时成为第一结论。
/// </summary>
public sealed class NetworkHealthyRule : IDiagnosisRule
{
    public string Id => "finding.network_healthy";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        var coreIds = new[] { "NET-01", "NET-02", "NET-03", "DNS-02", "WEB-02" };
        foreach (var id in coreIds)
        {
            var result = snapshot.Get(id);
            if (result is null || result.Status != ProbeStatus.Passed)
            {
                return null;
            }
        }

        // TARGET-01 失败时不判定为完全健康
        var target01 = snapshot.Get("TARGET-01");
        if (target01 is { Status: ProbeStatus.Failed })
        {
            return null;
        }

        // PRX-04 失败（失效代理）时不判定为健康
        var prx04 = snapshot.Get("PRX-04");
        if (prx04 is { Status: ProbeStatus.Failed })
        {
            return null;
        }

        // PRX-03 失败（PAC 不可达）时不判定为健康
        var prx03 = snapshot.Get("PRX-03");
        if (prx03 is { Status: ProbeStatus.Failed })
        {
            return null;
        }

        // PRX-02 配置了自定义 WinHTTP 代理时不判定为完全健康
        // （让 WinHttpProxyConfigRule 的信息性 Finding 优先）
        var prx02 = snapshot.Get("PRX-02");
        if (prx02 is { Status: ProbeStatus.Passed }
            && prx02.Evidence.TryGetValue("winhttp_has_proxy", out var hpObj)
            && hpObj is true)
        {
            return null;
        }

        // WEB-01 Warning（认证门户信号）时不判定为健康
        var web01 = snapshot.Get("WEB-01");
        if (web01 is { Status: ProbeStatus.Warning })
        {
            return null;
        }

        return Finding.Create(
            id: this.Id, confidence: Confidence.High, severity: FindingSeverity.Info,
            titleKey: "finding.network_healthy.title",
            explanationKey: "finding.network_healthy.explanation",
            userSummaryKey: "finding.network_healthy.summary",
            recommendedActionId: null,
            evidenceIds: coreIds,
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 生产规则注册表入口。
/// MainViewModel 和生产代码通过此入口获取规则，不引用 Testing 命名空间。
/// </summary>
public static class BuiltinRuleRegistry
{
    /// <summary>
    /// 当前已实现的修复动作集合。
    /// 只有 FIX-PRX-01 和 FIX-DNS-01 在阶段 4 实现，其他不得在 UI 显示"安全修复"。
    /// </summary>
    public static IReadOnlySet<string> SupportedRepairActions { get; } = new HashSet<string>
    {
        "FIX-PRX-01",
        "FIX-DNS-01",
    };

    public static RuleRegistry CreateDefault()
    {
        var registry = new RuleRegistry();
        registry.Add(new DeadLocalProxyRule());
        registry.Add(new WinHttpProxyConfigRule());
        registry.Add(new PacUnreachableRule());
        registry.Add(new ApipaDhcpRule());
        registry.Add(new DnsFailureRule());
        registry.Add(new NcsiMismatchRule());
        registry.Add(new CaptivePortalRule());
        registry.Add(new TargetUnreachableRule());
        registry.Add(new NetworkHealthyRule());
        return registry;
    }
}
