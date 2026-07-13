using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using NetMedic.App.Windows.Probes;

namespace NetMedic.Tests;

/// <summary>
/// TLS 错误分类测试。仅 net10.0-windows。
/// 验证 HostnameMismatch、ChainUntrusted、ConnectionFailed 不被混淆。
/// 验证回调分类优先于异常分类。
/// </summary>
public class WindowsTlsTests
{
    [Fact]
    public void TlsClassification_None_IsValid()
    {
        var c = TlsErrorClassification.Classify(SslPolicyErrors.None);
        Assert.True(c.IsValid);
        Assert.Equal("None", c.ErrorCategory);
    }

    [Fact]
    public void TlsClassification_ChainErrors_ChainUntrusted()
    {
        var c = TlsErrorClassification.Classify(SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.False(c.IsValid);
        Assert.Equal("ChainUntrusted", c.ErrorCategory);
    }

    [Fact]
    public void TlsClassification_NameMismatch_HostnameMismatch()
    {
        var c = TlsErrorClassification.Classify(SslPolicyErrors.RemoteCertificateNameMismatch);
        Assert.False(c.IsValid);
        Assert.Equal("HostnameMismatch", c.ErrorCategory);
    }

    [Fact]
    public void TlsClassification_NotAvailable_CertificateNotAvailable()
    {
        var c = TlsErrorClassification.Classify(SslPolicyErrors.RemoteCertificateNotAvailable);
        Assert.False(c.IsValid);
        Assert.Equal("CertificateNotAvailable", c.ErrorCategory);
    }

    [Fact]
    public void TlsClassification_Exception_HttpRequest_IsConnectionFailed()
    {
        var ex = new HttpRequestException("connection refused");
        var c = TlsErrorClassification.ClassifyException(ex);
        Assert.False(c.IsValid);
        Assert.Equal("ConnectionFailed", c.ErrorCategory);
    }

    [Fact]
    public void TlsClassification_AuthExpired_IsCertificateExpired()
    {
        var ex = new AuthenticationException("The certificate has expired");
        var c = TlsErrorClassification.ClassifyException(ex);
        Assert.False(c.IsValid);
        Assert.Equal("CertificateExpired", c.ErrorCategory);
    }

    [Fact]
    public void TlsClassification_Categories_NotConfused()
    {
        var hostname = TlsErrorClassification.Classify(SslPolicyErrors.RemoteCertificateNameMismatch);
        var chain = TlsErrorClassification.Classify(SslPolicyErrors.RemoteCertificateChainErrors);
        var conn = TlsErrorClassification.ClassifyException(new HttpRequestException("refused"));

        Assert.NotEqual(hostname.ErrorCategory, chain.ErrorCategory);
        Assert.NotEqual(hostname.ErrorCategory, conn.ErrorCategory);
        Assert.NotEqual(chain.ErrorCategory, conn.ErrorCategory);
    }

    [Fact]
    public void TlsErrorRecord_CallbackPriority_OverException()
    {
        var record = new TlsErrorRecord();
        record.SetError("HostnameMismatch", "test detail",
            SslPolicyErrors.RemoteCertificateNameMismatch);

        // 异常回退不应覆盖回调分类
        record.FallbackFromException(new HttpRequestException("refused"));

        Assert.Equal("HostnameMismatch", record.ErrorCategory);
        Assert.False(record.IsNotChecked);
    }

    [Fact]
    public void TlsErrorRecord_NotChecked_FallbackToException()
    {
        var record = new TlsErrorRecord();
        Assert.True(record.IsNotChecked);

        record.FallbackFromException(new HttpRequestException("refused"));

        Assert.Equal("ConnectionFailed", record.ErrorCategory);
    }

    [Fact]
    public void TlsErrorRecord_CallbackValid_NotOverwrittenByException()
    {
        var record = new TlsErrorRecord();
        record.SetValid();

        record.FallbackFromException(new AuthenticationException("expired"));

        Assert.True(record.IsValid);
        Assert.Equal("None", record.ErrorCategory);
    }
}
