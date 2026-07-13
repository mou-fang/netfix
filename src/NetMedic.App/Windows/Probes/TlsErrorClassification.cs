using System.Net.Security;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// TLS 证书错误分类。对应任务书 §5.3 WEB-TLS：
/// 禁止忽略证书错误后继续；有证书错误时不得把 HTTPS 标记为 Pass。
/// </summary>
public sealed record TlsErrorClassification(
    bool IsValid,
    string ErrorCategory,
    string Detail)
{
    /// <summary>
    /// 从 SslPolicyErrors 分类证书错误。
    /// 只在 SslPolicyErrors.None 时返回 IsValid=true。
    /// </summary>
    public static TlsErrorClassification Classify(SslPolicyErrors errors, string? subjectName = null)
    {
        if (errors == SslPolicyErrors.None)
        {
            return new TlsErrorClassification(true, "None", "证书验证通过");
        }

        var categories = new List<string>();

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            categories.Add("ChainUntrusted");
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            categories.Add("HostnameMismatch");
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            categories.Add("CertificateNotAvailable");
        }

        string category = categories.Count > 0 ? string.Join(",", categories) : "Unknown";
        string detail = $"SslPolicyErrors={errors}" +
            (subjectName is not null ? $", Subject={subjectName}" : "");

        return new TlsErrorClassification(false, category, detail);
    }

    /// <summary>
    /// 从异常分类 TLS/连接错误。区分证书过期/尚未生效、主机名不匹配、证书链不可信和普通连接失败。
    /// </summary>
    public static TlsErrorClassification ClassifyException(Exception ex)
    {
        // 认证异常（证书问题）
        if (ex is System.Security.Authentication.AuthenticationException authEx)
        {
            string msg = authEx.Message.ToLowerInvariant();
            if (msg.Contains("expired") || msg.Contains("not yet valid") || msg.Contains("notvalid"))
            {
                return new TlsErrorClassification(false, "CertificateExpired", authEx.Message);
            }

            return new TlsErrorClassification(false, "AuthenticationFailed", authEx.Message);
        }

        // 远程证书异常
        if (ex.InnerException is System.Security.Authentication.AuthenticationException innerAuth)
        {
            return ClassifyException(innerAuth);
        }

        // 连接失败（非证书问题）
        if (ex is System.Net.Http.HttpRequestException)
        {
            return new TlsErrorClassification(false, "ConnectionFailed", ex.GetType().Name);
        }

        if (ex is System.Net.Sockets.SocketException)
        {
            return new TlsErrorClassification(false, "ConnectionFailed", ex.GetType().Name);
        }

        return new TlsErrorClassification(false, "Unknown", ex.GetType().Name);
    }
}
