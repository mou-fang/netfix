using NetMedic.Core.Diagnostics;

namespace NetMedic.Tests;

/// <summary>
/// ProxyEndpointParser 纯函数解析测试。
/// 不依赖网络，可在 net10.0 跨平台运行。
/// 确保 PRX-01/PRX-04/Fake 共用同一解析逻辑。
/// </summary>
public class ProxyEndpointParserTests
{
    // === 有效输入 ===

    [Fact]
    public void Parse_Ipv4Loopback_ReturnsLoopbackTrue()
    {
        var ep = ProxyEndpointParser.TryParse("127.0.0.1:7890");
        Assert.NotNull(ep);
        Assert.Equal("127.0.0.1", ep!.Host);
        Assert.Equal(7890, ep.Port);
        Assert.True(ep.IsLoopback);
    }

    [Fact]
    public void Parse_LocalhostLowercase_ReturnsLoopbackTrue()
    {
        var ep = ProxyEndpointParser.TryParse("localhost:7890");
        Assert.NotNull(ep);
        Assert.Equal("localhost", ep!.Host);
        Assert.Equal(7890, ep.Port);
        Assert.True(ep.IsLoopback);
    }

    [Fact]
    public void Parse_LocalhostUppercase_ReturnsLoopbackTrue()
    {
        var ep = ProxyEndpointParser.TryParse("LOCALHOST:7890");
        Assert.NotNull(ep);
        Assert.Equal("LOCALHOST", ep!.Host);
        Assert.Equal(7890, ep.Port);
        Assert.True(ep.IsLoopback);
    }

    [Fact]
    public void Parse_Ipv6Loopback_Bracketed_ReturnsLoopbackTrue()
    {
        var ep = ProxyEndpointParser.TryParse("[::1]:7890");
        Assert.NotNull(ep);
        Assert.Equal("::1", ep!.Host);
        Assert.Equal(7890, ep.Port);
        Assert.True(ep.IsLoopback);
    }

    [Fact]
    public void Parse_ProtocolPrefixedMultipleEntries_TakesFirst()
    {
        var ep = ProxyEndpointParser.TryParse("http=127.0.0.1:8080;https=127.0.0.1:8443");
        Assert.NotNull(ep);
        Assert.Equal("127.0.0.1", ep!.Host);
        Assert.Equal(8080, ep.Port);
        Assert.True(ep.IsLoopback);
    }

    [Fact]
    public void Parse_WhitespaceAroundValid_TrimsAndParses()
    {
        var ep = ProxyEndpointParser.TryParse("  127.0.0.1:7890  ");
        Assert.NotNull(ep);
        Assert.Equal("127.0.0.1", ep!.Host);
        Assert.Equal(7890, ep.Port);
    }

    [Fact]
    public void Parse_NonLoopbackAddress_ReturnsLoopbackFalse()
    {
        var ep = ProxyEndpointParser.TryParse("10.0.0.5:7890");
        Assert.NotNull(ep);
        Assert.Equal("10.0.0.5", ep!.Host);
        Assert.Equal(7890, ep.Port);
        Assert.False(ep.IsLoopback);
    }

    // === 无效输入（返回 null） ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ReturnsNull(string input)
    {
        var ep = ProxyEndpointParser.TryParse(input);
        Assert.Null(ep);
    }

    [Fact]
    public void Parse_Null_ReturnsNull()
    {
        var ep = ProxyEndpointParser.TryParse(null);
        Assert.Null(ep);
    }

    [Fact]
    public void Parse_NoPort_ReturnsNull()
    {
        var ep = ProxyEndpointParser.TryParse("127.0.0.1");
        Assert.Null(ep);
    }

    [Fact]
    public void Parse_PortZero_ReturnsNull()
    {
        var ep = ProxyEndpointParser.TryParse("127.0.0.1:0");
        Assert.Null(ep);
    }

    [Fact]
    public void Parse_PortTooLarge_ReturnsNull()
    {
        var ep = ProxyEndpointParser.TryParse("127.0.0.1:99999");
        Assert.Null(ep);
    }

    [Fact]
    public void Parse_ControlCharNewline_ReturnsNull()
    {
        var ep = ProxyEndpointParser.TryParse("127.0.0.1:7890\nrm");
        Assert.Null(ep);
    }

    [Fact]
    public void Parse_Credentials_ReturnsNull()
    {
        var ep = ProxyEndpointParser.TryParse("user:pass@127.0.0.1:7890");
        Assert.Null(ep);
    }
}
