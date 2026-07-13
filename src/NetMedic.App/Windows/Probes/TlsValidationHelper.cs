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
/// 阶段 2.3 修正：回调取得的准确分类优先于异常分类。
/// 只有 record 为 NotChecked 时才回退到 ClassifyException。
/// </summary>
public sealed class TlsErrorRecord
{
    private const string NotChecked = "NotChecked";

    private volatile bool _isValid;
    private volatile string _errorCategory = NotChecked;
    private volatile string _detail = string.Empty;
    private SslPolicyErrors _errors;
    private volatile bool _callbackInvoked;

    public void SetValid()
    {
        _isValid = true;
        _errorCategory = "None";
        _callbackInvoked = true;
    }

    public void SetError(string category, string detail, SslPolicyErrors errors)
    {
        _isValid = false;
        _errorCategory = category;
        _detail = detail;
        _errors = errors;
        _callbackInvoked = true;
    }

    /// <summary>回调是否被调用过（即 TLS 握手是否到达证书验证阶段）。</summary>
    public bool CallbackInvoked => _callbackInvoked;

    /// <summary>错误分类是否仍为 NotChecked（回调未触发）。</summary>
    public bool IsNotChecked => _errorCategory == NotChecked;

    public bool IsValid => _isValid;
    public string ErrorCategory => _errorCategory;
    public string Detail => _detail;

    /// <summary>
    /// 从异常回退分类。只有当回调未触发（NotChecked）时才使用。
    /// 如果回调已触发，保留回调取得的准确分类。
    /// </summary>
    public void FallbackFromException(Exception ex)
    {
        if (_callbackInvoked)
        {
            // 回调已触发，保留准确分类，不覆盖
            return;
        }

        var classification = TlsErrorClassification.ClassifyException(ex);
        _isValid = false;
        _errorCategory = classification.ErrorCategory;
        _detail = classification.Detail;
    }

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
