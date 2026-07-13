using NetMedic.Core.Diagnostics;

namespace NetMedic.Tests;

/// <summary>
/// 阶段 2.4 测试：直接调用生产函数，不是自我验证式测试。
/// 全部不依赖公网，可在 net10.0 上运行。
/// </summary>
public class Phase24ProductionTests
{
    // === 1. NCSI 语义测试：直接调用生产 NcsiSemanticEvaluator ===

    /// <summary>正文成功：NcsiSemanticEvaluator 返回 Pass。</summary>
    [Fact]
    public void NcsiSemanticEvaluator_ContentMatch_NlmConnected_ReturnsPass()
    {
        var result = NcsiSemanticEvaluator.Evaluate(
            contentMatches: true, nlmConnected: true,
            redirected: false, contentError: false);

        Assert.Equal(ProbeStatus.Passed, result.Status);
        Assert.Equal("probe.web.ncsi.ok", result.SummaryKey);
    }

    /// <summary>重定向：返回 Warning + captive-portal 信号，不是 Fail。</summary>
    [Fact]
    public void NcsiSemanticEvaluator_Redirected_ReturnsWarningNotFail()
    {
        var result = NcsiSemanticEvaluator.Evaluate(
            contentMatches: false, nlmConnected: false,
            redirected: true, contentError: false);

        Assert.Equal(ProbeStatus.Warning, result.Status);
        Assert.Equal("captive_portal_redirect", result.Signal);
        Assert.NotEqual(ProbeStatus.Failed, result.Status);
    }

    /// <summary>正文失败：返回 Skipped/Inconclusive，不判定断网或认证门户。</summary>
    [Fact]
    public void NcsiSemanticEvaluator_ContentMismatch_ReturnsSkipped()
    {
        var result = NcsiSemanticEvaluator.Evaluate(
            contentMatches: false, nlmConnected: true,
            redirected: false, contentError: true);

        Assert.Equal(ProbeStatus.Skipped, result.Status);
        Assert.NotEqual(ProbeStatus.Failed, result.Status);
    }

    /// <summary>NCSI 失败但 HTTPS 成功：NcsiSemanticEvaluator 返回 Skipped，规则引擎负责检测不一致。</summary>
    [Fact]
    public void NcsiSemanticEvaluator_NcsiFail_DoesNotClaimNetworkDown()
    {
        // NCSI 失败（正文不匹配、无重定向）
        var ncsiResult = NcsiSemanticEvaluator.Evaluate(
            contentMatches: false, nlmConnected: false,
            redirected: false, contentError: false);

        // WEB-01 不返回 Failed（不声称断网），返回 Skipped
        Assert.NotEqual(ProbeStatus.Failed, ncsiResult.Status);
        // 规则引擎根据 WEB-01 (Skipped) + WEB-02 (Pass) 得出不一致
    }

    // === 2. URL 脱敏测试：直接调用生产 UrlSanitizer.SanitizeUrl ===

