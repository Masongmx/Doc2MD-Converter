using System.Text;
using System.Text.RegularExpressions;
using Doc2MD.Models;
using UglyToad.PdfPig.Content;

namespace Doc2MD.Parsers;

/// <summary>
/// PDF 文本合并与 Markdown 输出
/// </summary>
internal class PdfTextMerger
{
    private readonly PdfLineClassifier _lineClassifier;

    public PdfTextMerger(PdfLineClassifier lineClassifier)
    {
        _lineClassifier = lineClassifier;
    }

    #region 文本合并与段落输出

    /// <summary>
    /// 将行信息合并为Markdown段落输出
    /// </summary>
    internal string MergeIntoParagraphs(List<PdfLineClassifier.LineInfo> lines, HashSet<int> pageBreakIndices, List<ConversionWarning>? warnings = null)
    {
        if (lines.Count == 0) return "";

        // 计算正文行的常见左边距
        var margins = lines
            .Where(l => l.Type == PdfLineClassifier.LineType.BodyText && !string.IsNullOrWhiteSpace(l.Text) && !l.Merged)
            .Select(l => Math.Round(l.LeftMargin, 0))
            .ToList();

        double baseMargin = margins.Count > 0
            ? margins.GroupBy(m => m).OrderByDescending(g => g.Count()).First().Key
            : 0;

        double marginThreshold = 10;

        // 计算正文行的行间距
        double avgSpacing = 0;
        double paragraphGapThreshold = 0;
        var bodyPositions = lines
            .Where(l => l.Type == PdfLineClassifier.LineType.BodyText && !string.IsNullOrWhiteSpace(l.Text) && !l.Merged)
            .Select(l => l.TopPosition)
            .OrderByDescending(p => p)
            .ToList();

        if (bodyPositions.Count > 1)
        {
            var spacings = new List<double>();
            for (int si = 1; si < bodyPositions.Count; si++)
            {
                var sp = bodyPositions[si - 1] - bodyPositions[si];
                if (sp > 0) spacings.Add(sp);
            }
            if (spacings.Count > 0)
            {
                var sorted = spacings.OrderBy(s => s).ToList();
                avgSpacing = sorted[sorted.Count / 2];
                paragraphGapThreshold = avgSpacing * 1.8;
            }
        }

        var content = new StringBuilder();
        bool isFirstOutput = true;
        bool inParagraph = false;
        string paragraphText = "";
        double lastBodyTextTop = 0;
        int currentPageNumber = 0;


        int i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (line.Merged) { i++; continue; }

            if (line.Type == PdfLineClassifier.LineType.Empty)
            {
                // P0: 检查是否是页边界，输出 PAGE_START 标记
                if (pageBreakIndices.Contains(i) && line.IsPageBreak)
                {
                    currentPageNumber++;
                    if (currentPageNumber > 1)
                    {
                        FlushParagraph(content, ref paragraphText, ref inParagraph);
                        content.AppendLine();
                    }
                    content.AppendLine($"<!-- PAGE_START: {currentPageNumber} -->");
                    isFirstOutput = false;
                    i++;
                    continue;
                }

                int emptyCount = 0;
                int j = i;
                while (j < lines.Count && (lines[j].Merged || lines[j].Type == PdfLineClassifier.LineType.Empty))
                {
                    if (!lines[j].Merged && lines[j].Type == PdfLineClassifier.LineType.Empty) emptyCount++;
                    j++;
                }

                if (!inParagraph || string.IsNullOrEmpty(paragraphText))
                {
                    i = j;
                    continue;
                }

                if (emptyCount >= 2)
                {
                    FlushParagraph(content, ref paragraphText, ref inParagraph);
                    i = j;
                    continue;
                }

                if (j < lines.Count)
                {
                    var nextNonEmpty = lines[j];
                    if (nextNonEmpty.Type == PdfLineClassifier.LineType.Heading ||
                        nextNonEmpty.Type == PdfLineClassifier.LineType.OrderedList ||
                        nextNonEmpty.Type == PdfLineClassifier.LineType.UnorderedList)
                    {
                        FlushParagraph(content, ref paragraphText, ref inParagraph);
                    }
                    else if (nextNonEmpty.Type == PdfLineClassifier.LineType.BodyText)
                    {
                        bool isIndented = nextNonEmpty.LeftMargin > baseMargin + marginThreshold;
                        bool prevEndsParagraph = PdfStyles.EndsWithParagraphMark(paragraphText);
                        bool isAcrossPage = pageBreakIndices.Contains(j) && !PdfStyles.EndsWithParagraphMark(paragraphText);
                        bool shouldBreak = (prevEndsParagraph || isIndented) && !isAcrossPage;

                        if (shouldBreak)
                        {
                            FlushParagraph(content, ref paragraphText, ref inParagraph);
                        }
                    }
                }
                else
                {
                    FlushParagraph(content, ref paragraphText, ref inParagraph);
                }

                i = j;
                continue;
            }

            if (line.Type == PdfLineClassifier.LineType.Heading)
            {
                FlushParagraph(content, ref paragraphText, ref inParagraph);

                if (!isFirstOutput) content.AppendLine();
                content.AppendLine($"{new string('#', line.HeadingLevel)} {line.Text}");
                isFirstOutput = false;
            }
            else if (line.Type == PdfLineClassifier.LineType.OrderedList)
            {
                FlushParagraph(content, ref paragraphText, ref inParagraph);

                if (!isFirstOutput) content.AppendLine();
                var itemText = Regex.Replace(line.Text, @"^（[一二三四五六七八九十零\d]+）\s*", "");
                itemText = Regex.Replace(itemText, @"^（\d+）\s*", "");
                itemText = Regex.Replace(itemText, @"^\d+[.．、]\s*", "");
                content.AppendLine($"{line.ListNumber}. {itemText}");
                isFirstOutput = false;
            }
            else if (line.Type == PdfLineClassifier.LineType.UnorderedList)
            {
                FlushParagraph(content, ref paragraphText, ref inParagraph);

                if (!isFirstOutput) content.AppendLine();
                var itemText = Regex.Replace(line.Text, @"^[•●○◆▪]\s*", "");
                content.AppendLine($"- {itemText}");
                isFirstOutput = false;
            }
            else if (line.Type == PdfLineClassifier.LineType.Table)
            {
                FlushParagraph(content, ref paragraphText, ref inParagraph);

                var tableRows = new List<PdfLineClassifier.LineInfo> { line };
                int ti = i + 1;
                while (ti < lines.Count && lines[ti].Type == PdfLineClassifier.LineType.Table)
                {
                    tableRows.Add(lines[ti]);
                    lines[ti].Merged = true;
                    ti++;
                }

                if (!isFirstOutput) content.AppendLine();
                RenderMarkdownTable(content, tableRows, warnings);
                i = ti - 1;
                isFirstOutput = false;
            }
            else // BodyText
            {
                bool startsNewParagraph;

                if (!inParagraph)
                {
                    startsNewParagraph = true;
                }
                else
                {
                    bool isIndented = line.LeftMargin > baseMargin + marginThreshold;
                    bool prevEndsParagraph = PdfStyles.EndsWithParagraphMark(paragraphText);
                    bool isAcrossPage = pageBreakIndices.Contains(i) && !PdfStyles.EndsWithParagraphMark(paragraphText);
                    startsNewParagraph = (prevEndsParagraph || isIndented) && !isAcrossPage;
                }

                if (startsNewParagraph)
                {
                    FlushParagraph(content, ref paragraphText, ref inParagraph);
                    paragraphText = line.Text;
                    inParagraph = true;
                }
                else
                {
                    paragraphText = JoinText(paragraphText, line.Text);
                }

                lastBodyTextTop = line.TopPosition;
                isFirstOutput = false;
            }

            i++;
        }

