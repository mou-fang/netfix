namespace NetMedic.Core.Repairs;

/// <summary>
/// 权限评估结果。对应任务书 §7.3 权限检查。
/// </summary>
public enum ElevationDecision
{
    /// <summary>当前权限已满足，无需提权。</summary>
    NotRequired,

    /// <summary>需要提权（管理员），但当前未提权。Real 模式下应阻止执行。</summary>
    Required,

    /// <summary>用户拒绝了提权请求。</summary>
    Denied,

    /// <summary>Real 执行模式尚未启用（生产修复目录为空）。</summary>
    NotEnabled,
}

/// <summary>
/// 纯函数：评估修复动作所需权限与当前上下文是否匹配。
/// 对应任务书 §7.3 权限检查。
/// DryRun 模式不需要真实提权（仅模拟流程）。
/// </summary>
public static class PrivilegeEvaluator
{
    /// <summary>
    /// 评估动作元数据与上下文的权限匹配情况。
    /// </summary>
    /// <param name="metadata">修复动作元数据。</param>
    /// <param name="context">执行上下文。</param>
    /// <param name="realExecutionEnabled">Real 模式是否已启用（生产目录非空）。</param>
    /// <returns>权限决策结果。</returns>
    public static ElevationDecision Evaluate(
        RepairActionMetadata metadata,
        RepairContext context,
        bool realExecutionEnabled)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(context);

        // DryRun 模式不需要真实提权
        if (context.ExecutionMode == RepairExecutionMode.DryRun)
        {
            return ElevationDecision.NotRequired;
        }

        // Real 模式：生产目录为空时拒绝
        if (!realExecutionEnabled)
        {
            return ElevationDecision.NotEnabled;
        }

        // Real 模式 + 需要管理员
        if (metadata.PrivilegeRequirement == RepairPrivilegeRequirement.Administrator)
        {
            if (!context.IsElevated)
            {
                // 用户是否已被询问并拒绝
                return context.UserConfirmed
                    ? ElevationDecision.Denied
                    : ElevationDecision.Required;
            }
        }

        return ElevationDecision.NotRequired;
    }

    /// <summary>
    /// 判断权限决策是否允许执行。
    /// 只有 <see cref="ElevationDecision.NotRequired"/> 允许执行。
    /// </summary>
    public static bool CanExecute(ElevationDecision decision)
        => decision == ElevationDecision.NotRequired;
}
