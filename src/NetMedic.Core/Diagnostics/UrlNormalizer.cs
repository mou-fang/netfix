namespace NetMedic.Core.Diagnostics;

/// <summary>
/// URL 安全规范化结果。包含方案、主机、端口。
/// </summary>
public sealed record NormalizedTarget(
    string Scheme,
    string Host,
    int Port,
    bool IsTls)
{
    /// <summary>默认端口（http=80, https=443）。</summary>
    public int DefaultPort => Scheme == "https" ? 443 : 80;
}

/// <summary>
/// URL 安全规范化工具。对应任务书 §9.1 输入安全。
/// 只允许 HTTP/HTTPS，提取主机名和端口，拒绝凭据、换行、命令字符和异常长度。
/// 用户不输入协议时默认补全为 HTTPS。
/// </summary>
public static class UrlNormalizer
{
    private const int MaxUrlLength = 2048;
    private const int MaxHostLength = 253;

    // 危险字符：换行、管道、引号、反引号、分号、反斜杠、尖括号、花括号、脱字符
    private static readonly char[] ForbiddenChars = ['\r', '\n', '|', '"', '\'', '`', ';', '\\', '<', '>', '{', '}', '^'];

    /// <summary>
    /// 安全规范化用户输入的 URL 或域名。
    /// 返回完整的 NormalizedTarget（方案+主机+端口）；如果输入不安全返回 null。
    /// 用户不输入协议时默认补全为 HTTPS。
    /// </summary>
    public static NormalizedTarget? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();

        // 长度检查
        if (trimmed.Length > MaxUrlLength)
        {
            return null;
        }

        // 拒绝危险字符
        if (trimmed.IndexOfAny(ForbiddenChars) >= 0)
        {
            return null;
        }

        // 拒绝 URL userinfo（凭据）
        if (trimmed.Contains("://") && trimmed.Contains('@'))
        {
            return null;
        }

        // 解析方案和主机
        string scheme;
        string hostPort; // host:port 部分

        if (!trimmed.Contains("://"))
        {
            // 用户未输入协议，默认补全为 HTTPS
            scheme = "https";
            hostPort = trimmed;
        }
        else
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                return null;
            }

            // 只允许 http 和 https
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            scheme = uri.Scheme;
            // 提取 host:port（Uri.Host 不含端口，需要用 Authority 或手动提取）
            hostPort = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        // 从 hostPort 中分离 host 和 port
        string host;
        int port = 0;
        var colonIdx = hostPort.LastIndexOf(':');
        if (colonIdx >= 0 && int.TryParse(hostPort[(colonIdx + 1)..], out var explicitPort))
        {
            host = hostPort[..colonIdx];
            port = explicitPort;
        }
        else
        {
            host = hostPort;
        }

        // 移除末尾点
        host = host.TrimEnd('.');

        // 主机名长度检查
        if (host.Length == 0 || host.Length > MaxHostLength)
        {
            return null;
        }

        // 拒绝 localhost
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 基本格式验证：只允许字母数字、点、连字符
        foreach (var c in host)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
            {
                return null;
            }
        }

        // 端口范围检查
        if (port < 0 || port > 65535)
        {
            return null;
        }

        bool isTls = scheme == "https";
        int finalPort = port > 0 ? port : (isTls ? 443 : 80);

        return new NormalizedTarget(scheme, host, finalPort, isTls);
    }

    /// <summary>
    /// 安全规范化用户输入，返回主机名。向后兼容旧接口。
    /// </summary>
    public static string? NormalizeToHost(string? input)
    {
        return Normalize(input)?.Host;
    }

    /// <summary>
    /// 检查输入是否为有效的目标主机名。
    /// </summary>
    public static bool IsValidTargetHost(string? input) => NormalizeToHost(input) is not null;
}
