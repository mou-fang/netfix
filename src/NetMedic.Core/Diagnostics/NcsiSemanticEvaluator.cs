namespace NetMedic.Core.Diagnostics;

/// <summary>
/// NCSI 语义评估结果。
/// </summary>
public sealed record NcsiEvaluation(
    ProbeStatus Status,
    ProbeSeverity Severity,
    string SummaryKey,
    string? Signal = null);

/// <summary>
/// NCSI 语义评估器。纯函数，NcsiProbe 和单元测试共同调用。
/// 对应阶段 2.3/2.4 修正：
/// - 正文匹配+NLM连接 = Pass
/// - 重定向 = Warning（captive-portal 信号），不单独判定认证门户
/// - 正文不匹配/超时/不可达 = Skipped（Inconclusive）
/// - NCSI 失败但 HTTPS 成功由规则引擎判断，不由 NCSI 单独声称
/// </summary>
public static class NcsiSemanticEvaluator
{
    /// <summary>
    /// 根据 NCSI 证据评估语义。
    /// </summary>
    /// <param name="contentMatches">NCSI 正文是否匹配预期内容。</param>
    /// <param name="nlmConnected">NLM COM 报告的连接状态。</param>
    /// <param name="redirected">NCSI 请求是否被重定向。</param>
    /// <param name="contentError">NCSI 正文获取是否出错（超时/不可达）。</param>
    public static NcsiEvaluation Evaluate(bool contentMatches, bool nlmConnected, bool redirected, bool contentError)
    {
        // 正文匹配 + NLM 连接 = 正常
        if (contentMatches && nlmConnected)
        {
            return new NcsiEvaluation(
                ProbeStatus.Passed, ProbeSeverity.Info, "probe.web.ncsi.ok");
        }

        // 重定向 = captive-portal 信号（Warning），不单独判定认证门户
        if (redirected)
        {
            return new NcsiEvaluation(
                ProbeStatus.Warning, ProbeSeverity.Medium,
                "probe.web.ncsi.captive_signal", Signal: "captive_portal_redirect");
        }

        // 正文不匹配、超时、目标不可达 = Inconclusive（Skipped）
        // 不由 NCSI 单独声称断网或认证门户
        return new NcsiEvaluation(
            ProbeStatus.Skipped, ProbeSeverity.Info, "probe.web.ncsi.inconclusive");
    }
}
