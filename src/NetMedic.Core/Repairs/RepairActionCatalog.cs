using System.Collections.Immutable;

namespace NetMedic.Core.Repairs;

/// <summary>
/// 修复动作目录。注册并提供 <see cref="IRepairAction"/> 实例。
/// 对应任务书 §7.3 动作注册表。
/// 阶段 4.0：生产目录为空，无真实修复动作。
/// </summary>
public sealed class RepairActionCatalog
{
    private readonly Dictionary<string, IRepairAction> _actions = new(StringComparer.Ordinal);

    /// <summary>已注册的修复动作（只读视图）。</summary>
    public IReadOnlyCollection<IRepairAction> Actions => _actions.Values.ToImmutableList();

    /// <summary>已注册的可执行动作 ID 集合。</summary>
    public IReadOnlySet<string> ExecutableActionIds => _actions.Keys.ToImmutableHashSet();

    /// <summary>目录是否为空（无注册动作）。</summary>
    public bool IsEmpty => _actions.Count == 0;

    /// <summary>
    /// 注册一个修复动作。
    /// 拒绝：空 ActionId、重复 ActionId、缺少资源 key、缺少 ApplicableFindingIds。
    /// </summary>
    public void Register(IRepairAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var meta = action.Metadata;

        if (string.IsNullOrWhiteSpace(meta.ActionId))
        {
            throw new ArgumentException(
                "RepairActionMetadata.ActionId must not be null or empty.", nameof(action));
        }

        if (_actions.ContainsKey(meta.ActionId))
        {
            throw new InvalidOperationException(
                $"Duplicate repair action ID '{meta.ActionId}' is already registered.");
        }

        if (string.IsNullOrWhiteSpace(meta.TitleKey))
        {
            throw new ArgumentException(
                $"RepairAction '{meta.ActionId}' is missing TitleKey.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(meta.DescriptionKey))
        {
            throw new ArgumentException(
                $"RepairAction '{meta.ActionId}' is missing DescriptionKey.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(meta.ConfirmationKey))
        {
            throw new ArgumentException(
                $"RepairAction '{meta.ActionId}' is missing ConfirmationKey.", nameof(action));
        }

        if (meta.ApplicableFindingIds.Count == 0)
        {
            throw new ArgumentException(
                $"RepairAction '{meta.ActionId}' must specify at least one ApplicableFindingId.",
                nameof(action));
        }

        _actions[meta.ActionId] = action;
    }

    /// <summary>
    /// 按 ID 获取修复动作。未找到返回 null。
    /// </summary>
    public IRepairAction? GetAction(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return null;
        return _actions.TryGetValue(actionId, out var action) ? action : null;
    }

    /// <summary>
    /// 按 ID 获取修复动作。未找到抛出 <see cref="KeyNotFoundException"/>。
    /// </summary>
    public IRepairAction GetRequiredAction(string actionId)
    {
        return GetAction(actionId)
            ?? throw new KeyNotFoundException($"Repair action '{actionId}' is not registered.");
    }

    /// <summary>
    /// 判断指定 ID 的修复动作是否已注册。
    /// </summary>
    public bool Contains(string actionId)
        => !string.IsNullOrEmpty(actionId) && _actions.ContainsKey(actionId);
}
