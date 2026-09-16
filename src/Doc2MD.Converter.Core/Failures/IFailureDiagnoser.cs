namespace Doc2MD.Failures;

/// <summary>
/// 失败诊断器接口
/// </summary>
public interface IFailureDiagnoser
{
    FailureContext Diagnose(Exception? ex, string? filePath, string? errorMessage = null);
}
