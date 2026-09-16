using System.Text;
using System.Text.RegularExpressions;
using Doc2MD.Models;

namespace Doc2MD.Humanizer;

/// <summary>
/// 1. 自动生成目录 (TOC) 转换器
/// </summary>
public class TocGeneratorTransform : IHumanizerTransform
{
    public string Name => "TocGenerator";

    private static readonly Regex HeadingRegex = new(
        @"^(#{1,6})\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex FrontmatterRegex = new(
        @"^---\r?\n.*?\r?\n---\r?\n?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public Task<string> ApplyAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken)
    {
        if (!options.EnableTocGenerator) return Task.FromResult(markdown);

        // 已经包含 TOC 则跳过
        if (markdown.Contains("## 目录") || markdown.Contains("## Table of Contents") || markdown.Contains("[TOC]"))
        {
            return Task.FromResult(markdown);
        }

        var matches = HeadingRegex.Matches(markdown);
        if (matches.Count < options.TocHeadingThreshold)
        {
            return Task.FromResult(markdown);
        }

        var tocBuilder = new StringBuilder();
        tocBuilder.AppendLine("## 目录");
        tocBuilder.AppendLine();

        foreach (Match m in matches)
        {
            var level = m.Groups[1].Value.Length;
            var title = m.Groups[2].Value.Trim();

            // 跳过一级主标题
            if (level == 1) continue;

            var indent = new string(' ', Math.Max(0, (level - 2) * 2));
            var anchor = title.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("（", "")
                .Replace("）", "")
                .Replace("(", "")
                .Replace(")", "");

            tocBuilder.AppendLine($"{indent}- [{title}](#{anchor})");
        }
        tocBuilder.AppendLine();

        // 插入到 frontmatter 之后或第一个 # 标题之后
        var fmMatch = FrontmatterRegex.Match(markdown);
        if (fmMatch.Success)
        {
            var insertPos = fmMatch.Index + fmMatch.Length;
            return Task.FromResult(markdown.Insert(insertPos, tocBuilder.ToString() + "\n"));
        }

        var firstHeadingEnd = markdown.IndexOf('\n');
        if (firstHeadingEnd >= 0)
        {
            return Task.FromResult(markdown.Insert(firstHeadingEnd + 1, "\n" + tocBuilder));
        }

        return Task.FromResult(tocBuilder + markdown);
    }
}

/// <summary>
/// 2. 标题跳级修复与规范化转换器（例如一级 # 直接跳到 ### 时规整）
/// </summary>
public class HeadingNormalizerTransform : IHumanizerTransform
{
    public string Name => "HeadingNormalizer";

    private static readonly Regex HeadingLineRegex = new(
        @"^(#{1,6})\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public Task<string> ApplyAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken)
    {
        if (!options.EnableHeadingNormalizer) return Task.FromResult(markdown);

        var lines = markdown.Split('\n');
        var sb = new StringBuilder();
        int lastLevel = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var match = HeadingLineRegex.Match(line);
            if (match.Success)
            {
                int currentLevel = match.Groups[1].Value.Length;
                string text = match.Groups[2].Value.Trim();

                // 修复跳级：若从 1 级直接跳到 3 级或更深，则规整为 lastLevel + 1
                if (lastLevel > 0 && currentLevel > lastLevel + 1)
                {
                    currentLevel = lastLevel + 1;
                }

                lastLevel = currentLevel;
                sb.AppendLine($"{new string('#', currentLevel)} {text}");
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return Task.FromResult(sb.ToString().TrimEnd('\r', '\n') + "\n");
    }
}

/// <summary>
/// 3. 表格对齐与排版优化转换器
/// </summary>
public class TableFormatterTransform : IHumanizerTransform
{
    public string Name => "TableFormatter";

    public Task<string> ApplyAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken)
    {
        if (!options.EnableTableFormatter) return Task.FromResult(markdown);

        // 规范化表格行两端的管道符和空格
        var lines = markdown.Split('\n');
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith('|') && line.EndsWith('|'))
            {
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                sb.AppendLine("| " + string.Join(" | ", cells) + " |");
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return Task.FromResult(sb.ToString().TrimEnd('\r', '\n') + "\n");
    }
}

/// <summary>
/// 4. 嵌套列表缩进规范化转换器
/// </summary>
public class ListNormalizerTransform : IHumanizerTransform
{
    public string Name => "ListNormalizer";

    private static readonly Regex ListRegex = new(
        @"^(\s*)([-*+]|\d+\.)\s+(.+)$",
        RegexOptions.Compiled);

    public Task<string> ApplyAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken)
    {
        if (!options.EnableListNormalizer) return Task.FromResult(markdown);

        var lines = markdown.Split('\n');
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var match = ListRegex.Match(line);
            if (match.Success)
            {
                var leadingSpaces = match.Groups[1].Value.Length;
                var marker = match.Groups[2].Value;
                var text = match.Groups[3].Value.Trim();

                // 规范为 2 空格/4 空格对齐
                var normalizedLevel = leadingSpaces / 2;
                var normalizedIndent = new string(' ', normalizedLevel * 2);

                sb.AppendLine($"{normalizedIndent}{marker} {text}");
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return Task.FromResult(sb.ToString().TrimEnd('\r', '\n') + "\n");
    }
}

/// <summary>
/// 5. 公文关键要素（发文字号、发文机关、成文日期）识别加粗高亮转换器
/// </summary>
public class DocHighlightTransform : IHumanizerTransform
{
    public string Name => "DocHighlight";

    // 匹配公文发文字号，如 国发〔2026〕1号 或 发文字号：xxx
    private static readonly Regex DocNumberRegex = new(
        @"([^\n\r]+?〔\d{4}〕\d+号)",
        RegexOptions.Compiled);

    // 匹配成文日期，如 2026年8月12日 或 二〇二六年八月十二日
    private static readonly Regex DocDateRegex = new(
        @"(\d{4}年\d{1,2}月\d{1,2}日)",
        RegexOptions.Compiled);

    public Task<string> ApplyAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken)
    {
        if (!options.EnableDocHighlight) return Task.FromResult(markdown);

        // 如果提取到了公文元数据，精准加粗
        var content = markdown;

        if (result.Metadata.GovMetadata != null)
        {
            var docNo = result.Metadata.GovMetadata.DocumentNumber;
            if (!string.IsNullOrEmpty(docNo) && content.Contains(docNo) && !content.Contains($"**{docNo}**"))
            {
                content = content.Replace(docNo, $"**{docNo}**");
            }
        }

        return Task.FromResult(content);
    }
}
