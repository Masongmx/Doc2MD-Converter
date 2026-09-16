namespace Doc2MD.Failures;

/// <summary>
/// 重试执行上下文
/// </summary>
public class RetryContext
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public FailureDiagnosis Diagnosis { get; set; } = new();
    public int AttemptCount { get; set; } = 1;
    public bool EnableOcrFallback { get; set; } = true;
}

/// <summary>
/// 诊断结果
/// </summary>
public class FailureDiagnosis
{
    public FailureCategory Category { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
}

/// <summary>
/// 重试执行结果
/// </summary>
public class RetryExecutionResult
{
    public bool ShouldRetryImmediately { get; set; }
    public bool RequiresUserAction { get; set; }
    public bool Handled { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 针对性重试策略接口
/// </summary>
public interface IRetryStrategy
{
    FailureCategory AppliesTo { get; }
    Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken);
}
