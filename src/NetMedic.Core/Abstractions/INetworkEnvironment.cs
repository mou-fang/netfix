namespace NetMedic.Core.Abstractions;

/// <summary>
/// 网络环境抽象。阶段 1 起由 <c>NetMedic.App</c> 提供 Windows 实现，
/// 测试由 <c>FakeNetworkEnvironment</c> 提供。阶段 0 仅保留接口骨架。
/// 对应任务书 §11.1 模拟优先策略。
/// </summary>
public interface INetworkEnvironment
{
    /// <summary>环境名称，便于日志和诊断区分。</summary>
    string Name { get; }
}
