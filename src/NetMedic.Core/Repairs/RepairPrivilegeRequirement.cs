namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复动作所需权限级别。对应任务书 §7.3。
/// </summary>
public enum RepairPrivilegeRequirement
{
    /// <summary>当前用户权限即可执行（如关闭当前用户手动代理）。</summary>
    CurrentUser,

    /// <summary>需要管理员权限（如修改系统级 WinHTTP 代理、注册表）。</summary>
    Administrator,
}
