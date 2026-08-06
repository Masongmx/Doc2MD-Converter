using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// U1: AIGC 水印过滤器测试
/// </summary>
public class AigcWatermarkFilterTests
{
    // === 零宽字符水印 ===

    [Fact]
    public void Filter_ZeroWidthChars_AboveThreshold_RemovesThem()
    {
        var visible = new string('x', 200);
        var zw = new string('\u200B', 15);
        var input = visible + zw;

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.Contains("aigc_zero_width_chars", result.DetectedTypes);
        Assert.DoesNotContain('\u200B', result.Markdown);
    }

    [Fact]
    public void Filter_ZeroWidthChars_BelowCountThreshold_DoesNotRemove()
    {
        var visible = new string('x', 100);
        var zw = new string('\u200B', 5);
        var input = visible + zw;

        var result = AigcWatermarkFilter.Filter(input);

        Assert.False(result.HasWatermark);
        Assert.Contains('\u200B', result.Markdown);
    }

    [Fact]
    public void Filter_ZeroWidthChars_BelowDensityThreshold_DoesNotRemove()
    {
        var visible = new string('x', 10000);
        var zw = new string('\u200B', 15);
        var input = visible + zw;

        var result = AigcWatermarkFilter.Filter(input);

        Assert.False(result.HasWatermark);
    }

    [Fact]
    public void DetectZeroWidthWatermark_MultipleTypes_AggregatesCount()
    {
        var text = new string('x', 200) +
                   new string('\u200B', 5) +
                   new string('\u200C', 5) +
                   new string('\u200D', 5);

        var detection = AigcWatermarkFilter.DetectZeroWidthWatermark(text);

        Assert.True(detection.IsWatermark);
        Assert.Equal(15, detection.ZeroWidthCount);
        Assert.Equal(3, detection.CharBreakdown.Count);
    }

    // === YAML frontmatter 中的 AIGC 块 ===

    [Fact]
    public void Filter_FrontmatterWithAigcBlock_RemovesAigcKeepsRest()
    {
        var input = "---\ntitle: \"test\"\nAIGC:\n  ContentProducer: 'abc'\n  Label: '1'\nauthor: \"zs\"\n---\n\n# Title\n\nBody.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.Contains("title", result.Markdown);
        Assert.Contains("author", result.Markdown);
        Assert.Contains("Title", result.Markdown);
        Assert.DoesNotContain("ContentProducer", result.Markdown);
        Assert.DoesNotContain("AIGC:", result.Markdown);
    }

    [Fact]
    public void Filter_FrontmatterWithoutAigc_LeavesUntouched()
    {
        var input = "---\ntitle: \"normal\"\nauthor: \"ls\"\n---\n\n# Title";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.False(result.HasWatermark);
        Assert.Contains("title", result.Markdown);
        Assert.Contains("author", result.Markdown);
    }

    [Fact]
    public void Filter_MultipleFrontmatterWithAigc_RemovesAll()
    {
        var input = "---\nAIGC:\n  ContentProducer: 'abc'\n---\n\nP1\n\n---\nAIGC:\n  ContentPropagator: 'def'\n---\n\nP2";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.Contains("P1", result.Markdown);
        Assert.Contains("P2", result.Markdown);
        Assert.DoesNotContain("ContentProducer", result.Markdown);
        Assert.DoesNotContain("ContentPropagator", result.Markdown);
    }

    // === AIGC 标识行 ===

    [Fact]
    public void Filter_AigcLabelLine_RemovesIt()
    {
        var input = "# Title\n\nAIGC\u6807\u8bc6: test label\n\nBody.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.Contains("aigc_label_line", result.DetectedTypes);
        Assert.DoesNotContain("AIGC\u6807\u8bc6", result.Markdown);
        Assert.Contains("Body", result.Markdown);
    }

    [Fact]
    public void Filter_AigcLabelLineWithChineseColon_RemovesIt()
    {
        var input = "# Title\n\nAIGC\u6807\u8bc6\uff1achinese colon\n\nBody.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.DoesNotContain("AIGC\u6807\u8bc6", result.Markdown);
    }

    // === 独立 AIGC 行和元信息行 ===

    [Fact]
    public void Filter_AigcStandaloneLine_RemovesIt()
    {
        var input = "# Title\n\nAIGC: f93e2c47-c43b-4e1c-a40e-d2fb66c968da\n\nBody.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.DoesNotContain("AIGC:", result.Markdown);
        Assert.Contains("Body", result.Markdown);
    }

    [Fact]
    public void Filter_ScatteredAigcMetaLines_RemovesThem()
    {
        var input = "# Title\n\nContentProducer: 'abc'\nProduceID: 'f93e2c47'\n\nBody.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.DoesNotContain("ContentProducer", result.Markdown);
        Assert.DoesNotContain("ProduceID", result.Markdown);
        Assert.Contains("Body", result.Markdown);
    }

    // === 正文中的 AIGC frontmatter 块 ===

    [Fact]
    public void Filter_BodyFrontmatterAigcBlock_RemovesIt()
    {
        var input = "# Title\n\nP1.\n\n---\nAIGC:\n  ContentProducer: 'abc'\n  Label: '1'\n---\n\nP2.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.Contains("P1", result.Markdown);
        Assert.Contains("P2", result.Markdown);
        Assert.DoesNotContain("ContentProducer", result.Markdown);
    }

    // === UUID 行过滤 ===

    [Fact]
    public void Filter_UuidLine_WithPriorAigcDetection_RemovesIt()
    {
        var input = "# Title\n\nAIGC\u6807\u8bc6: test\n\nBody.\n\nf93e2c47-c43b-4e1c-a40e-d2fb66c968da\n\nMore.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.True(result.HasWatermark);
        Assert.DoesNotContain("f93e2c47", result.Markdown);
    }

    [Fact]
    public void Filter_UuidLine_WithoutPriorAigc_KeepsIt()
    {
        var input = "# Title\n\nref: f93e2c47-c43b-4e1c-a40e-d2fb66c968da\n\nBody.";

        var result = AigcWatermarkFilter.Filter(input);

        Assert.False(result.HasWatermark);
        Assert.Contains("f93e2c47", result.Markdown);
    }

    // === HasResidual 检测 ===

    [Fact]
    public void HasResidual_CleanMarkdown_ReturnsFalse()
    {
        var input = "# Title\n\nNormal body.";

        Assert.False(AigcWatermarkFilter.HasResidual(input));
    }

    [Fact]
    public void HasResidual_WithAigcContent_ReturnsTrue()
    {
        var input = "# Title\n\nContentProducer: 'abc'\n\nBody.";

        Assert.True(AigcWatermarkFilter.HasResidual(input));
    }

    [Fact]
    public void HasResidual_WithZeroWidthWatermark_ReturnsTrue()
    {
        var input = new string('x', 200) + new string('\u200B', 15);
        Assert.True(AigcWatermarkFilter.HasResidual(input));
    }

    // === 空输入 ===

    [Fact]
    public void Filter_NullInput_ReturnsEmpty()
    {
        var result = AigcWatermarkFilter.Filter(null!);
        Assert.False(result.HasWatermark);
        Assert.Equal(0, result.RemovedBlocks);
    }

    [Fact]
    public void Filter_EmptyString_ReturnsEmpty()
    {
        var result = AigcWatermarkFilter.Filter("");
        Assert.False(result.HasWatermark);
    }
}
