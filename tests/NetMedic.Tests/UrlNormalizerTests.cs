using NetMedic.Core.Diagnostics;

namespace NetMedic.Tests;

/// <summary>
/// URL 安全规范化测试。对应任务书 §9.1 输入安全。
/// 必须拒绝凭据、换行、命令字符和异常长度。
/// 对应任务书 §11.2 场景 L22：恶意 URL/路径/换行/引号输入。
/// </summary>
public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://www.example.com/path", "www.example.com")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("www.example.com:443", "www.example.com")]
    [InlineData("https://example.com./path", "example.com")]
    [InlineData("sub.domain.example.com", "sub.domain.example.com")]
    public void NormalizeToHost_ValidInput_ReturnsHost(string input, string expected)
    {
        var result = UrlNormalizer.NormalizeToHost(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://user:pass@example.com")]      // URL 凭据
    [InlineData("https://example.com\nrm -rf /")]       // 换行
    [InlineData("example.com|whoami")]                  // 管道
    [InlineData("example.com;del /f")]                  // 分号命令
    [InlineData("\"example.com\"")]                     // 引号
    [InlineData("example.com`whoami`")]                 // 反引号
    [InlineData("ftp://example.com")]                   // 非 HTTP/HTTPS
    [InlineData("localhost")]                           // localhost
    [InlineData("")]                                     // 空
    [InlineData("   ")]                                  // 空白
    public void NormalizeToHost_InvalidInput_ReturnsNull(string input)
    {
        var result = UrlNormalizer.NormalizeToHost(input);
        Assert.Null(result);
    }

    [Fact]
    public void NormalizeToHost_RejectsExcessiveLength()
    {
        var input = new string('a', 3000) + ".example.com";
        var result = UrlNormalizer.NormalizeToHost(input);
        Assert.Null(result);
    }

    [Fact]
    public void NormalizeToHost_RejectsBackslash()
    {
        var result = UrlNormalizer.NormalizeToHost("example.com\\..\\..\\windows");
        Assert.Null(result);
    }

    [Fact]
    public void IsValidTargetHost_ConsistentWithNormalize()
    {
        Assert.True(UrlNormalizer.IsValidTargetHost("https://www.example.com"));
        Assert.False(UrlNormalizer.IsValidTargetHost("ftp://example.com"));
        Assert.False(UrlNormalizer.IsValidTargetHost(""));
    }

    [Fact]
    public void HealthTargetCatalog_HasAtLeastTwoTargets()
    {
        // 对应任务书 §5.4：至少两个独立健康目标
        Assert.True(HealthTargetCatalog.Targets.Count >= 2);
        // 两个不同的 host
        var hosts = HealthTargetCatalog.Targets.Select(t => t.Host).Distinct().ToList();
        Assert.True(hosts.Count >= 2);
    }
}
