using System.Collections.Immutable;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Repairs;

namespace NetMedic.Core.Diagnostics.Rules;

/// <summary>
/// 规则：检测失效本地代理。
/// 证据：PRX-01 配置显示代理启用且 is_loopback=true + PRX-04 端口未监听 Failed + WEB-02 直连成功。
/// 阶段 3.3 修正：
/// - 必须显式验证 is_loopback=true（非回环远程代理不可达不命中）。
/// - 必须验证 PRX-01 和 PRX-04 检查的是同一个 host/port。
/// - RecommendedActionId = null（阶段 3 无真实修复实现）。
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

        // 必须显式验证 is_loopback=true
        bool isLoopback = prx01.Evidence.TryGetValue("is_loopback", out var loopbackObj) && loopbackObj is true;
        if (!isLoopback)
        {
            return null;
        }

        // PRX-04 必须为 Failed（端口未监听）
        if (prx04.Status != ProbeStatus.Failed)
        {
            return null;
        }

        // 必须验证 PRX-01 和 PRX-04 检查的是同一个 host/port
        string? prx01Host = prx01.Evidence.TryGetValue("proxy_host", out var h1) ? h1?.ToString() : null;
        int prx01Port = prx01.Evidence.TryGetValue("proxy_port", out var p1) && p1 is int pp1 ? pp1 : 0;
        string? prx04Host = prx04.Evidence.TryGetValue("proxy_host", out var h4) ? h4?.ToString() : null;
        int prx04Port = prx04.Evidence.TryGetValue("proxy_port", out var p4) && p4 is int pp4 ? pp4 : 0;

        if (prx01Host != prx04Host || prx01Port != prx04Port)
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

        // 阶段 3.3：无真实修复实现，RecommendedActionId = null
        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.High,
            titleKey: "finding.dead_local_proxy.title",
            explanationKey: "finding.dead_local_proxy.explanation",
            userSummaryKey: "finding.dead_local_proxy.summary",
            guidanceKey: "finding.dead_local_proxy.guidance",
            recommendedActionId: null,
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
            guidanceKey: "finding.winhttp_proxy_config.guidance",
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

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.Medium,
            titleKey: "finding.pac_unreachable.title",
            explanationKey: "finding.pac_unreachable.explanation",
            userSummaryKey: "finding.pac_unreachable.summary",
            guidanceKey: "finding.pac_unreachable.guidance",
            recommendedActionId: null,
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

        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.High,
            titleKey: "finding.apipa_dhcp.title",
            explanationKey: "finding.apipa_dhcp.explanation",
            userSummaryKey: "finding.apipa_dhcp.summary",
            guidanceKey: "finding.apipa_dhcp.guidance",
            recommendedActionId: null,
            evidenceIds: ["NET-02", "NET-01"],
            counterEvidenceIds: counterEvidence,
            protectedContextDowngrade: protectedCtx);
    }
}

/// <summary>
/// 规则：检测 DNS 路径异常。
/// 证据：DNS-02 Failed + NET-03 Passed（有网关）。
/// 阶段 3.3 修正：
/// - DNS 路径异常 ≠ DNS 缓存异常。当前无 DNS-03/DNS-04 缓存探针。
/// - 不推荐 FIX-DNS-01（清缓存），因为服务器不可达不等于缓存问题。
/// - 清缓存只能在有直接缓存异常证据时推荐（未来 DnsCacheAnomalyRule）。
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

        var confidence = protectedCtx
            ? (gatewayOk ? Confidence.Medium : Confidence.Insufficient)
            : (gatewayOk ? Confidence.High : Confidence.Medium);

        // 阶段 3.3：DNS 路径异常不等于缓存异常，不推荐清缓存
        return Finding.Create(
            id: this.Id, confidence: confidence, severity: FindingSeverity.High,
            titleKey: "finding.dns_failure.title",
            explanationKey: "finding.dns_failure.explanation",
            userSummaryKey: "finding.dns_failure.summary",
            guidanceKey: "finding.dns_failure.guidance",
            recommendedActionId: null,
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
            guidanceKey: "finding.ncsi_mismatch.guidance",
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
            guidanceKey: "finding.captive_portal.guidance",
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
            guidanceKey: "finding.target_unreachable.guidance",
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

        // WEB-01 非 Passed（Warning/Skipped）时不判定为健康
        var web01 = snapshot.Get("WEB-01");
        if (web01 is not null && web01.Status != ProbeStatus.Passed)
        {
            return null;
        }

        return Finding.Create(
            id: this.Id, confidence: Confidence.High, severity: FindingSeverity.Info,
            titleKey: "finding.network_healthy.title",
            explanationKey: "finding.network_healthy.explanation",
            userSummaryKey: "finding.network_healthy.summary",
            guidanceKey: "finding.network_healthy.guidance",
            recommendedActionId: null,
            evidenceIds: coreIds,
            counterEvidenceIds: []);
    }
}

/// <summary>
/// 规则：证据不足兜底结论。
/// 当存在 Failed/Error 探针但无具体规则命中时触发。
/// 必须作为注册表中最后一条规则——具体规则优先，此规则为兜底。
/// </summary>
public sealed class InconclusiveRule : IDiagnosisRule
{
    public string Id => "finding.inconclusive";

    public Finding? Evaluate(DiagnosticSnapshot snapshot)
    {
        // 仅当存在 Failed/Error 探针时考虑
        bool hasAnyFailure = snapshot.Session.Results.Any(r =>
            r.Status is ProbeStatus.Failed or ProbeStatus.Error);

        if (!hasAnyFailure)
        {
            return null;
        }

        // 兜底规则：具体规则已在之前评估并命中。
        // 此规则作为最后一条注册，仅在没有任何具体 Finding 覆盖该失败时生效。
        var failedEvidence = snapshot.Session.Results
            .Where(r => r.Status is ProbeStatus.Failed or ProbeStatus.Error)
            .Select(r => r.Id)
            .ToList();

        return Finding.Create(
            id: this.Id,
            confidence: Confidence.Insufficient,
            severity: FindingSeverity.Medium,
            titleKey: "finding.inconclusive.title",
            explanationKey: "finding.inconclusive.explanation",
            userSummaryKey: "finding.inconclusive.summary",
            guidanceKey: "finding.inconclusive.guidance",
            recommendedActionId: null,
            evidenceIds: failedEvidence);
    }
}

/// <summary>
/// 生产规则注册表入口。
/// MainViewModel 和生产代码通过此入口获取规则，不引用 Testing 命名空间。
/// </summary>
public static class BuiltinRuleRegistry
{
    /// <summary>
    /// 生产修复动作目录。阶段 4.0：无真实 IRepairAction 实现，目录为空。
    /// 阶段 4.1 起由各动作实现注册成功后加入此目录。
    /// </summary>
    public static RepairActionCatalog ProductionRepairCatalog { get; } = new();

    /// <summary>
    /// 当前真正存在实现、可以执行的修复动作注册表。
    /// 阶段 3.3：无真实 IRepairAction 实现，此集合为空。
    /// 阶段 4.0：委托给 <see cref="ProductionRepairCatalog"/>，生产目录仍为空。
    /// 阶段 4.1 每完成一个真实动作的事务、快照、复检和安全测试后，
    /// 由动作实现注册成功后才加入此集合，不能提前批量开启。
    /// </summary>
    public static IReadOnlySet<string> ExecutableRepairActions => ProductionRepairCatalog.ExecutableActionIds;

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
        // 兜底规则必须最后注册：仅在具体规则均未命中时生效
        registry.Add(new InconclusiveRule());
        return registry;
    }
}
