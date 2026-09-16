namespace Doc2MD.Failures;

/// <summary>
/// 转换失败上下文信息
/// </summary>
public class FailureContext
{
    public string? SourceFilePath { get; set; }
    public string? SourceFileName { get; set; }
    public string? FileSha256 { get; set; }
    public FailureCategory Category { get; set; }
    public string? ExceptionType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? SuggestedAction { get; set; }
    public bool IsRetryable { get; set; }
    public int AttemptCount { get; set; } = 1;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
