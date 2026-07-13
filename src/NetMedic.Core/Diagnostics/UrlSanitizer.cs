namespace NetMedic.Core.Diagnostics;

/// <summary>
/// URL 隐私脱敏工具。对应任务书 §9.3 报告脱敏。
/// PAC、WinINET、WinHTTP 代理地址和 AutoConfigURL 共同调用此函数。
/// 移除 userinfo、query、fragment，不记录代理密码或 URL token。
/// </summary>
public static class UrlSanitizer
{
    /// <summary>
    /// 脱敏 URL：移除 userinfo、query、fragment。
    /// 只保留 scheme://host:port/path。
    /// 无效 URL 返回 "[invalid_url]"。
    /// </summary>
    public static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return "[invalid_url]";
            }

            var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
            return $"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath}";
        }
        catch
        {
            return "[mask_error]";
        }
    }

    /// <summary>
    /// 脱敏代理服务器地址字符串。
    /// ProxyServer 格式可能是 "host:port" 或 "http=host:port;https=host:port"。
    /// 只保留 host:port 和协议分类，移除任何凭据。
    /// </summary>
    public static string SanitizeProxyServer(string? proxyServer)
    {
        if (string.IsNullOrWhiteSpace(proxyServer))
        {
            return string.Empty;
        }

        // ProxyServer 可能包含多个条目用分号分隔
        var entries = proxyServer.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sanitized = new List<string>();

        foreach (var entry in entries)
        {
            // 条目可能是 "protocol=host:port" 或 "host:port"
            var parts = entry.Split('=', 2);
            string hostPort = parts.Length == 2 ? parts[1] : parts[0];
            string? protocol = parts.Length == 2 ? parts[0] : null;

            // 如果 hostPort 看起来是 URL（含 ://），用 SanitizeUrl 脱敏
            if (hostPort.Contains("://"))
            {
                hostPort = SanitizeUrl(hostPort);
            }

            // 移除可能的 userinfo（@ 之前的内容）
            int atIndex = hostPort.IndexOf('@');
            if (atIndex >= 0)
            {
                hostPort = hostPort[(atIndex + 1)..];
            }

            sanitized.Add(protocol is not null ? $"{protocol}={hostPort}" : hostPort);
        }

        return string.Join("; ", sanitized);
    }
}
