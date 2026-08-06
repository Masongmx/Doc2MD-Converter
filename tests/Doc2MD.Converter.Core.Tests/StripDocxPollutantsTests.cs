using System.Reflection;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// U8: StripDocxPollutants 修复测试
/// 通过反射调用 MarkdownToDocxParser 的私有方法 StripDocxPollutants
/// </summary>
public class StripDocxPollutantsTests
{
    private static readonly MethodInfo? StripMethod =
        typeof(Parsers.MarkdownToDocxParser).GetMethod("StripDocxPollutants",
            BindingFlags.NonPublic | BindingFlags.Static);

    private static string Strip(string markdown)
    {
        if (StripMethod == null)
            throw new InvalidOperationException("StripDocxPollutants not found");
        return (string)StripMethod.Invoke(null, [markdown])!;
    }

    // === YAML frontmatter 正确跳过 ===

    [Fact]
    public void Strip_ValidFrontmatter_SkipsIt()
    {
        var input = "---\ntitle: \"doc\"\nsource_type: \"Word\"\nocr_used: false\n---\n\n# Body Title\n\nBody content.";

        var result = Strip(input);

        Assert.DoesNotContain("title:", result);
        Assert.DoesNotContain("source_type:", result);
        Assert.Contains("Body Title", result);
        Assert.Contains("Body content", result);
    }

    [Fact]
    public void Strip_FrontmatterWithClosingDots_SkipsIt()
    {
        var input = "---\ntitle: \"doc\"\n...\n\nBody.";

        var result = Strip(input);

        Assert.DoesNotContain("title:", result);
        Assert.Contains("Body", result);
    }

    // === U8 修复核心：横线分隔符不被误删 ===

    [Fact]
    public void Strip_HorizontalRuleAtStart_NotConsumedAsFrontmatter()
    {
        var input = "---\n\nBody content.";

        var result = Strip(input);

        Assert.Contains("Body content", result);
    }

    [Fact]
    public void Strip_HorizontalRuleFollowByText_NotConsumedAsFrontmatter()
    {
        var input = "---\nplain text\nno key value\n---\n\nMore content.";

        var result = Strip(input);

        Assert.Contains("plain text", result);
        Assert.Contains("More content", result);
    }

    [Fact]
    public void Strip_OnlyHorizontalRule_NotConsumedAsFrontmatter()
    {
        var input = "---\n\nBody paragraph.";

        var result = Strip(input);

        Assert.Contains("Body paragraph", result);
    }

    // === HTML 注释过滤 ===

    [Fact]
    public void Strip_SingleLineHtmlComment_RemovesIt()
    {
        var input = "# Title\n\n<!-- AI_AGENT_NOTICE: START -->\n<!-- comment -->\n\nBody.";

        var result = Strip(input);

        Assert.DoesNotContain("AI_AGENT_NOTICE", result);
        Assert.DoesNotContain("<!--", result);
        Assert.Contains("Body", result);
        Assert.Contains("Title", result);
    }

    [Fact]
    public void Strip_MultiLineHtmlComment_RemovesEntireBlock()
    {
        var input = "# Title\n\n<!--\n  multi line\n  block_id=b0001\n  source marker\n-->\n\nBody.";

        var result = Strip(input);

        Assert.DoesNotContain("multi line", result);
        Assert.DoesNotContain("block_id", result);
        Assert.Contains("Body", result);
    }

    // === AI_AGENT_NOTICE 行过滤 ===

    [Fact]
    public void Strip_StandaloneAigcNoticeLine_RemovesIt()
    {
        var input = "# Title\n\nAI_AGENT_NOTICE: notice line\n\nBody.";

        var result = Strip(input);

        Assert.DoesNotContain("AI_AGENT_NOTICE", result);
        Assert.Contains("Body", result);
    }

    // === 综合 ===

    [Fact]
    public void Strip_FrontmatterPlusCommentsPlusBody_KeepsOnlyBody()
    {
        var input = "---\ntitle: \"test\"\nconverter: \"v2.0\"\n---\n\n<!-- AI_AGENT_NOTICE: START -->\n<!-- WARNING: test -->\n<!-- AI_AGENT_NOTICE: END -->\n\n# Body Section\n\nBody para 1.\n\nBody para 2.";

        var result = Strip(input);

        Assert.DoesNotContain("title:", result);
        Assert.DoesNotContain("converter:", result);
        Assert.DoesNotContain("AI_AGENT_NOTICE", result);
        Assert.DoesNotContain("WARNING", result);
        Assert.Contains("Body Section", result);
        Assert.Contains("Body para 1", result);
        Assert.Contains("Body para 2", result);
    }

    // === 纯正文无污染物 ===

    [Fact]
    public void Strip_PureMarkdown_KeepsEverything()
    {
        var input = "# Title\n\n## Subtitle\n\nBody text.\n\n| Col1 | Col2 |\n|-----|-----|\n| A   | B   |\n\n- Item 1\n- Item 2\n";

        var result = Strip(input);

        Assert.Contains("Title", result);
        Assert.Contains("Subtitle", result);
        Assert.Contains("Body text", result);
        Assert.Contains("Col1", result);
        Assert.Contains("Item 1", result);
    }
}
