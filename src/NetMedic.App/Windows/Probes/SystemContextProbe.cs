using System.Collections.Immutable;
using System.Runtime.InteropServices;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// SYS-01: 系统上下文探针。
/// 检查 Windows 版本、远程会话(RDP)、域/管理状态。
/// 对应任务书 §5.3 SYS-01。
/// 修正（阶段 2.1）：域加入状态必须使用 NetGetJoinInformation API，
/// 不能只比较用户名、域名或环境变量。
/// </summary>
public sealed class SystemContextProbe : WindowsProbeBase
{
    public override string Id => "SYS-01";

    public override TimeSpan Timeout => TimeSpan.FromSeconds(3);

    protected override Task<ProbeResult> ExecuteCoreAsync(ProbeContext context, CancellationToken cancellationToken)
    {
        var evidence = new Dictionary<string, object?>();

        // Windows 版本
        var osVer = Environment.OSVersion;
        evidence["os_version"] = $"{osVer.Version.Major}.{osVer.Version.Minor}.{osVer.Version.Build}";

        // 远程会话检测：GetSystemMetrics(SM_REMOTESESSION)
        // GetSystemMetrics 只用于远程会话等状态检测，不用于域判断
        bool isRdp = GetSystemMetrics(SM_REMOTESESSION) != 0;
        evidence["is_rdp_session"] = isRdp;

        // 域加入状态：使用 NetGetJoinInformation API（可靠方式）
        var (isDomainJoined, joinStatus) = GetJoinStatus();
        evidence["is_domain_joined"] = isDomainJoined;
        evidence["join_status"] = joinStatus;

        // 判定：RDP 或域加入是保护上下文信号，不是网络故障
        // SYS-01 返回 Warning（保护模式）而非 Failed（网络故障）
        // 对应阶段 2.2 修正：域加入和 RDP 不应返回网络 Failed
        if (isRdp || isDomainJoined)
        {
            return Task.FromResult(new ProbeResult(
                Id: this.Id,
                Status: ProbeStatus.Warning,
                Severity: ProbeSeverity.Medium,
                SummaryKey: "probe.sys.protected_context",
                Evidence: evidence.AsReadOnly(),
                Duration: TimeSpan.Zero,
                StartedAt: DateTimeOffset.UtcNow));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.sys.ok",
            evidence: evidence.AsReadOnly()));
    }

    /// <summary>
    /// 使用 NetGetJoinInformation API 获取域加入状态。
    /// 返回值：NetSetupUnknownName=0, NetSetupUnjoined=1, NetSetupWorkgroupName=2, NetSetupDomainName=3
    /// </summary>
    private static (bool isDomainJoined, string status) GetJoinStatus()
    {
        IntPtr pDomain = IntPtr.Zero;
        try
        {
            int result = NetGetJoinInformation(null, out pDomain, out NetJoinStatus status);
            if (result != 0)
            {
                return (false, "NetGetJoinInformation_failed");
            }

            string domainName = pDomain != IntPtr.Zero ? Marshal.PtrToStringUni(pDomain) ?? string.Empty : string.Empty;
            bool isDomain = status == NetJoinStatus.NetSetupDomainName;
            return (isDomain, $"{status}({domainName})");
        }
        catch
        {
            return (false, "api_error");
        }
        finally
        {
            if (pDomain != IntPtr.Zero)
            {
                NetApiBufferFree(pDomain);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetGetJoinInformation(
        string? lpServer,
        out IntPtr lpNameBuffer,
        out NetJoinStatus BufferType);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr Buffer);

    private const int SM_REMOTESESSION = 0x1000;
}

/// <summary>
/// NetGetJoinInformation 返回的加入状态枚举。
/// </summary>
public enum NetJoinStatus
{
    NetSetupUnknownStatus = 0,
    NetSetupUnjoined = 1,
    NetSetupWorkgroupName = 2,
    NetSetupDomainName = 3,
}
