using System.Text.RegularExpressions;
using Doc2MD.Pipeline.Models;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// Markdown → SemanticDocument 语义解析器。
/// 只负责语义解析，不包含任何 Word 样式逻辑。
/// 解析结果为 SemanticDocument，供 DocxRenderer 使用。
/// </summary>
public static class SemanticDocumentConverter
{
    private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.+)", RegexOptions.Compiled);
    private static readonly Regex UnorderedListPattern = new(@"^(\s*)([-*+])\s+(.+)", RegexOptions.Compiled);
    private static readonly Regex OrderedListPattern = new(@"^(\s*)(\d+)\.\s+(.+)", RegexOptions.Compiled);
    private static readonly Regex HorizontalRulePattern = new(@"^(-{3,}|\*{3,}|_{3,})$", RegexOptions.Compiled);
    private static readonly Regex QuotePattern = new(@"^>\s*(.*)", RegexOptions.Compiled);

    /// <summary>将 Markdown 文本解析为 SemanticDocument</summary>
    public static SemanticDocument Convert(string markdown)
    {
        var doc = new SemanticDocument();
        var lines = markdown.Split('\n');
        var currentParagraph = new System.Text.StringBuilder();
        int i = SkipPreamble(lines);

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmedLine = line.Trim();

            // 空行 → 段落结束
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(doc, currentParagraph);
                i++;
                continue;
            }

            // HTML 注释 → 跳过（单行 + 多行）
            if (IsHtmlComment(trimmedLine))
            {
                i = SkipHtmlComment(lines, i);
                continue;
            }

            // 各语义块：命中即刷新段落并解析，返回 true 表示已消费该行
            if (TryParseHeading(doc, currentParagraph, line, i, out var nextIndex)
                || TryParseTable(doc, currentParagraph, lines, i, trimmedLine, out nextIndex)
                || TryParseUnorderedList(doc, currentParagraph, line, i, out nextIndex)
                || TryParseOrderedList(doc, currentParagraph, line, i, out nextIndex)
                || TryParseHorizontalRule(doc, currentParagraph, trimmedLine, i, out nextIndex)
                || TryParseBlockquote(doc, currentParagraph, line, i, out nextIndex))
            {
                i = nextIndex;
                continue;
            }

            // 普通段落（累积，保留行内格式标记）
            currentParagraph.AppendLine(line);
            i++;
        }

        // 最后一段
        FlushParagraph(doc, currentParagraph);

        return doc;
    }

    /// <summary>
    /// 解析行内格式：将 Markdown 行内标记转为 InlineRun 列表。
    /// 支持：**粗体**、*斜体*、`代码`、~~删除线~~
    /// </summary>
    public static List<InlineRun> ParseInlineFormatting(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [new InlineRun { Text = text }];

        var runs = new List<InlineRun>();

        // 处理图片和链接：先提取为纯文本
        text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");

        // 使用位置追踪逐段解析
        int pos = 0;
        var combinedPattern = new Regex(
            @"(\*\*\*(.+?)\*\*\*)" +    // 粗体+斜体
            @"|(\*\*(.+?)\*\*)" +        // 粗体
            @"|(\*(.+?)\*)" +            // 斜体
            @"|(~~(.+?)~~)" +            // 删除线
            @"|(`(.+?)`)",               // 行内代码
            RegexOptions.Compiled);

        var match = combinedPattern.Match(text, pos);
        while (match.Success)
        {
            if (match.Index > pos)
                runs.Add(new InlineRun { Text = text[pos..match.Index] });

            if (match.Groups[2].Success)
                runs.Add(new InlineRun { Text = match.Groups[2].Value, Bold = true, Italic = true });
            else if (match.Groups[4].Success)
                runs.Add(new InlineRun { Text = match.Groups[4].Value, Bold = true });
            else if (match.Groups[6].Success)
                runs.Add(new InlineRun { Text = match.Groups[6].Value, Italic = true });
            else if (match.Groups[8].Success)
                runs.Add(new InlineRun { Text = match.Groups[8].Value, Strikethrough = true });
            else if (match.Groups[10].Success)
                runs.Add(new InlineRun { Text = match.Groups[10].Value, Code = true });

            pos = match.Index + match.Length;
            match = combinedPattern.Match(text, pos);
        }

        if (pos < text.Length)
            runs.Add(new InlineRun { Text = text[pos..] });

        // 合并相邻的普通文本 Run
        var merged = new List<InlineRun>();
        foreach (var run in runs)
        {
            if (merged.Count > 0
                && !merged[^1].Bold && !merged[^1].Italic && !merged[^1].Strikethrough && !merged[^1].Code
                && !run.Bold && !run.Italic && !run.Strikethrough && !run.Code)
            {
                merged[^1].Text += run.Text;
            }
            else
            {
                merged.Add(run);
            }
        }

        return merged;
    }

    /// <summary>清理内联格式（仅用于表格单元格等不需要格式还原的场景）</summary>
    internal static string CleanInlineFormatting(string text)
    {
        text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");
        text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "$1");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"~~(.+?)~~", "$1");
        text = Regex.Replace(text, @"`(.+?)`", "$1");
        return text.Trim();
    }

    // ==================== 前置处理（C5 拆分） ====================

    /// <summary>跳过 frontmatter 与 AI_AGENT_NOTICE 注释块，返回第一个有效行索引</summary>
    private static int SkipPreamble(string[] lines)
    {
        int i = 0;

        // 跳过 frontmatter
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            i = 1;
            while (i < lines.Length && lines[i].Trim() != "---") i++;
            i++; // skip closing ---
        }

        // 跳过 AI_AGENT_NOTICE 块
        while (i < lines.Length && lines[i].Trim().StartsWith("<!--")) i++;

        return i;
    }

    /// <summary>将累积的普通段落文本刷新为 ParagraphBlock（若无内容则忽略）</summary>
    private static void FlushParagraph(SemanticDocument doc, System.Text.StringBuilder currentParagraph)
    {
        if (currentParagraph.Length <= 0)
            return;

        var content = currentParagraph.ToString().Trim();
        doc.Blocks.Add(new ParagraphBlock
        {
            Content = content,
            Runs = ParseInlineFormatting(content)
        });
        currentParagraph.Clear();
    }

    private static bool IsHtmlComment(string trimmedLine)
    {
        return trimmedLine.StartsWith("<!--");
    }

    /// <summary>
    /// 跳过 HTML 注释（单行 + 多行），返回注释之后的下一个行索引。
    /// 调用前提：<paramref name="i"/> 指向注释起始行。
    /// </summary>
    private static int SkipHtmlComment(string[] lines, int i)
    {
        if (lines[i].Trim().EndsWith("-->"))
            return i + 1;

        i++;
        while (i < lines.Length && !lines[i].Contains("-->"))
        {
            i++;
        }
        return i < lines.Length ? i + 1 : i;
    }

    // ==================== 语义块解析（C5 拆分） ====================

    /// <summary>解析标题（# ~ ######）</summary>
    private static bool TryParseHeading(SemanticDocument doc, System.Text.StringBuilder paragraph,
        string line, int currentIndex, out int nextIndex)
    {
        var match = HeadingPattern.Match(line);
        if (!match.Success)
        {
            nextIndex = -1;
            return false;
        }

        FlushParagraph(doc, paragraph);

        var headingContent = match.Groups[2].Value.Trim();
        doc.Blocks.Add(new HeadingBlock
        {
            Level = match.Groups[1].Value.Length,
            Content = headingContent,
            Runs = ParseInlineFormatting(headingContent)
        });

        nextIndex = currentIndex + 1;
        return true;
    }

    /// <summary>解析表格（连续 | 开头行）</summary>
    private static bool TryParseTable(SemanticDocument doc, System.Text.StringBuilder paragraph,
        string[] lines, int i, string trimmedLine, out int nextIndex)
    {
        if (!trimmedLine.StartsWith("|"))
        {
            nextIndex = -1;
            return false;
        }

        FlushParagraph(doc, paragraph);

        var tableLines = new List<string>();
        while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
        {
            tableLines.Add(lines[i]);
            i++;
        }

        doc.Blocks.Add(ParseTable(tableLines));
        nextIndex = i;
        return true;
    }

    /// <summary>解析无序列表项（- * + 开头）</summary>
    private static bool TryParseUnorderedList(SemanticDocument doc, System.Text.StringBuilder paragraph,
        string line, int currentIndex, out int nextIndex)
    {
        var match = UnorderedListPattern.Match(line);
        if (!match.Success)
        {
            nextIndex = -1;
            return false;
        }

        FlushParagraph(doc, paragraph);

        // 尝试合并到已有 ListBlock
        var itemContent = match.Groups[3].Value;
        if (doc.Blocks.Count > 0 && doc.Blocks[^1] is ListBlock lb && !lb.IsOrdered)
        {
            lb.Items.Add(new ListItem
            {
                Content = itemContent,
                Runs = ParseInlineFormatting(itemContent)
            });
        }
        else
        {
            var list = new ListBlock { IsOrdered = false };
            list.Items.Add(new ListItem
            {
                Content = itemContent,
                Runs = ParseInlineFormatting(itemContent)
            });
            doc.Blocks.Add(list);
        }

        nextIndex = currentIndex + 1;
        return true;
    }

    /// <summary>解析有序列表项（数字. 开头）</summary>
    private static bool TryParseOrderedList(SemanticDocument doc, System.Text.StringBuilder paragraph,
        string line, int currentIndex, out int nextIndex)
    {
        var match = OrderedListPattern.Match(line);
        if (!match.Success)
        {
            nextIndex = -1;
            return false;
        }

        FlushParagraph(doc, paragraph);

        var itemContent = match.Groups[3].Value;
        var order = int.Parse(match.Groups[2].Value);
        if (doc.Blocks.Count > 0 && doc.Blocks[^1] is ListBlock lb && lb.IsOrdered)
        {
            lb.Items.Add(new ListItem
            {
                Order = order,
                Content = itemContent,
                Runs = ParseInlineFormatting(itemContent)
            });
        }
        else
        {
            var list = new ListBlock { IsOrdered = true };
            list.Items.Add(new ListItem
            {
                Order = order,
                Content = itemContent,
                Runs = ParseInlineFormatting(itemContent)
            });
            doc.Blocks.Add(list);
        }

        nextIndex = currentIndex + 1;
        return true;
    }

    /// <summary>解析分隔线（--- / *** / ___）</summary>
    private static bool TryParseHorizontalRule(SemanticDocument doc, System.Text.StringBuilder paragraph,
        string trimmedLine, int currentIndex, out int nextIndex)
    {
        if (!HorizontalRulePattern.IsMatch(trimmedLine))
        {
            nextIndex = -1;
            return false;
        }

        FlushParagraph(doc, paragraph);
        doc.Blocks.Add(new HorizontalRuleBlock());

        nextIndex = currentIndex + 1;
        return true;
    }

    /// <summary>解析引用块（&gt; 开头），连续引用合并到同一 QuoteBlock</summary>
    private static bool TryParseBlockquote(SemanticDocument doc, System.Text.StringBuilder paragraph,
        string line, int currentIndex, out int nextIndex)
    {
        var match = QuotePattern.Match(line);
        if (!match.Success)
        {
            nextIndex = -1;
            return false;
        }

        FlushParagraph(doc, paragraph);

        // 尝试合并到已有 QuoteBlock
        var quoteContent = match.Groups[1].Value;
        if (doc.Blocks.Count > 0 && doc.Blocks[^1] is QuoteBlock qb)
        {
            qb.Runs.Add(new InlineRun { Text = "\n" });
            qb.Runs.AddRange(ParseInlineFormatting(quoteContent));
            qb.Content += "\n" + quoteContent;
        }
        else
        {
            doc.Blocks.Add(new QuoteBlock
            {
                Content = quoteContent,
                Runs = ParseInlineFormatting(quoteContent)
            });
        }

        nextIndex = currentIndex + 1;
        return true;
    }

    /// <summary>解析 Markdown 表格行</summary>
    private static TableBlock ParseTable(List<string> lines)
    {
        var rows = new List<List<TableCellContent>>();
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line.Trim(), @"^\|[\s\-:|]+\|$"))
                continue;

            var cells = line.Split('|')
                .Skip(1)
                .SkipLast(1)
                .Select(c => c.Trim())
                .ToList();

            if (cells.Count > 0 && cells.Any(c => !string.IsNullOrEmpty(c)))
            {
                rows.Add(cells.Select(c => new TableCellContent
                {
                    RawText = c,
                    Runs = ParseInlineFormatting(c)
                }).ToList());
            }
        }

        return new TableBlock { Rows = rows };
    }
}
