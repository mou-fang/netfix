using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Diagnostics;
using NetMedic.Core.Diagnostics;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// SYS-01: 系统上下文探针。
/// 检查 Windows 版本、远程会话(RDP)、域/管理状态。
/// 对应任务书 §5.3 SYS-01。
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

        // 是否远程会话
        bool isRdp = IsRemoteSession();
        evidence["is_rdp_session"] = isRdp;

        // 是否域加入
        bool isDomainJoined = IsDomainJoined();
        evidence["is_domain_joined"] = isDomainJoined;

        // 判定
        if (isRdp || isDomainJoined)
        {
            // 进入保护模式信号
            return Task.FromResult(ProbeResult.Fail(this.Id, "probe.sys.managed",
                evidence: evidence.AsReadOnly(), severity: ProbeSeverity.Medium));
        }

        return Task.FromResult(ProbeResult.Pass(this.Id, "probe.sys.ok",
            evidence: evidence.AsReadOnly()));
    }

    private static bool IsRemoteSession()
    {
        // 检测 RDP 会话：通过 GetSystemMetrics(SM_REMOTESESSION)
        return GetSystemMetrics(SM_REMOTESESSION) != 0;
    }

    private static bool IsDomainJoined()
    {
        try
        {
            // 简单检查：环境变量 USERDOMAIN 与 COMPUTERNAME 不同，可能是域
            // 更准确的方式是 NetGetJoinInformation，但需要 P/Invoke
            var computerName = Environment.MachineName;
            var userDomain = Environment.UserDomainName;
            return !string.Equals(computerName, userDomain, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_REMOTESESSION = 0x1000;
}
