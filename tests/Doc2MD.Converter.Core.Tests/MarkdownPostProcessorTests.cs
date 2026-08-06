using Doc2MD.Models;
using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// 后处理管道集成测试
/// </summary>
public class MarkdownPostProcessorTests
{
    [Fact]
    public void Process_CleanMarkdown_InjectsFrontmatterWithGovMetadata()
    {
        var rawMd = "# \u5173\u4e8e\u5f00\u5c55\u8003\u6838\u5de5\u4f5c\u7684\u901a\u77e5\n\n\u4eba\u529b\u8d44\u6e90\u90e8\uff1a\n2024\u5e743\u670815\u65e5\n\n\u5404\u90e8\u95e8\uff1a\n\u5f00\u5c55\u5e74\u5ea6\u7ee9\u6548\u8003\u6838\u3002";

        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "notice.docx",
            SourceFileName = "notice.docx",
            SourceType = "Word",
            SourceFileSize = 1024,
            RawMarkdown = rawMd
        };

        var postResult = MarkdownPostProcessor.Process(rawMd, result);

        Assert.Contains("gov_title:", postResult.Markdown);
        Assert.Contains("gov_issuing_authority:", postResult.Markdown);
        Assert.Contains("gov_publish_date:", postResult.Markdown);
        Assert.Contains("gov_document_type:", postResult.Markdown);

        Assert.NotNull(result.GovMetadata);
        Assert.Equal("\u5173\u4e8e\u5f00\u5c55\u8003\u6838\u5de5\u4f5c\u7684\u901a\u77e5", result.GovMetadata!.Title);
        Assert.Equal("\u4eba\u529b\u8d44\u6e90\u90e8", result.GovMetadata.IssuingAuthority);
        Assert.Equal("2024-03-15", result.GovMetadata.PublishDate);
    }

    [Fact]
    public void Process_MarkdownWithAigcWatermark_FiltersAndAddsWarning()
    {
        var rawMd = "---\nAIGC:\n  ContentProducer: '001191110102MAD55U9H0F10002'\n  Label: '1'\n---\n\n# Title\n\nBody.";

        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceFileName = "test.docx",
            SourceType = "Word",
            RawMarkdown = rawMd
        };

        var postResult = MarkdownPostProcessor.Process(rawMd, result);

        Assert.Contains(result.Warnings, w => w.Code == "W_AIGC_WATERMARK");
        Assert.DoesNotContain("ContentProducer", postResult.Markdown);
        Assert.Contains("Title", postResult.Markdown);
    }

    [Fact]
    public void Process_EmptyMarkdown_ReturnsEmpty()
    {
        var result = new ConversionResult
        {
            Success = true,
            RawMarkdown = ""
        };

        var postResult = MarkdownPostProcessor.Process("", result);

        Assert.Equal("", postResult.Markdown);
        Assert.Equal(0, postResult.BlockCount);
    }

    [Fact]
    public void Process_WithAigcNotice_FiltersItFromProcessedMarkdown()
    {
        var rawMd = "# Title\n\nAIGC\u6807\u8bc6: watermark\n\nBody.";

        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.docx",
            SourceFileName = "test.docx",
            SourceType = "Word",
            RawMarkdown = rawMd
        };

        var postResult = MarkdownPostProcessor.Process(rawMd, result);

        Assert.DoesNotContain("AIGC\u6807\u8bc6", postResult.Markdown);
        Assert.Contains("Body", postResult.Markdown);
        Assert.Contains(result.Warnings, w => w.Code == "W_AIGC_WATERMARK");
    }

    [Fact]
    public void Process_GovDocument_HasGovDocumentFlagInFrontmatter()
    {
        var rawMd = "# \u5173\u4e8e\u5f00\u5c55\u5de5\u4f5c\u7684\u901a\u77e5\n\n\u5de1\u529e\u53d1\u30142024\u30158\u53f7\n\n\u4eba\u529b\u8d44\u6e90\u90e8\uff1a\n\n\u6b63\u6587\u3002";

        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "notice.docx",
            SourceFileName = "notice.docx",
            SourceType = "Word",
            RawMarkdown = rawMd
        };

        var postResult = MarkdownPostProcessor.Process(rawMd, result);

        Assert.Contains("gov_document: true", postResult.Markdown);
        Assert.Contains("gov_doc_number:", postResult.Markdown);
        Assert.Contains("gov_confidence:", postResult.Markdown);
    }

    [Fact]
    public void Process_BlockIds_Injected()
    {
        var rawMd = "# Heading 1\n\nParagraph text.\n\n## Heading 2\n\n- list item";

        var result = new ConversionResult
        {
            Success = true,
            SourceFilePath = "test.md",
            SourceFileName = "test.md",
            SourceType = "Text",
            RawMarkdown = rawMd
        };

        var postResult = MarkdownPostProcessor.Process(rawMd, result);

        Assert.Contains("block_id=b", postResult.Markdown);
        Assert.True(postResult.BlockCount > 0);
    }
}