        FlushParagraph(content, ref paragraphText, ref inParagraph);

        var result = content.ToString();
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim() + Environment.NewLine;
    }

    internal void FlushParagraph(StringBuilder content, ref string paragraphText, ref bool inParagraph)
    {
        if (inParagraph && !string.IsNullOrEmpty(paragraphText))
        {
            content.AppendLine(paragraphText);
            content.AppendLine();
        }
        inParagraph = false;
        paragraphText = "";
    }

    /// <summary>
    /// 合并两段文本，处理中文不加空格和PDF断词
    /// </summary>
    internal string JoinText(string prev, string next)
    {
        if (string.IsNullOrEmpty(prev)) return next;
        if (string.IsNullOrEmpty(next)) return prev;

        if (PdfStyles.EndsWithCJK(prev) && PdfStyles.StartsWithCJK(next))
        {
            return prev + next;
        }
        else
        {
            return prev + " " + next;
        }
    }

    #endregion

    #region Markdown 表格渲染

    /// <summary>
    /// 将表格行渲染为 Markdown 表格
    /// </summary>
    internal void RenderMarkdownTable(StringBuilder content, List<PdfLineClassifier.LineInfo> tableRows, List<ConversionWarning>? warnings = null)
    {
        if (tableRows.Count == 0) return;

        // 确定列数
        int maxCol = tableRows
            .Where(r => r.TableCells != null)
            .SelectMany(r => r.TableCells!)
            .DefaultIfEmpty(new PdfLineClassifier.TableCell())
            .Max(c => c.ColumnIndex) + 1;

        if (maxCol < 2)
        {
            // 退化为普通文本输出
            content.AppendLine("<!-- TABLE_DEGRADED: 表格列数不足2列，退化为纯文本 -->");
            foreach (var row in tableRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Text))
                    content.AppendLine(row.Text);
            }
            content.AppendLine();
            warnings?.Add(ConversionWarning.Create(
                "W_TABLE_DEGRADE", "PDF 表格列数不足2列，退化为纯文本输出"));
            return;
        }

        // 构建二维数组
        var grid = new string[tableRows.Count, maxCol];
        for (int r = 0; r < tableRows.Count; r++)
        {
            for (int c = 0; c < maxCol; c++)
            {
                grid[r, c] = "";
            }

            if (tableRows[r].TableCells != null)
            {
                foreach (var cell in tableRows[r].TableCells!)
                {
                    if (cell.ColumnIndex >= 0 && cell.ColumnIndex < maxCol)
                    {
                        string existing = grid[r, cell.ColumnIndex];
                        grid[r, cell.ColumnIndex] = string.IsNullOrEmpty(existing)
                            ? cell.Text
                            : existing + " " + cell.Text;
                    }
                }
            }
        }

        // 质量检测：如果空 cell 超过 60%，退化为普通文本
        int totalCells = tableRows.Count * maxCol;
        int emptyCells = 0;
        for (int r = 0; r < tableRows.Count; r++)
        {
            for (int c = 0; c < maxCol; c++)
            {
                if (string.IsNullOrWhiteSpace(grid[r, c])) emptyCells++;
            }
        }
        if (totalCells > 0 && (double)emptyCells / totalCells > 0.4)
        {
            content.AppendLine("<!-- TABLE_DEGRADED: 空单元格超过40%，退化为纯文本 -->");
            foreach (var row in tableRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Text))
                    content.AppendLine(row.Text);
            }
            content.AppendLine();
            warnings?.Add(ConversionWarning.Create(
                "W_TABLE_DEGRADE",
                $"PDF 表格空单元格率 {(double)emptyCells / totalCells:P0}，退化为纯文本输出"));
            return;
        }

        // 合并表头区域
        int headerEndRow = 0;
        for (int r = 1; r < tableRows.Count; r++)
        {
            int nonEmptyCols = 0;
            for (int c = 0; c < maxCol; c++)
            {
                if (!string.IsNullOrWhiteSpace(grid[r, c])) nonEmptyCols++;
            }
            if (nonEmptyCols > 0 && nonEmptyCols <= maxCol / 2 + 1)
            {
                for (int c = 0; c < maxCol; c++)
                {
                    if (!string.IsNullOrWhiteSpace(grid[r, c]) && string.IsNullOrWhiteSpace(grid[0, c]))
                    {
                        grid[0, c] = grid[r, c].Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(grid[r, c]) && !string.IsNullOrWhiteSpace(grid[0, c]))
                    {
                        grid[0, c] = (grid[0, c].Trim() + grid[r, c].Trim());
                    }
                }
                headerEndRow = r;
            }
            else
            {
                break;
            }
        }

        var header = new List<string>();
        for (int c = 0; c < maxCol; c++)
        {
            header.Add(grid[0, c].Trim());
        }
        content.AppendLine("| " + string.Join(" | ", header) + " |");
        content.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", maxCol)) + " |");

        // 数据行
        for (int r = headerEndRow + 1; r < tableRows.Count; r++)
        {
            var row = new List<string>();
            for (int c = 0; c < maxCol; c++)
            {
                row.Add(grid[r, c].Trim());
            }
            content.AppendLine("| " + string.Join(" | ", row) + " |");
        }
        content.AppendLine();
    }

    #endregion
}
