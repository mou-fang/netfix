using System.Net;
using System.Net.Sockets;

namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 解析后的代理端点。
/// </summary>
/// <param name="Host">主机名或 IP 地址字符串（IPv6 不带方括号）。</param>
/// <param name="Port">端口。</param>
/// <param name="IsLoopback">是否为回环地址。</param>
public sealed record ProxyEndpoint(string Host, int Port, bool IsLoopback);

/// <summary>
/// 纯函数代理配置解析器。
/// PRX-01 和 PRX-04 共用，确保 host/port/is_loopback 证据可比对。
/// 支持 IPv6（[::1]:port）和多条目格式（http=...;https=...）。
/// </summary>
public static class ProxyEndpointParser
{
    /// <summary>
    /// 解析代理配置字符串。
    /// 支持: "127.0.0.1:7890"、"localhost:7890"、"LOCALHOST:7890"、"[::1]:7890"、
    /// "http=127.0.0.1:8080;https=127.0.0.1:8443"（取第一条）。
    /// 对以下情况返回 null：空、缺少端口、端口=0、端口>65535、无效主机、
    /// 换行/控制字符、包含凭据（@）。
    /// </summary>
    public static ProxyEndpoint? TryParse(string? proxyConfiguration)
    {
        // 1. null / 空白
        if (string.IsNullOrWhiteSpace(proxyConfiguration))
        {
            return null;
        }

        // 2. 拒绝控制字符（\r、\n、\t 等）
        foreach (var ch in proxyConfiguration)
        {
            if (char.IsControl(ch))
            {
                return null;
            }
        }

        // 3. 取首条目（分号分隔）
        string raw = proxyConfiguration.Trim();
        int semi = raw.IndexOf(';');
        if (semi >= 0)
        {
            raw = raw[..semi];
        }

        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        // 4. 去除协议前缀（http=host:port）
        int eq = raw.IndexOf('=');
        if (eq >= 0)
        {
            raw = raw[(eq + 1)..].Trim();
        }

        if (raw.Length == 0)
        {
            return null;
        }

        // 5. 拒绝凭据（@）
        if (raw.Contains('@'))
        {
            return null;
        }

        // 6. 解析 host:port
        string host;
        int port;

        if (raw.StartsWith('['))
        {
            // IPv6: [::1]:port
            int close = raw.IndexOf(']');
            if (close <= 0 || close == raw.Length - 1)
            {
                // 没有闭合 ] 或 ] 后无端口
                return null;
            }

            host = raw[1..close];

            // ] 后必须紧跟 ':'
            if (raw[close + 1] != ':')
            {
                return null;
            }

            string portPart = raw[(close + 2)..];
            if (!TryParsePort(portPart, out port))
            {
                return null;
            }
        }
        else
        {
            // 普通主机: 在最后一个 ':' 处分割
            int lastColon = raw.LastIndexOf(':');
            if (lastColon <= 0)
            {
                // 没有端口
                return null;
            }

            host = raw[..lastColon];
            string portPart = raw[(lastColon + 1)..];

            if (!TryParsePort(portPart, out port))
            {
                return null;
            }
        }

        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        // 7. 回环判定
        bool isLoopback = DetermineLoopback(host);

        return new ProxyEndpoint(host, port, isLoopback);
    }

    private static bool TryParsePort(string? portPart, out int port)
    {
        port = 0;
        if (string.IsNullOrEmpty(portPart))
        {
            return false;
        }

        // 端口必须是纯数字
        if (!int.TryParse(portPart, out port))
        {
            return false;
        }

        // 端口 0 无效；端口 > 65535 无效
        if (port <= 0 || port > 65535)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 回环判定：可解析为 IP 时用 IPAddress.IsLoopback，否则大小写不敏感匹配 "localhost"。
    /// </summary>
    private static bool DetermineLoopback(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return IPAddress.IsLoopback(address);
        }

        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
    }
}
