namespace Doc2MD.Failures;

/// <summary>
/// 失败案例知识库记录实体（支持脱敏存储与历史查询）
/// </summary>
public class FailureRecord
{
    public string FailureId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string SourceDirectory { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string? SourceSha256 { get; set; }
    public FailureCategory Category { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = 1;
    public string? ResolvedByStrategy { get; set; }
    public bool IsResolved { get; set; }

    public static FailureRecord FromContext(FailureContext context)
    {
        return new FailureRecord
        {
            SourceDirectory = PathSanitizer.Anonymize(context.SourceFilePath != null ? System.IO.Path.GetDirectoryName(context.SourceFilePath) : string.Empty),
            SourceFileName = context.SourceFileName ?? string.Empty,
            SourceSha256 = context.FileSha256,
            Category = context.Category,
            RootCause = context.ErrorMessage ?? context.Category.GetDescription(),
            SuggestedAction = context.SuggestedAction ?? context.Category.GetSuggestedAction(),
            AttemptCount = context.AttemptCount,
            TimestampUtc = context.OccurredAt
        };
    }
}
