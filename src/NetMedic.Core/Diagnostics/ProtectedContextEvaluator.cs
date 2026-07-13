namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 保护上下文评估器。纯函数，规则和测试共同调用。
/// 对应任务书 §7.4 自动保护 + 阶段 3.1 修正。
/// 当 SYS-01 Warning（域/RDP）或 NET-01 Warning（多活动网卡）时，
/// 自动修复建议必须降级。
/// 注意：当前没有真实 VPN 证据探针，不宣称支持 VPN 保护降级。
/// </summary>
public static class ProtectedContextEvaluator
{
    /// <summary>
    /// 判断当前快照是否处于保护上下文。
    /// 保护上下文 = SYS-01 Warning（域加入/RDP）或 NET-01 Warning（多活动网卡）。
    /// </summary>
    public static bool IsProtected(DiagnosticSnapshot snapshot)
    {
        var sys01 = snapshot.Get("SYS-01");
        if (sys01 is { Status: ProbeStatus.Warning })
        {
            return true;
        }

        var net01 = snapshot.Get("NET-01");
        if (net01 is { Status: ProbeStatus.Warning })
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取保护上下文的原因探针 ID 列表（用于反证引用）。
    /// </summary>
    public static IReadOnlyList<string> GetProtectedContextEvidenceIds(DiagnosticSnapshot snapshot)
    {
        var ids = new List<string>();
        var sys01 = snapshot.Get("SYS-01");
        if (sys01 is { Status: ProbeStatus.Warning })
        {
            ids.Add("SYS-01");
        }

        var net01 = snapshot.Get("NET-01");
        if (net01 is { Status: ProbeStatus.Warning })
        {
            ids.Add("NET-01");
        }

        return ids;
    }
}
