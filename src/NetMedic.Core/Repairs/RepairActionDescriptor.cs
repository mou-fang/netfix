namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复动作风险等级。对应任务书 §7.1。
/// </summary>
public enum RepairRisk
{
    /// <summary>低风险：影响小、可回滚、通常不断网。</summary>
    Low,

    /// <summary>中风险：可能短暂断网或改变行为，但可备份恢复。</summary>
    Medium,

    /// <summary>高风险：可能需重启、影响多个网卡/协议、不能完整自动回滚。</summary>
    High,
}

/// <summary>
/// 修复动作描述符。对应任务书 §7.3 的接口元数据。
/// 阶段 1 只包含描述信息，不包含实际执行逻辑。
/// </summary>
public sealed record RepairActionDescriptor(
    string Id,
    RepairRisk Risk,
    bool RequiresElevation,
    bool InterruptsNetwork,
    bool RequiresReboot,
    bool Reversible,
    string TitleKey,
    string DescriptionKey,
    string ConfirmationKey)
{
    /// <summary>FIX-PRX-01：关闭已证实失效的当前用户手动代理。</summary>
    public static readonly RepairActionDescriptor DisableDeadLocalProxy = new(
        Id: "FIX-PRX-01",
        Risk: RepairRisk.Low,
        RequiresElevation: false,
        InterruptsNetwork: false,
        RequiresReboot: false,
        Reversible: true,
        TitleKey: "repair.disable_dead_proxy.title",
        DescriptionKey: "repair.disable_dead_proxy.desc",
        ConfirmationKey: "repair.disable_dead_proxy.confirm");

    /// <summary>FIX-DNS-01：清理 DNS 缓存。</summary>
    public static readonly RepairActionDescriptor FlushDnsCache = new(
        Id: "FIX-DNS-01",
        Risk: RepairRisk.Low,
        RequiresElevation: false,
        InterruptsNetwork: false,
        RequiresReboot: false,
        Reversible: false,
        TitleKey: "repair.flush_dns.title",
        DescriptionKey: "repair.flush_dns.desc",
        ConfirmationKey: "repair.flush_dns.confirm");
}
