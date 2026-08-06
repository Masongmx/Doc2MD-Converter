using System.IO;
using Doc2MD.Models;
using Doc2MD.Pipeline.Services;
using Doc2MD.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// 模板功能、目录生成、页眉页脚、OCR 开关测试
/// </summary>
public class TemplateAndFeatureTests
{
    // === 模型层：新增字段 ===

    [Fact]
    public void FormatDocPreviewSettings_HasTemplatePath()
    {
        var settings = new FormatDocPreviewSettings();
        Assert.Equal(string.Empty, settings.TemplatePath);

        settings.TemplatePath = "C:\\templates\\gov.dotx";
        Assert.Equal("C:\\templates\\gov.dotx", settings.TemplatePath);
    }

    [Fact]
    public void MarkdownToDocxPreviewSettings_HasHeaderText()
    {
        var settings = new MarkdownToDocxPreviewSettings();
        Assert.Equal(string.Empty, settings.HeaderText);

        settings.HeaderText = "福州电信";
        Assert.Equal("福州电信", settings.HeaderText);
    }

    [Fact]
    public void MarkdownToDocxPreviewSettings_HasFooterText()
    {
        var settings = new MarkdownToDocxPreviewSettings();
        Assert.Equal(string.Empty, settings.FooterText);

        settings.FooterText = "第 &p 页";
        Assert.Equal("第 &p 页", settings.FooterText);
    }

    [Fact]
    public void FormatDocPreviewSettings_HasHeaderText()
    {
        var settings = new FormatDocPreviewSettings();
        Assert.Equal(string.Empty, settings.HeaderText);
    }

    [Fact]
    public void FormatDocPreviewSettings_HasFooterText()
    {
        var settings = new FormatDocPreviewSettings();
        Assert.Equal(string.Empty, settings.FooterText);
    }

    // === GenerateToc 属性 ===

    [Fact]
    public void MarkdownToDocxPreviewSettings_GenerateToc_DefaultFalse()
    {
        var settings = new MarkdownToDocxPreviewSettings();
        Assert.False(settings.GenerateToc);
    }

    [Fact]
    public void MarkdownToDocxPreviewSettings_GenerateToc_SetTrue()
    {
        var settings = new MarkdownToDocxPreviewSettings();
        settings.GenerateToc = true;
        Assert.True(settings.GenerateToc);
    }

    // === HeaderFooterEnabled 属性 ===

    [Fact]
    public void FormatDocPreviewSettings_HeaderFooterEnabled_DefaultTrue()
    {
        var settings = new FormatDocPreviewSettings();
        Assert.True(settings.HeaderFooterEnabled);
    }

    // === OCR 开关 ===

    [Fact]
    public void DocumentToMarkdownPreviewSettings_EnableOcr_DefaultFalse()
    {
        var settings = new DocumentToMarkdownPreviewSettings();
        Assert.False(settings.EnableOcr);
    }

    [Fact]
    public void DocumentToMarkdownPreviewSettings_EnableOcr_SetTrue()
    {
        var settings = new DocumentToMarkdownPreviewSettings();
        settings.EnableOcr = true;
        Assert.True(settings.EnableOcr);
    }

    // === TemplateSettings ===

    [Fact]
    public void TemplateSettings_DefaultDocxTemplate_DefaultEmpty()
    {
        var settings = new TemplateSettings();
        Assert.Equal(string.Empty, settings.DefaultDocxTemplate);
    }

    [Fact]
    public void TemplateSettings_OfficialDocTemplate_DefaultEmpty()
    {
        var settings = new TemplateSettings();
        Assert.Equal(string.Empty, settings.OfficialDocTemplate);
    }

    // === MarkdownToDocxPreviewSettings TemplatePath ===

    [Fact]
    public void MarkdownToDocxPreviewSettings_TemplatePath_DefaultEmpty()
    {
        var settings = new MarkdownToDocxPreviewSettings();
        Assert.Equal(string.Empty, settings.TemplatePath);
    }

    [Fact]
    public void MarkdownToDocxPreviewSettings_TemplatePath_SetValue()
    {
        var settings = new MarkdownToDocxPreviewSettings();
        settings.TemplatePath = "C:\\templates\\report.dotx";
        Assert.Equal("C:\\templates\\report.dotx", settings.TemplatePath);
    }

