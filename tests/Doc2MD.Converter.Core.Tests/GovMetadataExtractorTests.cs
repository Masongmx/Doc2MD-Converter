using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// U6: 公文元数据提取器测试
/// </summary>
public class GovMetadataExtractorTests
{
    // === 标题识别 ===

    [Fact]
    public void Extract_H1Heading_ExtractsAsTitle()
    {
        var md = "# \u5173\u4e8e\u5f00\u5c552024\u5e74\u5ea6\u8003\u6838\u5de5\u4f5c\u7684\u901a\u77e5\n\n\u6b63\u6587\u5185\u5bb9\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("\u5173\u4e8e\u5f00\u5c552024\u5e74\u5ea6\u8003\u6838\u5de5\u4f5c\u7684\u901a\u77e5", meta.Title);
    }

    [Fact]
    public void Extract_NoH1ButHasFileName_UsesFileName()
    {
        var md = "\u6b63\u6587\u5185\u5bb9\uff0c\u65e0\u6807\u9898\u3002";

        var meta = GovMetadataExtractor.Extract(md, "\u5173\u4e8e\u57f9\u8bad\u65b9\u6848\u7684\u901a\u77e5.docx");

        Assert.Equal("\u5173\u4e8e\u57f9\u8bad\u65b9\u6848\u7684\u901a\u77e5", meta.Title);
    }

    [Fact]
    public void Extract_InvalidTitleKeyword_SkipsToFallback()
    {
        var md = "# \u76ee\u5f55\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, "\u5b9e\u9645\u6587\u4ef6\u540d.docx");

        Assert.Equal("\u5b9e\u9645\u6587\u4ef6\u540d", meta.Title);
    }

    [Fact]
    public void Extract_TitleWithNumberPrefix_CleansPrefix()
    {
        var md = "# 1.\u5173\u4e8e\u5de5\u4f5c\u7684\u901a\u77e5\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("\u5173\u4e8e\u5de5\u4f5c\u7684\u901a\u77e5", meta.Title);
    }

    // === 文号识别 ===

    [Fact]
    public void Extract_StandardDocumentNumber_ExtractsCorrectly()
    {
        var md = "# \u901a\u77e5\n\n\u5de1\u529e\u53d1\u30142022\u30158\u53f7\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.NotNull(meta.DocumentNumber);
        Assert.Contains("\u30142022\u30158\u53f7", meta.DocumentNumber);
        Assert.Contains("\u5de1\u529e\u53d1", meta.DocumentNumber);
    }

    [Fact]
    public void Extract_DocumentNumberWithBrackets_ExtractsCorrectly()
    {
        var md = "# \u901a\u77e5\n\n[2024]12\u53f7\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.NotNull(meta.DocumentNumber);
        Assert.Contains("[2024]12\u53f7", meta.DocumentNumber!);
    }

    [Fact]
    public void Extract_NoDocumentNumber_ReturnsNull()
    {
        var md = "# \u666e\u901a\u6587\u6863\n\n\u6b63\u6587\u5185\u5bb9\u6ca1\u6709\u4efb\u4f55\u6587\u53f7\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Null(meta.DocumentNumber);
    }

    // === 发文单位识别 ===

    [Fact]
    public void Extract_AuthorityInHeadArea_ExtractsLongestMatch()
    {
        var md = "# \u901a\u77e5\n\n\u4eba\u529b\u8d44\u6e90\u90e8\uff1a\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.NotNull(meta.IssuingAuthority);
        Assert.Equal("\u4eba\u529b\u8d44\u6e90\u90e8", meta.IssuingAuthority);
    }

    [Fact]
    public void Extract_MultipleAuthorityKeywords_PicksLongest()
    {
        var md = "# \u901a\u77e5\n\n\u516c\u53f8\u529e\u516c\u5ba4\u548c\u7eaa\u59d4\u8054\u5408\u53d1\u6587\u3002\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("\u516c\u53f8\u529e\u516c\u5ba4", meta.IssuingAuthority);
    }

    [Fact]
    public void Extract_NoAuthority_ReturnsNull()
    {
        var md = "# \u666e\u901a\u6587\u6863\n\n\u4e00\u4e9b\u666e\u901a\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Null(meta.IssuingAuthority);
    }

    // === 发布日期识别 ===

    [Fact]
    public void Extract_ChineseDate_ExtractsCorrectly()
    {
        var md = "# \u901a\u77e5\n\n2024\u5e743\u670815\u65e5\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("2024-03-15", meta.PublishDate);
    }

    [Fact]
    public void Extract_IsoDate_ExtractsCorrectly()
    {
        var md = "# \u901a\u77e5\n\n2024-03-15\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("2024-03-15", meta.PublishDate);
    }

