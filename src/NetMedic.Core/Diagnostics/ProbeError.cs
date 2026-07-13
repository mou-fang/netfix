namespace NetMedic.Core.Diagnostics;

/// <summary>
/// 探针错误信息。区分探针自身失败与检查对象异常。
/// 对应任务书 §5.2：Error 表示探针自己无法完成。
/// </summary>
public sealed record ProbeError(
    string Code,
    string MessageKey,
    int? NativeErrorCode = null);