    // === Pipeline MarkdownToDocxConverter 无模板生成 ===

    [Fact]
    public void Pipeline_OfficialReport_CreatesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "official-report");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath).Length > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === Pipeline 多级标题 ===

    [Fact]
    public void Pipeline_MultiLevelHeadings_CreatesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 标题一\n\n## 标题二\n\n### 标题三\n\n正文。");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "official-report");

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === Pipeline 会议纪要模板 ===

    [Fact]
    public void Pipeline_MeetingMinutes_CreatesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 会议纪要\n\n时间：2026年8月\n\n一、议题\n\n讨论内容。");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "meeting-minutes");

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === Pipeline 巡察文档模板 ===

    [Fact]
    public void Pipeline_InspectionReport_CreatesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 巡察报告\n\n一、基本情况\n\n描述内容。");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "inspection-report");

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === Pipeline 格式检查报告生成 ===

    [Fact]
    public void Pipeline_GeneratesFormatCheckReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "official-report");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.FormatCheckReportPath), "格式检查报告文件应存在");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === Pipeline 输入文件不存在 ===

    [Fact]
    public void Pipeline_NonexistentFile_ReturnsError()
    {
        var converter = new MarkdownToDocxConverter();
        var result = converter.Convert("C:\\nonexistent\\file.md", "C:\\output.docx", "official-report");

        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    // === DocxFormatChecker 独立测试 ===

    [Fact]
    public void DocxFormatChecker_ChecksRenderedDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。");

            var converter = new MarkdownToDocxConverter();
            var outputPath = Path.Combine(tempDir, "output.docx");
            var result = converter.Convert(mdPath, outputPath, "official-report");

            Assert.True(result.Success);

            var checker = new DocxFormatChecker();
            var report = checker.Check(outputPath, "official-report");

            Assert.Equal("official-report", report.Template);
            Assert.NotNull(report.Issues);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === 旧配置文件兼容性：UsePipelineEngine 被移除后应静默跳过 ===

    [Fact]
    public void AppConfig_OldConfigWithUsePipelineEngine_SilentlyIgnored()
    {
        // 模拟旧配置文件中包含已删除的 UsePipelineEngine 属性
        var json = @"{
            ""General"": { ""DefaultOutputDir"": ""C:\\output"" },
            ""Preview"": {
                ""MarkdownToDocx"": {
                    ""UsePipelineEngine"": false,
                    ""PipelineTemplateId"": ""official-report""
                }
            }
        }";

        var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);
        Assert.NotNull(config);
        Assert.Equal("C:\\output", config.General.DefaultOutputDir);
        Assert.Equal("official-report", config.Preview.MarkdownToDocx.PipelineTemplateId);
        // UsePipelineEngine 已不存在于模型中，JSON 反序列化静默跳过
    }

    // === DocxFormatter 无模板排版 ===

    [Fact]
    public void DocxFormatter_NoTemplate_FormatsSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // 创建一个简单 docx
            var docxPath = Path.Combine(tempDir, "input.docx");
            CreateSimpleDocx(docxPath);

            var formatter = new DocxFormatter(new FormatDocPreviewSettings());
            var result = formatter.Format(docxPath, tempDir, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === DocxFormatter 页眉页脚 ===

    [Fact]
    public void DocxFormatter_HeaderFooter_FormatsWithHeaderFooter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var docxPath = Path.Combine(tempDir, "input.docx");
            CreateSimpleDocx(docxPath);

            var formatter = new DocxFormatter(new FormatDocPreviewSettings
            {
                HeaderFooterEnabled = true,
                HeaderText = "内部文件",
                FooterText = "禁止外传"
            });
            var result = formatter.Format(docxPath, tempDir, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === 辅助方法 ===

    private static void CreateSimpleDocx(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        var body = new Body();
        body.Append(new Paragraph(
            new Run(
                new Text("一、标题") { Space = SpaceProcessingModeValues.Preserve })));
        body.Append(new Paragraph(
            new Run(
                new Text("正文内容。") { Space = SpaceProcessingModeValues.Preserve })));
        mainPart.Document = new Document(body);
        mainPart.Document.Save();
    }
}
