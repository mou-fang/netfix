namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 健康目标的用途分类。对应任务书 §5.4 + 阶段 2.1 修正。
/// 不同分类有不同的语义和失败影响。
/// </summary>
public enum HealthTargetCategory
{
    /// <summary>
    /// NCSI 正文验证目标。关闭自动重定向并验证预期正文。
    /// 主要用于认证门户检测。失败不直接判定断网。
    /// </summary>
    NcsiContentCheck,

    /// <summary>
    /// 独立 HTTPS 目标。用于直连和系统代理路径验证。
    /// </summary>
    IndependentHttps,

    /// <summary>
    /// 全球服务路径目标。失败不得用于判断整个互联网不可用，
    /// 也不能与断网 Finding 直接绑定。
    /// </summary>
    GlobalServicePath,
}

/// <summary>
/// 健康目标配置。对应任务书 §5.4：至少两个独立路径提供的健康目标。
/// 不携带设备标识、用户名、SSID 或诊断报告。
/// 随签名版本发布，不做无签名远程热更新。
/// </summary>
public sealed record HealthTarget(
    string Host,
    string ExpectedPath,
    string ExpectedContentFragment,
    HealthTargetCategory Category,
    bool DisableRedirect,
    string Description);

/// <summary>
/// 健康目标目录。V1 随发布包签名发布。
/// 每个目标单独记录结果；任一目标失败不能直接判定断网。
/// </summary>
public static class HealthTargetCatalog
{
    /// <summary>
    /// 健康目标列表。
    /// - NCSI 正文验证：用于认证门户检测，关闭重定向，验证 "Microsoft Connect Test"。
    /// - 独立 HTTPS：Cloudflare trace，用于直连/代理路径验证。
    /// - 全球服务路径：Google 204，可选，失败不判定断网。
    /// </summary>
    public static IReadOnlyList<HealthTarget> Targets { get; } =
    [
        new HealthTarget(
            Host: "www.msftconnecttest.com",
            ExpectedPath: "/connecttest.txt",
            ExpectedContentFragment: "Microsoft Connect Test",
            Category: HealthTargetCategory.NcsiContentCheck,
            DisableRedirect: true,
            Description: "Windows NCSI official content check"),
        new HealthTarget(
            Host: "www.cloudflare.com",
            ExpectedPath: "/cdn-cgi/trace",
            ExpectedContentFragment: "ip=",
            Category: HealthTargetCategory.IndependentHttps,
            DisableRedirect: false,
            Description: "Cloudflare trace endpoint (independent HTTPS)"),
        new HealthTarget(
            Host: "connectivitycheck.gstatic.com",
            ExpectedPath: "/generate_204",
            ExpectedContentFragment: string.Empty,
            Category: HealthTargetCategory.GlobalServicePath,
            DisableRedirect: false,
            Description: "Google connectivity check 204 (global service path, failure does not mean internet down)"),
    ];

    /// <summary>NCSI 正文验证目标。</summary>
    public static HealthTarget NcsiTarget => Targets[0];

    /// <summary>独立 HTTPS 目标。</summary>
    public static HealthTarget IndependentTarget => Targets[1];

    /// <summary>全球服务路径目标（可选，失败不判定断网）。</summary>
    public static HealthTarget GlobalPathTarget => Targets[2];
}
