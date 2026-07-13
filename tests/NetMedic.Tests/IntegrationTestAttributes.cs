using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace NetMedic.Tests;

/// <summary>
/// 仅在环境变量 NETMEDIC_INTEGRATION_TESTS=1 时运行的集成测试。
/// 普通默认 dotnet test 不运行这些测试，避免依赖公网和真实 Windows 探针。
/// 使用方式：dotnet test --environment NETMEDIC_INTEGRATION_TESTS=1
/// 或 --verify-probes 模式。
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    private const string EnvVar = "NETMEDIC_INTEGRATION_TESTS";

    public IntegrationFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable(EnvVar);
        if (enabled != "1" && enabled != "true")
        {
            Skip = $"集成测试默认跳过。设置 {EnvVar}=1 启用。";
        }
    }
}

/// <summary>
/// 仅在环境变量 NETMEDIC_INTEGRATION_TESTS=1 时运行的集成理论测试。
/// </summary>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    private const string EnvVar = "NETMEDIC_INTEGRATION_TESTS";

    public IntegrationTheoryAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable(EnvVar);
        if (enabled != "1" && enabled != "true")
        {
            Skip = $"集成测试默认跳过。设置 {EnvVar}=1 启用。";
        }
    }
}
