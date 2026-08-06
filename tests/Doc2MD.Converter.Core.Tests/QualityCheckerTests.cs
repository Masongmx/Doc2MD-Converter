using System.Text.Json;
using Doc2MD.Models;
using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// U7: 质量检查器测试
/// </summary>
public class QualityCheckerTests
{
    // === 导入建议 ===

    [Fact]
    public void GenerateReport_SuccessNoWarnings_Recommended()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word"
        };

        QualityChecker.GenerateReport(result);

        Assert.Equal("recommended", result.ImportRecommendation);
    }

    [Fact]
    public void GenerateReport_Failed_NotRecommended()
    {
        var result = new ConversionResult
        {
            Success = false,
            SourceFilePath = "test.docx",
            SourceType = "Word",
            ErrorMessage = "fail"
        };

        QualityChecker.GenerateReport(result);

        Assert.Equal("not_recommended", result.ImportRecommendation);
    }

    [Fact]
    public void GenerateReport_WithHighWarning_Review()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.pdf",
            SourceType = "PDF"
        };
        result.Warnings.Add(ConversionWarning.Create("W_IMG_LOST", "img lost", "p1"));

        QualityChecker.GenerateReport(result);

        Assert.Equal("review", result.ImportRecommendation);
    }

    [Fact]
    public void GenerateReport_LowScoreButNoHigh_Review()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.pdf",
            SourceType = "PDF"
        };

        for (int i = 0; i < 30; i++)
        {
            result.Warnings.Add(ConversionWarning.Create("W_REVISION_LOST", $"w{i}", "full"));
        }

        QualityChecker.GenerateReport(result);

        Assert.True(result.QualityScore < 0.7);
        Assert.Equal("review", result.ImportRecommendation);
    }

    // === 公文加分 ===

    [Fact]
    public void GenerateReport_GovDocWithDocNumber_GetsBonusScore()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word",
            GovMetadata = new GovMetadata
            {
                Title = "t",
                DocumentNumber = "x2024x8",
                IssuingAuthority = "office"
            }
        };

        QualityChecker.GenerateReport(result);

        Assert.True(result.QualityScore >= 0.95);
    }

    [Fact]
    public void GenerateReport_GovDocWithoutDocNumber_NoBonusScore()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word",
            GovMetadata = new GovMetadata
            {
                Title = "t",
                IssuingAuthority = "office"
            }
        };

        QualityChecker.GenerateReport(result);

        Assert.Equal(1.0, result.QualityScore);
    }

    [Fact]
    public void GenerateReport_NonGovDoc_NoBonusScore()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word",
            GovMetadata = new GovMetadata
            {
                Title = "plain"
            }
        };

        QualityChecker.GenerateReport(result);

        Assert.Equal(1.0, result.QualityScore);
    }

    // === 质量报告内容 ===

    [Fact]
    public void GenerateReport_WithWarnings_ReportContainsWarningDetails()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.xlsx",
            SourceType = "Excel"
        };
        result.Warnings.Add(ConversionWarning.Create("W_MERGED_CELLS", "merged", "Sheet1"));
        result.Warnings.Add(ConversionWarning.Create("W_FORMULA_LOST", "formula", "Sheet1"));

        var json = QualityChecker.GenerateReport(result);
        var report = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal(2, report.GetProperty("totalWarnings").GetInt32());
        Assert.Equal(1, report.GetProperty("mediumSeverityCount").GetInt32());
        Assert.Equal(1, report.GetProperty("lowSeverityCount").GetInt32());
    }

    [Fact]
    public void GenerateReport_WithGovMetadata_ReportContainsGovFields()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word",
            GovMetadata = new GovMetadata
            {
                Title = "t",
                DocumentNumber = "x8",
                IssuingAuthority = "office",
                Confidence = 0.75
            }
        };

        var json = QualityChecker.GenerateReport(result);
        var report = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.True(report.GetProperty("govDocument").GetBoolean());
        Assert.Equal(0.75, report.GetProperty("govConfidence").GetDouble());
    }

    [Fact]
    public void GenerateReport_NoGovMetadata_ReportShowsFalse()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word"
        };

        var json = QualityChecker.GenerateReport(result);
        var report = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.False(report.GetProperty("govDocument").GetBoolean());
        Assert.Equal(0, report.GetProperty("govConfidence").GetDouble());
    }

    // === 警告严重程度分类 ===

    [Theory]
    [InlineData("W_IMG_LOST", "high")]
    [InlineData("W_TABLE_DEGRADE", "high")]
    [InlineData("W_OCR_FAILED", "high")]
    [InlineData("W_AIGC_WATERMARK", "high")]
    [InlineData("W_TABLE_TRUNCATED", "medium")]
    [InlineData("W_MERGED_CELLS", "medium")]
    [InlineData("W_CHART_LOST", "medium")]
    [InlineData("W_LEGACY_FALLBACK", "medium")]
    [InlineData("W_FORMULA_LOST", "low")]
    [InlineData("W_REVISION_LOST", "low")]
    [InlineData("W_HIDDEN_ROW", "low")]
    [InlineData("W_HYPERLINK_URL_LOST", "low")]
    [InlineData("W_ORDERED_LIST_FLAT", "low")]
    public void ClassifySeverity_KnownCodes_ReturnsCorrectSeverity(string code, string expected)
    {
        Assert.Equal(expected, ConversionWarning.ClassifySeverity(code));
    }

    [Fact]
    public void ClassifySeverity_UnknownCode_ReturnsLow()
    {
        Assert.Equal("low", ConversionWarning.ClassifySeverity("W_UNKNOWN_CODE"));
    }

    [Fact]
    public void Create_AutoClassifiesSeverityFromCode()
    {
        var warning = ConversionWarning.Create("W_IMG_LOST", "img lost", "p1");

        Assert.Equal("W_IMG_LOST", warning.Code);
        Assert.Equal("high", warning.Severity);
        Assert.Equal("img lost", warning.Message);
        Assert.Equal("p1", warning.Location);
    }

    // === 质量等级映射 ===

    [Fact]
    public void GenerateReport_HighScore_LevelHigh()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceType = "Word"
        };

        var json = QualityChecker.GenerateReport(result);
        var report = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal("high", report.GetProperty("overallLevel").GetString());
    }

    [Fact]
    public void GenerateReport_MediumScore_LevelMedium()
    {
        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.pdf",
            SourceType = "PDF"
        };
        result.Warnings.Add(ConversionWarning.Create("W_IMG_LOST", "img lost", "p1"));

        var json = QualityChecker.GenerateReport(result);
        var report = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal("medium", report.GetProperty("overallLevel").GetString());
        Assert.True(report.GetProperty("overallScore").GetDouble() >= 0.7);
    }
}