    [Fact]
    public void Extract_DateInFrontmatter_SkipsCreated_at()
    {
        var md = "---\ntitle: \"doc\"\ncreated_at: \"2024-01-01\"\n---\n\n# Title\n\nDate 2024-06-20.";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("2024-06-20", meta.PublishDate);
    }

    [Fact]
    public void Extract_YearTooOld_SkipsDate()
    {
        var md = "# \u901a\u77e5\n\n1900\u5e741\u67081\u65e5\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Null(meta.PublishDate);
    }

    // === 文档类型识别 ===

    [Theory]
    [InlineData("\u5173\u4e8e\u8003\u6838\u5de5\u4f5c\u7684\u901a\u77e5", "notice")]
    [InlineData("\u4eba\u624d\u57f9\u8bad\u65b9\u6848", "plan")]
    [InlineData("\u7ba1\u7406\u529e\u6cd5", "measure")]
    [InlineData("\u7ba1\u7406\u5236\u5ea6", "policy")]
    [InlineData("\u5de5\u4f5c\u60c5\u51b5\u62a5\u544a", "report")]
    [InlineData("\u4f1a\u8bae\u7eaa\u8981", "minutes")]
    [InlineData("\u8bf7\u793a\u62a5\u544a", "request")]
    public void Extract_DocumentTypeFromTitle_ClassifiesCorrectly(string title, string expectedType)
    {
        var md = $"# {title}\n\nbody.";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal(expectedType, meta.DocumentType);
    }

    [Fact]
    public void Extract_UnknownType_ReturnsOther()
    {
        var md = "# random title\n\nbody.";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal("other", meta.DocumentType);
    }

    // === 主题关键词识别 ===

    [Fact]
    public void Extract_HrKeywords_AddsHrTopic()
    {
        var md = "# \u5173\u4e8e\u5458\u5de5\u62db\u8058\u7684\u901a\u77e5\n\n\u6d89\u53ca\u4eba\u5458\u7f16\u5236\u548c\u5165\u804c\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Contains("hr", meta.SubjectKeywords);
    }

    [Fact]
    public void Extract_MultipleTopics_AddsAll()
    {
        var md = "# \u5173\u4e8e\u85aa\u916c\u8003\u6838\u4e0e\u57f9\u8bad\u65b9\u6848\u7684\u901a\u77e5\n\n\u6d89\u53ca\u7ee9\u6548\u8003\u6838\u3001\u5de5\u8d44\u5956\u91d1\u548c\u57f9\u8bad\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Contains("salary", meta.SubjectKeywords);
        Assert.Contains("assessment", meta.SubjectKeywords);
        Assert.Contains("training", meta.SubjectKeywords);
    }

    [Fact]
    public void Extract_NoMatchingKeywords_ReturnsEmptyList()
    {
        var md = "# plain title\n\nplain body.";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Empty(meta.SubjectKeywords);
    }

    // === 置信度计算 ===

    [Fact]
    public void Extract_CompleteGovDoc_HasHighConfidence()
    {
        var md = "# \u5173\u4e8e\u5f00\u5c55\u8003\u6838\u5de5\u4f5c\u7684\u901a\u77e5\n\n\u5de1\u529e\u53d1\u30142024\u30158\u53f7\n\n\u4eba\u529b\u8d44\u6e90\u90e8\uff1a\n2024\u5e743\u670815\u65e5\n\n\u5173\u4e8e\u7ee9\u6548\u8003\u6838\u7684\u5de5\u4f5c\u5b89\u6392\u3002";

        var meta = GovMetadataExtractor.Extract(md, "test.docx");

        Assert.True(meta.Confidence >= 0.9);
        Assert.True(meta.IsGovDocument);
    }

    [Fact]
    public void Extract_OnlyTitle_HasLowConfidence()
    {
        var md = "# Title\n\nbody.";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.Equal(0.3, meta.Confidence);
        Assert.False(meta.IsGovDocument);
    }

    [Fact]
    public void Extract_TitlePlusDocNumber_IsGovDocument()
    {
        var md = "# \u901a\u77e5\n\n\u5de1\u529e\u53d1\u30142024\u30158\u53f7\n\n\u6b63\u6587\u3002";

        var meta = GovMetadataExtractor.Extract(md, null);

        Assert.True(meta.IsGovDocument);
        Assert.True(meta.Confidence >= 0.55);
    }

    // === 空输入 ===

    [Fact]
    public void Extract_EmptyMarkdown_ReturnsDefaultMetadata()
    {
        var meta = GovMetadataExtractor.Extract("", null);

        Assert.Null(meta.Title);
        Assert.Null(meta.DocumentNumber);
        Assert.Null(meta.IssuingAuthority);
        Assert.Null(meta.PublishDate);
        Assert.Equal(0.0, meta.Confidence);
        Assert.False(meta.IsGovDocument);
    }
}
