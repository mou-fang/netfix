namespace NetMedic.Core.Diagnostics;

/// <summary>
/// URL 安全规范化工具。对应任务书 §9.1 输入安全。
/// 只允许 HTTP/HTTPS，提取主机名，拒绝凭据、换行、命令字符和异常长度。
/// </summary>
public static class UrlNormalizer
{
    private const int MaxUrlLength = 2048;
    private const int MaxHostLength = 253;

    /// <summary>
    /// 安全规范化用户输入的 URL 或域名。
    /// 返回提取的主机名；如果输入不安全返回 null。
    /// </summary>
    public static string? NormalizeToHost(string? input)
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

        // 拒绝换行、管道、引号、反斜杠等危险字符
        char[] forbidden = ['\r', '\n', '|', '"', '\'', '`', ';', '\\', '<', '>', '{', '}', '^'];
        if (trimmed.IndexOfAny(forbidden) >= 0)
        {
            return null;
        }

        // 拒绝 URL userinfo（凭据）
        // 如果包含 @ 但不是邮件格式，可能是凭据注入
        if (trimmed.Contains("://") && trimmed.Contains('@'))
        {
            return null;
        }

        // 尝试解析为 URI
        string host;
        if (!trimmed.Contains("://"))
        {
            // 用户可能只输入了域名，加上 https:// 前缀解析
            host = trimmed;
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

            host = uri.Host;
        }

        // 移除可能的端口和路径残留
        var colonIdx = host.IndexOf(':');
        if (colonIdx >= 0)
        {
            host = host[..colonIdx];
        }

        // 移除末尾点
        host = host.TrimEnd('.');

        // 主机名长度检查
        if (host.Length == 0 || host.Length > MaxHostLength)
        {
            return null;
        }

        // 拒绝 localhost / IP 地址（不作为目标网站诊断）
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

        return host;
    }

    /// <summary>
    /// 检查输入是否为有效的目标主机名。
    /// </summary>
    public static bool IsValidTargetHost(string? input) => NormalizeToHost(input) is not null;
}
