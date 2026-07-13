using NetMedic.Core;
using NetMedic.Core.Diagnostics;

namespace NetMedic.Tests;

/// <summary>
/// 阶段 0 冒烟测试：验证 Tests 项目对 Core 的引用关系与基础模型可用。
/// 后续阶段在此添加规则、调度、脱敏等测试。
/// </summary>
public class SmokeTests
{
    [Fact]
    public void CoreMarker_Exposes_ProductName_And_Version()
    {
        Assert.Equal("网络急救箱 NetMedic", CoreMarker.ProductName);
        Assert.False(string.IsNullOrWhiteSpace(CoreMarker.Version));
    }

    [Fact]
    public void ProbeStatus_Has_Five_Expected_Values()
    {
        // 对应任务书 §5.2：Passed/Warning/Failed/Skipped/Error
        var expected = new[]
        {
            ProbeStatus.Passed,
            ProbeStatus.Warning,
            ProbeStatus.Failed,
            ProbeStatus.Skipped,
            ProbeStatus.Error,
        };

        Assert.Equal(5, expected.Distinct().Count());
        Assert.All(expected, s => Assert.True(Enum.IsDefined(s)));
    }

    [Fact]
    public void Confidence_Has_Three_Expected_Values()
    {
        // 对应任务书 §6.2：高可信/可能/证据不足
        var expected = new[]
        {
            Confidence.High,
            Confidence.Medium,
            Confidence.Insufficient,
        };

        Assert.Equal(3, expected.Distinct().Count());
        Assert.All(expected, c => Assert.True(Enum.IsDefined(c)));
    }
}
