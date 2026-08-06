using System.IO;
using Doc2MD.Models;
using Doc2MD.Parsers;
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

    // === MarkdownToDocxParser 无模板生成 ===

    [Fact]
    public void MarkdownToDocxParser_NoTemplate_CreatesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。");

            var parser = new MarkdownToDocxParser();
            parser.PreviewSettings = new MarkdownToDocxPreviewSettings();
            var result = parser.Parse(mdPath, tempDir, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath).Length > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === MarkdownToDocxParser 模板不存在时回退 ===

    [Fact]
    public void MarkdownToDocxParser_TemplateNotFound_FallsBackToNewDoc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。");

            var parser = new MarkdownToDocxParser();
            parser.PreviewSettings = new MarkdownToDocxPreviewSettings
            {
                TemplatePath = "C:\\nonexistent\\template.dotx"
            };
            var result = parser.Parse(mdPath, tempDir, CancellationToken.None);

            // 模板不存在时应回退到新建文档
            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === MarkdownToDocxParser 生成目录 ===

    [Fact]
    public void MarkdownToDocxParser_GenerateToc_CreatesDocxWithToc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 标题一\n\n## 标题二\n\n### 标题三\n\n正文。");

            var parser = new MarkdownToDocxParser();
            parser.PreviewSettings = new MarkdownToDocxPreviewSettings { GenerateToc = true };
            var result = parser.Parse(mdPath, tempDir, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === MarkdownToDocxParser 页眉页脚 ===

    [Fact]
    public void MarkdownToDocxParser_HeaderFooter_CreatesDocxWithHeaderFooter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文。");

            var parser = new MarkdownToDocxParser();
            parser.PreviewSettings = new MarkdownToDocxPreviewSettings
            {
                HeaderText = "机密文件",
                FooterText = "仅限内部使用"
            };
            var result = parser.Parse(mdPath, tempDir, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // === MarkdownToDocxParser 模板克隆 ===

    [Fact]
    public void MarkdownToDocxParser_WithTemplate_ClonesAndWritesContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // 先创建一个简单模板
            var templatePath = Path.Combine(tempDir, "template.docx");
            CreateSimpleTemplate(templatePath);

            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文。");

            var parser = new MarkdownToDocxParser();
            parser.PreviewSettings = new MarkdownToDocxPreviewSettings
            {
                TemplatePath = templatePath
            };
            var result = parser.Parse(mdPath, tempDir, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(new FileInfo(result.OutputPath).Length > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
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

    private static void CreateSimpleTemplate(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        var body = new Body();
        body.Append(new Paragraph(
            new Run(
                new Text("模板内容（应被清除）"))));
        body.Append(new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin { Top = 2098, Bottom = 1984, Left = 1587, Right = 1474 }));
        mainPart.Document = new Document(body);
        mainPart.Document.Save();
    }

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