    [Theory]
    [InlineData("https://user:password@pac.example.com/proxy.pac",
                "https://pac.example.com/proxy.pac")]
    [InlineData("http://pac.example.com/config.pac?token=secret123",
                "http://pac.example.com/config.pac")]
    [InlineData("https://pac.example.com/proxy.pac#fragment",
                "https://pac.example.com/proxy.pac")]
    [InlineData("https://admin:s3cr3t@pac.example.com:8443/proxy.pac?key=abc#frag",
                "https://pac.example.com:8443/proxy.pac")]
    public void UrlSanitizer_SanitizeUrl_RemovesUserinfoQueryFragment(string input, string expected)
    {
        var result = UrlSanitizer.SanitizeUrl(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UrlSanitizer_SanitizeUrl_NoCredentials_PreservedCleanly()
    {
        var result = UrlSanitizer.SanitizeUrl("https://pac.example.com/proxy.pac");
        Assert.Equal("https://pac.example.com/proxy.pac", result);
    }

    [Fact]
    public void UrlSanitizer_SanitizeUrl_NonDefaultPort_Preserved()
    {
        var result = UrlSanitizer.SanitizeUrl("https://pac.example.com:8443/proxy.pac?token=secret");
        Assert.Equal("https://pac.example.com:8443/proxy.pac", result);
        Assert.DoesNotContain("token", result);
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public void UrlSanitizer_SanitizeUrl_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, UrlSanitizer.SanitizeUrl(null));
        Assert.Equal(string.Empty, UrlSanitizer.SanitizeUrl(""));
        Assert.Equal(string.Empty, UrlSanitizer.SanitizeUrl("   "));
    }

    [Fact]
    public void UrlSanitizer_SanitizeUrl_InvalidUrl_ReturnsPlaceholder()
    {
        Assert.Equal("[invalid_url]", UrlSanitizer.SanitizeUrl("not a url at all"));
    }

    /// <summary>
    /// 修改生产脱敏函数后测试必须能失败。
    /// 此测试验证测试确实覆盖生产代码：如果 SanitizeUrl 不移除 query，此测试会失败。
    /// </summary>
    [Fact]
    public void UrlSanitizer_SanitizeUrl_TestCoversProductionCode()
    {
        var input = "https://user:pass@example.com/path?secret=token#frag";
        var result = UrlSanitizer.SanitizeUrl(input);

        // 如果生产代码正确工作，以下断言通过
        // 如果生产代码被修改为不移除 query，以下断言会失败
        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("token", result);
        Assert.DoesNotContain("user", result);
        Assert.DoesNotContain("pass", result);
        Assert.DoesNotContain("#", result);
    }

    // === 3. 代理地址脱敏测试：直接调用生产 UrlSanitizer.SanitizeProxyServer ===

    [Fact]
    public void UrlSanitizer_SanitizeProxyServer_RemovesCredentials()
    {
        var result = UrlSanitizer.SanitizeProxyServer("user:pass@127.0.0.1:7890");
        Assert.Equal("127.0.0.1:7890", result);
        Assert.DoesNotContain("user", result);
        Assert.DoesNotContain("pass", result);
    }

    [Fact]
    public void UrlSanitizer_SanitizeProxyServer_PreservesProtocolEntries()
    {
        var result = UrlSanitizer.SanitizeProxyServer("http=127.0.0.1:8080;https=127.0.0.1:8443");
        Assert.Contains("http=127.0.0.1:8080", result);
        Assert.Contains("https=127.0.0.1:8443", result);
    }

    [Fact]
    public void UrlSanitizer_SanitizeProxyServer_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, UrlSanitizer.SanitizeProxyServer(null));
        Assert.Equal(string.Empty, UrlSanitizer.SanitizeProxyServer(""));
    }

    // === 4. URL 规范化 + 路径保留测试：直接调用生产 UrlNormalizer.Normalize ===

    [Theory]
    [InlineData("https://example.com/path/to/page", "/path/to/page")]
    [InlineData("https://example.com/path?q=1", "/path?q=1")]
    [InlineData("https://example.com:8443/api/v1?token=abc", "/api/v1?token=abc")]
    [InlineData("http://example.com/", "/")]
    [InlineData("example.com", "/")]
    public void UrlNormalizer_Normalize_PreservesPathAndQuery(string input, string expectedPath)
    {
        var result = UrlNormalizer.Normalize(input);
        Assert.NotNull(result);
        Assert.Equal(expectedPath, result!.PathAndQuery);
    }

    [Theory]
    [InlineData("https://example.com:8443/path", 8443)]
    [InlineData("http://example.com:8080", 8080)]
    [InlineData("https://example.com", 443)]
    [InlineData("http://example.com", 80)]
    public void UrlNormalizer_Normalize_PreservesCustomPort(string input, int expectedPort)
    {
        var result = UrlNormalizer.Normalize(input);
        Assert.NotNull(result);
        Assert.Equal(expectedPort, result!.Port);
    }

    // === 5. 目标 URI 构建测试：直接调用生产 TargetUriBuilder.BuildRequestUrl ===

    [Fact]
    public void TargetUriBuilder_BuildRequestUrl_UsesPathAndQuery()
    {
        var target = new NormalizedTarget("https", "example.com", 443, true, "/api/v1?token=abc");
        var url = TargetUriBuilder.BuildRequestUrl(target);
        Assert.Equal("https://example.com:443/api/v1?token=abc", url);
    }

    [Fact]
    public void TargetUriBuilder_BuildRequestUrl_CustomPort()
    {
        var target = new NormalizedTarget("https", "example.com", 8443, true, "/path");
        var url = TargetUriBuilder.BuildRequestUrl(target);
        Assert.Equal("https://example.com:8443/path", url);
    }

    [Fact]
    public void TargetUriBuilder_BuildRequestUrl_HttpDefaultPort()
    {
        var target = new NormalizedTarget("http", "example.com", 80, false, "/");
        var url = TargetUriBuilder.BuildRequestUrl(target);
        Assert.Equal("http://example.com:80/", url);
    }

    /// <summary>
    /// 验证 TargetUriBuilder 确实使用 PathAndQuery，不固定 '/'。
    /// 如果生产代码被修改为忽略 PathAndQuery，此测试会失败。
    /// </summary>
    [Fact]
    public void TargetUriBuilder_BuildRequestUrl_TestCoversProductionCode()
    {
        var target = new NormalizedTarget("https", "example.com", 443, true, "/custom/path?q=test");
        var url = TargetUriBuilder.BuildRequestUrl(target);
        // 如果生产代码固定 '/'，此断言会失败
        Assert.Contains("/custom/path", url);
        Assert.Contains("q=test", url);
    }

    // === 6. 隐私加固测试：target_path + has_query 不泄露 query 值 ===

    [Fact]
    public void NormalizedTarget_SanitizedPath_RemovesQueryValue()
    {
        var target = UrlNormalizer.Normalize("https://example.com/path?token=secret123&code=abc")!;
        Assert.Equal("/path", target.SanitizedPath);
        Assert.True(target.HasQuery);
        // SanitizedPath 不应包含 token/secret/code/abc
        Assert.DoesNotContain("token", target.SanitizedPath);
        Assert.DoesNotContain("secret", target.SanitizedPath);
    }

    [Fact]
    public void NormalizedTarget_SanitizedPath_NoQuery_ReturnsFullPath()
    {
        var target = UrlNormalizer.Normalize("https://example.com/path/to/page")!;
        Assert.Equal("/path/to/page", target.SanitizedPath);
        Assert.False(target.HasQuery);
    }

    [Fact]
    public void NormalizedTarget_SanitizedPath_RootOnly()
    {
        var target = UrlNormalizer.Normalize("https://example.com")!;
        Assert.Equal("/", target.SanitizedPath);
        Assert.False(target.HasQuery);
    }

    [Fact]
    public void NormalizedTarget_PathAndQuery_PreservesFullForRequest()
    {
        // 实际网络请求仍可使用完整 PathAndQuery
        var target = UrlNormalizer.Normalize("https://example.com/api?key=value")!;
        Assert.Equal("/api?key=value", target.PathAndQuery);
        // 但证据记录用 SanitizedPath
        Assert.Equal("/api", target.SanitizedPath);
        Assert.True(target.HasQuery);
    }

    /// <summary>
    /// 修改生产脱敏函数后测试必须能失败。
    /// 如果 SanitizedPath 不移除 query，此测试会失败。
    /// </summary>
    [Fact]
    public void NormalizedTarget_SanitizedPath_TestCoversProductionCode()
    {
        var target = UrlNormalizer.Normalize("https://example.com/path?secret=token")!;
        // 如果生产代码不移除 query，以下断言会失败
        Assert.DoesNotContain("secret", target.SanitizedPath);
        Assert.DoesNotContain("token", target.SanitizedPath);
        Assert.DoesNotContain("?", target.SanitizedPath);
    }

    // === 7. 代理地址脱敏后安全解析测试 ===

    [Fact]
    public void UrlSanitizer_SanitizeProxyServer_ThenParse_NoCredentials()
    {
        // 原始代理地址含凭据
        var rawProxy = "user:pass@127.0.0.1:7890";
        // 先脱敏
        var sanitized = UrlSanitizer.SanitizeProxyServer(rawProxy);
        // 从脱敏结果解析 host/port
        Assert.Equal("127.0.0.1:7890", sanitized);
        Assert.DoesNotContain("user", sanitized);
        Assert.DoesNotContain("pass", sanitized);
    }

    // === 6. 健康目标配置测试 ===

    [Fact]
    public void HealthTargetCatalog_HasThreeTargetsWithCategories()
    {
        Assert.True(HealthTargetCatalog.Targets.Count >= 3);
        Assert.Contains(HealthTargetCatalog.Targets, t => t.Category == HealthTargetCategory.NcsiContentCheck);
        Assert.Contains(HealthTargetCatalog.Targets, t => t.Category == HealthTargetCategory.IndependentHttps);
        Assert.Contains(HealthTargetCatalog.Targets, t => t.Category == HealthTargetCategory.GlobalServicePath);
    }

    [Fact]
    public void NcsiTarget_DisablesRedirect_AndVerifiesContent()
    {
        var ncsi = HealthTargetCatalog.NcsiTarget;
        Assert.Equal(HealthTargetCategory.NcsiContentCheck, ncsi.Category);
        Assert.True(ncsi.DisableRedirect);
        Assert.Equal("Microsoft Connect Test", ncsi.ExpectedContentFragment);
        Assert.Equal("www.msftconnecttest.com", ncsi.Host);
    }
}
