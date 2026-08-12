using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Doc2MD.Pipeline.Models;

namespace Doc2MD.Services;

/// <summary>
/// F2: 将语义文档（SemanticDocument）渲染为 WPF FlowDocument 预览。
/// 复用 ConversionService 的 Markdown 语义解析管线，保证预览与实际转换结构一致。
/// 颜色通过 SetResourceReference 引用应用级画刷，深色/浅色主题切换时自动生效。
/// </summary>
public static class MarkdownPreviewBuilder
{
    private const string Fangsong = "仿宋";
    private const string Heiti = "黑体";
    private const string KaiTi = "楷体";
    private const string MonoFont = "Consolas, Microsoft YaHei";

    /// <summary>将语义文档构建为 FlowDocument（需在 UI 线程调用）。</summary>
    public static FlowDocument Build(SemanticDocument document)
    {
        var flowDoc = new FlowDocument
        {
            FontFamily = new FontFamily(Fangsong),
            FontSize = 14,
            LineHeight = 26,
            PagePadding = new Thickness(24, 18, 24, 24),
            ColumnWidth = double.MaxValue,
            TextAlignment = TextAlignment.Justify
        };
        flowDoc.SetResourceReference(TextElement.ForegroundProperty, "TextMainBrush");

        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    flowDoc.Blocks.Add(BuildHeading(heading));
                    break;
                case ParagraphBlock paragraph:
                    flowDoc.Blocks.Add(BuildParagraph(paragraph));
                    break;
                case TableBlock table:
                    flowDoc.Blocks.Add(BuildTable(table));
                    break;
                case ListBlock list:
                    flowDoc.Blocks.Add(BuildList(list));
                    break;
                case QuoteBlock quote:
                    flowDoc.Blocks.Add(BuildQuote(quote));
                    break;
                case HorizontalRuleBlock:
                    flowDoc.Blocks.Add(BuildHorizontalRule());
                    break;
            }
        }

        return flowDoc;
    }

    private static Paragraph BuildHeading(HeadingBlock heading)
    {
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(Heiti),
            Margin = new Thickness(0, heading.Level <= 1 ? 12 : 10, 0, 6)
        };
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextMainBrush");

        // 一级/二级标题使用主题色强调，其余使用次要色
        if (heading.Level <= 1)
        {
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "PrimaryBrush");
        }

        paragraph.FontSize = heading.Level switch
        {
            1 => 20,
            2 => 17,
            3 => 15.5,
            _ => 14
        };
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.TextAlignment = TextAlignment.Left;

        var runs = BuildInlines(heading.Runs, heading.Content);
        foreach (var run in runs)
        {
            paragraph.Inlines.Add(run);
        }

        return paragraph;
    }

    private static Paragraph BuildParagraph(ParagraphBlock paragraphBlock)
    {
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(Fangsong),
            Margin = new Thickness(0, 0, 0, 8)
        };
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextMainBrush");

        // 围栏代码块降级处理：语义层按段落累积，这里识别 ``` 包裹的代码块
        var content = paragraphBlock.Content.Trim();
        if (content.StartsWith("```", StringComparison.Ordinal) || content.StartsWith("~~~", StringComparison.Ordinal))
        {
            return BuildCodeBlock(content);
        }

        var runs = BuildInlines(paragraphBlock.Runs, paragraphBlock.Content);
        foreach (var run in runs)
        {
            paragraph.Inlines.Add(run);
        }

        return paragraph;
    }

    private static Paragraph BuildCodeBlock(string content)
    {
        var lines = content.Split('\n');
        var codeLines = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
                continue;
            codeLines.Add(line);
        }

        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(MonoFont),
            FontSize = 12.5,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(12, 8, 12, 8),
            TextAlignment = TextAlignment.Left
        };
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextSecondaryBrush");
        paragraph.SetResourceReference(Paragraph.BackgroundProperty, "BgSubtleBrush");
        paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "DividerBrush");
        paragraph.BorderThickness = new Thickness(1);
        paragraph.Inlines.Add(new Run(string.Join("\n", codeLines)));

        return paragraph;
    }

    private static Block BuildTable(TableBlock tableBlock)
    {
        var table = new Table
        {
            Margin = new Thickness(0, 0, 0, 10),
            CellSpacing = 0,
            BorderThickness = new Thickness(1)
        };
        table.SetResourceReference(Table.BorderBrushProperty, "BorderBrush");
        table.SetResourceReference(Table.BackgroundProperty, "BgCardBrush");

        var columnCount = tableBlock.Rows.Count > 0 ? tableBlock.Rows[0].Count : 1;
        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        for (var r = 0; r < tableBlock.Rows.Count; r++)
        {
            var row = new TableRow();
            rowGroup.Rows.Add(row);
            var cells = tableBlock.Rows[r];
            for (var c = 0; c < columnCount; c++)
            {
                var cell = new TableCell
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    BorderThickness = new Thickness(0.5)
                };
                row.Cells.Add(cell);
                cell.SetResourceReference(TableCell.BorderBrushProperty, "BorderBrush");
                cell.SetResourceReference(TableCell.BackgroundProperty, r == 0 ? "BgSubtleBrush" : "BgCardBrush");

                var cellContent = c < cells.Count ? cells[c] : new TableCellContent();
                var paragraph = new Paragraph
                {
                    FontFamily = new FontFamily(r == 0 ? Heiti : Fangsong),
                    FontSize = 12.5,
                    Margin = new Thickness(0)
                };
                paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextMainBrush");
                foreach (var run in BuildInlines(cellContent.Runs, cellContent.RawText))
                {
                    paragraph.Inlines.Add(run);
                }
                cell.Blocks.Add(paragraph);
            }
        }

        return table;
    }

    private static Block BuildList(ListBlock listBlock)
    {
        var list = new List
        {
            MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(18, 0, 0, 8),
            Padding = new Thickness(0)
        };

        foreach (var item in listBlock.Items)
        {
            var listItem = new System.Windows.Documents.ListItem();
            var paragraph = new Paragraph
            {
                FontFamily = new FontFamily(Fangsong),
                Margin = new Thickness(0, 0, 0, 4)
            };
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextMainBrush");
            foreach (var run in BuildInlines(item.Runs, item.Content))
            {
                paragraph.Inlines.Add(run);
            }
            listItem.Blocks.Add(paragraph);
            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static Block BuildQuote(QuoteBlock quoteBlock)
    {
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily(KaiTi),
            FontSize = 13.5,
            Margin = new Thickness(12, 0, 0, 10),
            Padding = new Thickness(12, 8, 12, 8)
        };
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextSecondaryBrush");
        paragraph.SetResourceReference(Paragraph.BackgroundProperty, "BgSubtleBrush");
        paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "PrimarySoftBrush");
        paragraph.BorderThickness = new Thickness(3, 0, 0, 0);

        foreach (var run in BuildInlines(quoteBlock.Runs, quoteBlock.Content))
        {
            paragraph.Inlines.Add(run);
        }

        return paragraph;
    }

    private static Block BuildHorizontalRule()
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 4, 0, 10) };
        paragraph.SetResourceReference(Paragraph.BorderBrushProperty, "DividerBrush");
        paragraph.BorderThickness = new Thickness(0, 1, 0, 0);
        return paragraph;
    }

    /// <summary>将行内格式片段映射为 Run 集合；无片段时按原文整体生成。</summary>
    private static IEnumerable<Run> BuildInlines(IReadOnlyList<InlineRun> runs, string rawText)
    {
        if (runs.Count == 0)
        {
            yield return new Run(rawText);
            yield break;
        }

        foreach (var inline in runs)
        {
            var run = new Run(inline.Text);
            if (inline.Bold)
            {
                run.FontWeight = FontWeights.Bold;
            }
            if (inline.Italic)
            {
                run.FontStyle = FontStyles.Italic;
            }
            if (inline.Strikethrough)
            {
                run.TextDecorations = TextDecorations.Strikethrough;
            }
            if (inline.Code)
            {
                run.FontFamily = new FontFamily(MonoFont);
                run.FontSize = Math.Max(10.5, run.FontSize - 0.5);
                run.SetResourceReference(Run.BackgroundProperty, "BgSubtleBrush");
                run.SetResourceReference(Run.ForegroundProperty, "PrimaryBrush");
            }

            yield return run;
        }
    }
}
