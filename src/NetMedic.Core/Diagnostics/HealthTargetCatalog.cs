namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 健康目标配置。对应任务书 §5.4：至少两个独立路径提供的健康目标。
/// 不携带设备标识、用户名、SSID 或诊断报告。
/// 随签名版本发布，不做无签名远程热更新。
/// </summary>
public sealed record HealthTarget(
    string Host,
    string ExpectedPath,
    string ExpectedContentFragment,
    string Description);

/// <summary>
/// 健康目标目录。V1 随发布包签名发布。
/// </summary>
public static class HealthTargetCatalog
{
    /// <summary>
    /// 两个独立健康目标。
    /// 请求不携带设备标识、用户名、SSID、已访问网站列表或诊断报告。
    /// </summary>
    public static IReadOnlyList<HealthTarget> Targets { get; } =
    [
        new HealthTarget(
            Host: "www.cloudflare.com",
            ExpectedPath: "/cdn-cgi/trace",
            ExpectedContentFragment: "ip=",
            Description: "Cloudflare trace endpoint"),
        new HealthTarget(
            Host: "connectivitycheck.gstatic.com",
            ExpectedPath: "/generate_204",
            ExpectedContentFragment: string.Empty,
            Description: "Google connectivity check (204)"),
    ];
}
