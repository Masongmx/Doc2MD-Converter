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
    /// <summary>将 Markdown 文本解析为 SemanticDocument</summary>
    public static SemanticDocument Convert(string markdown)
    {
        var doc = new SemanticDocument();
        var lines = markdown.Split('\n');
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

        var currentParagraph = new System.Text.StringBuilder();

        while (i < lines.Length)
        {
            var line = lines[i];

            // 空行 → 段落结束
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }
                i++;
                continue;
            }

            // HTML 注释 → 跳过（单行 + 多行）
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("<!--"))
            {
                if (trimmedLine.EndsWith("-->"))
                {
                    i++;
                    continue;
                }
                i++;
                while (i < lines.Length)
                {
                    if (lines[i].Contains("-->"))
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            // 标题
            var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)");
            if (headingMatch.Success)
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }

                var headingContent = headingMatch.Groups[2].Value.Trim();
                doc.Blocks.Add(new HeadingBlock
                {
                    Level = headingMatch.Groups[1].Value.Length,
                    Content = headingContent,
                    Runs = ParseInlineFormatting(headingContent)
                });
                i++;
                continue;
            }

            // 表格
            if (trimmedLine.StartsWith("|"))
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }

                var tableLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                {
                    tableLines.Add(lines[i]);
                    i++;
                }

                doc.Blocks.Add(ParseTable(tableLines));
                continue;
            }

            // 无序列表项
            var listMatch = Regex.Match(line, @"^(\s*)([-*+])\s+(.+)");
            if (listMatch.Success)
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }

                // 尝试合并到已有 ListBlock
                var itemContent = listMatch.Groups[3].Value;
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
                i++;
                continue;
            }

            // 有序列表项
            var orderedListMatch = Regex.Match(line, @"^(\s*)(\d+)\.\s+(.+)");
            if (orderedListMatch.Success)
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }

                var itemContent = orderedListMatch.Groups[3].Value;
                var order = int.Parse(orderedListMatch.Groups[2].Value);
                if (doc.Blocks.Count > 0 && doc.Blocks[^1] is ListBlock lb2 && lb2.IsOrdered)
                {
                    lb2.Items.Add(new ListItem
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
                i++;
                continue;
            }

            // 分隔线
            if (Regex.IsMatch(trimmedLine, @"^(-{3,}|\*{3,}|_{3,})$"))
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }

                doc.Blocks.Add(new HorizontalRuleBlock());
                i++;
                continue;
            }

            // 引用
            var quoteMatch = Regex.Match(line, @"^>\s*(.*)");
            if (quoteMatch.Success)
            {
                if (currentParagraph.Length > 0)
                {
                    var content = currentParagraph.ToString().Trim();
                    doc.Blocks.Add(new ParagraphBlock
                    {
                        Content = content,
                        Runs = ParseInlineFormatting(content)
                    });
                    currentParagraph.Clear();
                }

                // 尝试合并到已有 QuoteBlock
                var quoteContent = quoteMatch.Groups[1].Value;
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
                i++;
                continue;
            }

            // 普通段落（累积，保留行内格式标记）
            currentParagraph.AppendLine(line);
            i++;
        }

        // 最后一段
        if (currentParagraph.Length > 0)
        {
            var content = currentParagraph.ToString().Trim();
            doc.Blocks.Add(new ParagraphBlock
            {
                Content = content,
                Runs = ParseInlineFormatting(content)
            });
        }

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
