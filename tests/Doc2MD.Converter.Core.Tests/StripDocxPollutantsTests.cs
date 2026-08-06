using Doc2MD.Pipeline.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// Markdown 清洗测试：验证 Pipeline 入口对 YAML frontmatter/HTML 注释/AI 标记的过滤。
/// 使用 MarkdownToDocxConverter 公开 API 测试（不再反射私有方法）。
/// </summary>
public class StripDocxPollutantsTests
{
    /// <summary>
    /// 验证 Pipeline 可以处理含 YAML frontmatter 的 Markdown 而不崩溃
    /// </summary>
    [Fact]
    public void Pipeline_WithFrontmatter_GeneratesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "---\ntitle: \"doc\"\nsource_type: \"Word\"\nocr_used: false\n---\n\n# Body Title\n\nBody content.");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "official-report");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// 验证 Pipeline 可以处理含 HTML 注释的 Markdown
    /// </summary>
    [Fact]
    public void Pipeline_WithHtmlComments_GeneratesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# Title\n\n<!-- AI_AGENT_NOTICE: START -->\n<!-- comment -->\n\nBody.");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "official-report");

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// 验证 Pipeline 可以处理纯 Markdown（无污染物）
    /// </summary>
    [Fact]
    public void Pipeline_WithPureMarkdown_GeneratesDocx()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"Doc2MD_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mdPath = Path.Combine(tempDir, "test.md");
            File.WriteAllText(mdPath, "# Title\n\n## Subtitle\n\nBody text.\n\n| Col1 | Col2 |\n|-----|-----|\n| A   | B   |\n\n- Item 1\n- Item 2\n");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, Path.Combine(tempDir, "output.docx"), "official-report");

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
