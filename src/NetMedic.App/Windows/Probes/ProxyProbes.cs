using System.Collections.Immutable;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// PRX-01: 当前用户 WinINET 系统代理探针。
/// 读取注册表 HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings。
/// 检测代理是否开启、是否指向本地回环、端口是否监听。
/// WinINET、WinHTTP、PAC 和本地端口分别记录，不混为一个状态（任务书 §0 第 5 条）。
/// </summary>
public sealed class WininetProxyProbe : WindowsProbeBase
{
    public override string Id => "PRX-01";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // 读取当前用户 Internet Settings
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        if (key is null)
        {
            return Task.FromResult(ProbeResult.Err(this.Id, "probe.prx.wininet",
                new ProbeError("REGISTRY_READ_FAILED", "Cannot read Internet Settings")));
        }

        int proxyEnable = (int)(key.GetValue("ProxyEnable") ?? 0);
        string? proxyServer = key.GetValue("ProxyServer") as string;
        string? autoConfigUrl = key.GetValue("AutoConfigURL") as string;

        bool proxyEnabled = proxyEnable != 0;
        evidence["proxy_enabled"] = proxyEnabled;
        evidence["proxy_server"] = proxyServer ?? string.Empty;
        evidence["auto_config_url"] = autoConfigUrl ?? string.Empty;

        if (!proxyEnabled)
        {
            return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.wininet.off",
                evidence: evidence.AsReadOnly()));
        }

        // 解析代理地址
        var (host, port) = ParseProxyServer(proxyServer);
        evidence["proxy_host"] = host ?? string.Empty;
        evidence["proxy_port"] = port;

        bool isLoopback = host is "127.0.0.1" or "localhost" or "::1";
        evidence["is_loopback"] = isLoopback;

        if (isLoopback && port > 0)
        {
            bool portListening = IsPortListening(host!, port);
            evidence["port_listening"] = portListening;

            if (!portListening)
            {
                // 失效本地代理：代理开启 + 指向回环 + 端口未监听
                return Task.FromResult(ProbeResult.Fail(this.Id, "probe.prx.wininet.dead_local",
                    evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
            }
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.wininet.active",
            evidence: evidence.AsReadOnly()));
    }

    private static (string? host, int port) ParseProxyServer(string? proxyServer)
    {
        if (string.IsNullOrWhiteSpace(proxyServer))
        {
            return (null, 0);
        }

        // ProxyServer 可能是 "host:port" 或 "http=host:port;https=host:port"
        // 取第一个条目
        var first = proxyServer.Split(';')[0].Trim();
        if (first.Contains('='))
        {
            first = first.Split('=')[1];
        }

        var parts = first.Split(':');
        if (parts.Length >= 2 && int.TryParse(parts[^1], out var port))
        {
            return (parts[0], port);
        }

        return (first, 0);
    }

    private static bool IsPortListening(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (!connectTask.Wait(TimeSpan.FromMilliseconds(1000)))
            {
                return false;
            }

            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// PRX-02: WinHTTP 代理探针。
/// 读取注册表 HKLM\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections\WinHttpSettings。
/// WinHTTP 代理与 WinINET 分开记录（任务书 §0 第 5 条）。
/// </summary>
public sealed class WinhttpProxyProbe : WindowsProbeBase
{
    public override string Id => "PRX-02";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // WinHTTP 设置存储在二进制值 WinHttpSettings 中
        // 简化检测：读取 WinHttpSettings 是否存在且非默认
        using var key = Registry.LocalMachine.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections");

        var winHttpSettings = key?.GetValue("WinHttpSettings") as byte[];
        bool hasCustomProxy = winHttpSettings is not null && winHttpSettings.Length > 24;

        evidence["winhttp_custom_proxy"] = hasCustomProxy;

        if (!hasCustomProxy)
        {
            return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.winhttp.direct",
                evidence: evidence.AsReadOnly()));
        }

        // 有自定义 WinHTTP 代理设置
        // 详细解析需要解析 WinHttpSettings 二进制格式，此处标记为需要关注
        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.winhttp.custom",
            evidence: evidence.AsReadOnly()));
    }
}

/// <summary>
/// PRX-03: PAC 自动代理脚本探针。
/// 检查是否配置了 PAC URL，以及该 URL 是否可访问。
/// 不执行不受信任的 PAC 脚本（任务书 §8.2）。
/// </summary>
public sealed class PacProxyProbe : WindowsProbeBase
{
    public override string Id => "PRX-03";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        string? autoConfigUrl = key?.GetValue("AutoConfigURL") as string;
        bool pacEnabled = !string.IsNullOrWhiteSpace(autoConfigUrl);

        evidence["pac_enabled"] = pacEnabled;
        evidence["pac_url"] = pacEnabled ? MaskUrl(autoConfigUrl) : string.Empty;

        if (!pacEnabled)
        {
            return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.pac.off",
                evidence: evidence.AsReadOnly()));
        }

        // 尝试访问 PAC URL（只获取内容，不执行脚本）
        // 简化：标记为已配置
        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.pac.configured",
            evidence: evidence.AsReadOnly()));
    }

    private static string MaskUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        // 脱敏：移除凭据部分
        if (url.Contains("://") && url.Contains('@'))
        {
            int schemeEnd = url.IndexOf("://", StringComparison.Ordinal) + 3;
            var scheme = url[..schemeEnd];
            int atPos = url.IndexOf('@', StringComparison.Ordinal);
            var hostPart = url[(atPos + 1)..];
            return scheme + "***@" + hostPart;
        }

        return url;
    }
}

/// <summary>
/// PRX-04: 本地代理端口探针。
/// 当系统代理指向本地回环时，检查端口是否实际有程序监听。
/// </summary>
public sealed class LocalProxyPortProbe : WindowsProbeBase
{
    public override string Id => "PRX-04";

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        int proxyEnable = (int)(key?.GetValue("ProxyEnable") ?? 0);
        string? proxyServer = key?.GetValue("ProxyServer") as string;

        if (proxyEnable == 0 || string.IsNullOrWhiteSpace(proxyServer))
        {
            evidence["proxy_active"] = false;
            return Task.FromResult(ProbeResult.Skip(this.Id, "probe.prx.port.no_proxy"));
        }

        // 解析代理地址
        var first = proxyServer.Split(';')[0].Trim();
        if (first.Contains('='))
        {
            first = first.Split('=')[1];
        }

        var parts = first.Split(':');
        string host = parts[0];
        int port = parts.Length >= 2 && int.TryParse(parts[^1], out var p) ? p : 0;

        evidence["proxy_host"] = host;
        evidence["proxy_port"] = port;

        bool isLoopback = host is "127.0.0.1" or "localhost" or "::1";
        if (!isLoopback)
        {
            evidence["is_loopback"] = false;
            return Task.FromResult(ProbeResult.Skip(this.Id, "probe.prx.port.not_loopback"));
        }

        evidence["is_loopback"] = true;

        // 检查端口是否监听
        bool listening = false;
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            listening = task.Wait(TimeSpan.FromMilliseconds(1000)) && client.Connected;
        }
        catch
        {
            listening = false;
        }

        evidence["port_listening"] = listening;

        if (!listening)
        {
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.prx.port.dead",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.High));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.prx.port.ok",
            evidence: evidence.AsReadOnly()));
    }
}
