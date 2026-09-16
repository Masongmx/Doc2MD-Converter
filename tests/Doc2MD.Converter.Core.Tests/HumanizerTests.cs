using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doc2MD.Humanizer;
using Doc2MD.Models;
using Xunit;

namespace Doc2MD.Converter.Core.Tests;

public class HumanizerTests
{
    [Fact]
    public async Task TocGenerator_ShouldGenerateTocWhenHeadingsExceedThreshold()
    {
        var transform = new TocGeneratorTransform();
        var options = new HumanizerOptions
        {
            EnableTocGenerator = true,
            TocHeadingThreshold = 3
        };

        var md = "# Main Title\n\n## Section 1\nContent 1\n\n## Section 2\nContent 2\n\n### Subsection 2.1\nContent 2.1";
        var result = new ConversionResult { ProcessedMarkdown = md };

        var transformed = await transform.ApplyAsync(md, result, options, CancellationToken.None);

        Assert.Contains("## 目录", transformed);
        Assert.Contains("- [Section 1](#section-1)", transformed);
        Assert.Contains("- [Section 2](#section-2)", transformed);
        Assert.Contains("  - [Subsection 2.1](#subsection-2.1)", transformed);
    }

    [Fact]
    public async Task TocGenerator_ShouldSkipWhenTocAlreadyExistsOrBelowThreshold()
    {
        var transform = new TocGeneratorTransform();
        var options = new HumanizerOptions
        {
            EnableTocGenerator = true,
            TocHeadingThreshold = 5
        };

        var md = "# Main Title\n\n## Section 1\nContent 1";
        var result = new ConversionResult { ProcessedMarkdown = md };

        var transformed = await transform.ApplyAsync(md, result, options, CancellationToken.None);
        Assert.DoesNotContain("## 目录", transformed);

        var existingTocMd = "## 目录\n- [S1](#s1)\n\n## Section 1";
        var transformed2 = await transform.ApplyAsync(existingTocMd, result, new HumanizerOptions { TocHeadingThreshold = 1 }, CancellationToken.None);
        Assert.Equal(existingTocMd, transformed2);
    }

    [Fact]
    public async Task HeadingNormalizer_ShouldNormalizeSkippedHeadingLevels()
    {
        var transform = new HeadingNormalizerTransform();
        var options = new HumanizerOptions { EnableHeadingNormalizer = true };

        // Skips from level 1 to level 3, then level 5
        var md = "# Level 1\nText\n### Skipped to Level 3\nText\n##### Skipped to Level 5\nEnd";
        var result = new ConversionResult { ProcessedMarkdown = md };

        var transformed = await transform.ApplyAsync(md, result, options, CancellationToken.None);

        // Expect: # Level 1 -> ## Skipped to Level 3 -> ### Skipped to Level 5
        Assert.Contains("# Level 1", transformed);
        Assert.Contains("## Skipped to Level 3", transformed);
        Assert.Contains("### Skipped to Level 5", transformed);
    }

    [Fact]
    public async Task TableFormatter_ShouldFormatPipesAndCells()
    {
        var transform = new TableFormatterTransform();
        var options = new HumanizerOptions { EnableTableFormatter = true };

        var md = "|Name|Age|City|\n|---|---|---|\n| Alice | 25| Beijing |\n|Bob|30|Shanghai|";
        var result = new ConversionResult { ProcessedMarkdown = md };

        var transformed = await transform.ApplyAsync(md, result, options, CancellationToken.None);

        Assert.Contains("| Name | Age | City |", transformed);
        Assert.Contains("| Alice | 25 | Beijing |", transformed);
        Assert.Contains("| Bob | 30 | Shanghai |", transformed);
    }

    [Fact]
    public async Task ListNormalizer_ShouldNormalizeListIndents()
    {
        var transform = new ListNormalizerTransform();
        var options = new HumanizerOptions { EnableListNormalizer = true };

        var md = "- Item 1\n   - Item 1.1\n     - Item 1.1.1";
        var result = new ConversionResult { ProcessedMarkdown = md };

        var transformed = await transform.ApplyAsync(md, result, options, CancellationToken.None);

        Assert.Contains("- Item 1", transformed);
        Assert.Contains("  - Item 1.1", transformed);
        Assert.Contains("    - Item 1.1.1", transformed);
    }

    [Fact]
    public async Task DocHighlight_ShouldBoldGovDocNumber()
    {
        var transform = new DocHighlightTransform();
        var options = new HumanizerOptions { EnableDocHighlight = true };

        var md = "关于印发通知的决定\n国发〔2026〕12号\n各省、自治区、直辖市人民政府：";
        var result = new ConversionResult
        {
            ProcessedMarkdown = md,
            Metadata = new ConversionMetadata
            {
                GovMetadata = new Doc2MD.Services.GovMetadata
                {
                    DocumentNumber = "国发〔2026〕12号"
                }
            }
        };

        var transformed = await transform.ApplyAsync(md, result, options, CancellationToken.None);

        Assert.Contains("**国发〔2026〕12号**", transformed);
    }

    [Fact]
    public async Task HumanizerPipeline_ShouldExecuteAllTransformsWhenEnabled()
    {
        var pipeline = new HumanizerPipeline();
        var options = new HumanizerOptions
        {
            Enabled = true,
            EnableTocGenerator = true,
            TocHeadingThreshold = 2,
            EnableHeadingNormalizer = true,
            EnableTableFormatter = true,
            EnableListNormalizer = true,
            EnableDocHighlight = true
        };

        var rawMd = "# 主标题\n\n### 第一章 简介\n\n|Col1|Col2|\n|---|---|\n|A|B|\n\n- 一级列表\n   - 二级列表\n\n### 第二章 详细\n国发〔2026〕99号";
        var result = new ConversionResult
        {
            ProcessedMarkdown = rawMd,
            Metadata = new ConversionMetadata
            {
                GovMetadata = new Doc2MD.Services.GovMetadata
                {
                    DocumentNumber = "国发〔2026〕99号"
                }
            }
        };

        var processed = await pipeline.ProcessAsync(rawMd, result, options, CancellationToken.None);

        Assert.Contains("## 目录", processed);
        Assert.Contains("## 第一章 简介", processed); // normalized from ###
        Assert.Contains("| Col1 | Col2 |", processed);
        Assert.Contains("  - 二级列表", processed);
        Assert.Contains("**国发〔2026〕99号**", processed);
    }

    [Fact]
    public async Task HumanizerPipeline_ShouldBypassWhenDisabled()
    {
        var pipeline = new HumanizerPipeline();
        var options = new HumanizerOptions { Enabled = false };

        var rawMd = "# Title\n### Skipped";
        var result = new ConversionResult { ProcessedMarkdown = rawMd };

        var processed = await pipeline.ProcessAsync(rawMd, result, options, CancellationToken.None);
        Assert.Equal(rawMd, processed);
    }
}
