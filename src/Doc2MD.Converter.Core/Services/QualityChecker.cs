using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 质量检查器：基于 ConversionResult 中的 Warnings 生成 quality_report.json。
/// v1.3: 使用 0.0-1.0 评分制，支持全部警告代码。
/// </summary>
public static class QualityChecker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string GenerateReport(ConversionResult result)
    {
        var summary = ComputeSummary(result);

        // 同步质量评分到 ConversionResult
        result.QualityScore = summary.Score;

        // v2.0: 计算导入建议
        result.ImportRecommendation = ComputeImportRecommendation(summary.Score, result);

        var highCount = result.Warnings.Count(w => w.Severity == "high");
        var mediumCount = result.Warnings.Count(w => w.Severity == "medium");
        var lowCount = result.Warnings.Count(w => w.Severity == "low");

        var report = new QualityReport
        {
            SourceFile = Path.GetFileName(result.SourceFilePath),
            SourceType = result.SourceType,
            OverallScore = summary.Score,
            OverallLevel = summary.Level,
            ImportRecommendation = result.ImportRecommendation,
            TotalWarnings = result.Warnings.Count,
            HighSeverityCount = highCount,
            MediumSeverityCount = mediumCount,
            LowSeverityCount = lowCount,
            Warnings = result.Warnings.Select(w => new QualityWarningEntry
            {
                Code = w.Code,
                Severity = w.Severity,
                Message = w.Message,
                Location = w.Location
            }).ToList(),
            GovDocument = result.GovMetadata?.IsGovDocument ?? false,
            GovConfidence = result.GovMetadata?.Confidence ?? 0,
            CheckedAt = DateTimeOffset.Now
        };

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static (double Score, string Level) ComputeSummary(ConversionResult result)
    {
        double score = 1.0;
        foreach (var w in result.Warnings)
        {
            score -= w.Code switch
            {
                "W_IMG_LOST" => 0.15,
                "W_TABLE_DEGRADE" => 0.12,
                "W_OCR_FAILED" => 0.15,
                "W_AIGC_WATERMARK" => 0.10,
                "W_LEGACY_FALLBACK" => 0.08,
                "W_TABLE_TRUNCATED" => 0.05,
                "W_MERGED_CELLS" => 0.03,
                "W_FOOTNOTE_LOST" => 0.05,
                "W_COMMENT_LOST" => 0.05,
                "W_CHART_LOST" => 0.05,
                "W_EMBEDDED_OBJECT_LOST" => 0.05,
                "W_TWO_COLUMN_PDF" => 0.05,
                "W_OCR_LOW_CONFIDENCE" => 0.05,
                "W_REVISION_LOST" => 0.02,
                "W_FORMULA_LOST" => 0.02,
                "W_HIDDEN_ROW" => 0.02,
                "W_HYPERLINK_URL_LOST" => 0.01,
                "W_ORDERED_LIST_FLAT" => 0.02,
                _ => 0.01
            };
        }

        if (result.OcrUsed) score -= 0.05;

        // v2.0: 公文加分——识别为公文且有文号时加 0.05
        if (result.GovMetadata?.IsGovDocument == true && !string.IsNullOrEmpty(result.GovMetadata.DocumentNumber))
            score += 0.05;

        score = Math.Max(0.0, Math.Min(1.0, score));

        var level = score switch
        {
            >= 0.9 => "high",
            >= 0.7 => "medium",
            >= 0.5 => "low",
            _ => "poor"
        };

        return (Math.Round(score, 2), level);
    }

    /// <summary>
    /// 计算导入建议等级（v2.0 新增）
    /// recommended: 质量高，无 high 严重警告
    /// review: 有 high 警告或评分中等，建议人工复核
    /// not_recommended: 评分极低或提取失败
    /// </summary>
    private static string ComputeImportRecommendation(double score, ConversionResult result)
    {
        // 转换失败直接不建议
        if (!result.Success) return "not_recommended";

        // 有 high 级警告 → 建议复核
        if (result.Warnings.Any(w => w.Severity == "high"))
            return "review";

        // 评分 < 0.7 → 建议复核
        if (score < 0.7) return "review";

        // 评分 < 0.5 → 不建议
        if (score < 0.5) return "not_recommended";

        return "recommended";
    }
}

public class QualityReport
{
    public string? SourceFile { get; set; }
    public string? SourceType { get; set; }
    public double OverallScore { get; set; }
    public string OverallLevel { get; set; } = "high";
    public string ImportRecommendation { get; set; } = "recommended";
    public bool GovDocument { get; set; }
    public double GovConfidence { get; set; }
    public int TotalWarnings { get; set; }
    public int HighSeverityCount { get; set; }
    public int MediumSeverityCount { get; set; }
    public int LowSeverityCount { get; set; }
    public List<QualityWarningEntry> Warnings { get; set; } = [];
    public DateTimeOffset CheckedAt { get; set; }
}

public class QualityWarningEntry
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public string Message { get; set; } = string.Empty;
    public string? Location { get; set; }
}
