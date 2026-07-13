using System.Collections.Immutable;
using System.Net.Security;
using System.Net.Http;

namespace NetMedic.App.Windows.Probes;

/// <summary>
/// TLS 验证辅助。记录证书错误但只在 SslPolicyErrors.None 时接受。
/// 对应任务书 §5.3 WEB-TLS + 阶段 2.2 修正：
/// 删除所有无条件 return true 的证书验证回调。
/// </summary>
public static class TlsValidationHelper
{
    /// <summary>
    /// 创建 HttpClientHandler，使用严格的证书验证。
    /// 只在 SslPolicyErrors.None 时接受证书。
    /// 证书错误信息通过返回的 TlsErrorRecord 记录。
    /// </summary>
    public static (HttpClientHandler handler, TlsErrorRecord record) CreateStrictHandler()
    {
        var record = new TlsErrorRecord();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                // 只在无错误时接受
                if (errors == SslPolicyErrors.None)
                {
                    record.SetValid();
                    return true;
                }

                // 有错误：记录但不接受
                var classification = TlsErrorClassification.Classify(errors,
                    cert?.Subject ?? null);
                record.SetError(classification.ErrorCategory, classification.Detail, errors);
                return false; // 拒绝连接
            },
        };

        return (handler, record);
    }

    /// <summary>
    /// 创建直连 HttpClientHandler（UseProxy=false）+ 严格 TLS 验证。
    /// </summary>
    public static (HttpClientHandler handler, TlsErrorRecord record) CreateDirectStrictHandler()
    {
        var (handler, record) = CreateStrictHandler();
        handler.UseProxy = false;
        return (handler, record);
    }
}

/// <summary>
/// TLS 错误记录。记录证书验证结果。
/// </summary>
public sealed class TlsErrorRecord
{
    private volatile bool _isValid;
    private volatile string _errorCategory = "NotChecked";
    private volatile string _detail = string.Empty;
    private SslPolicyErrors _errors;

    public void SetValid()
    {
        _isValid = true;
        _errorCategory = "None";
    }

    public void SetError(string category, string detail, SslPolicyErrors errors)
    {
        _isValid = false;
        _errorCategory = category;
        _detail = detail;
        _errors = errors;
    }

    public bool IsValid => _isValid;
    public string ErrorCategory => _errorCategory;
    public string Detail => _detail;

    public void WriteTo(Dictionary<string, object?> evidence, string prefix = "tls_")
    {
        evidence[$"{prefix}valid"] = _isValid;
        evidence[$"{prefix}error_category"] = _errorCategory;
        if (!_isValid)
        {
            evidence[$"{prefix}detail"] = _detail;
            evidence[$"{prefix}ssl_policy_errors"] = _errors.ToString();
        }
    }
}
