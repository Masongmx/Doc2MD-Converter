using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Doc2MD.Parsers;

/// <summary>
/// PDF 表格检测逻辑
/// </summary>
internal class PdfTableDetector
{
    #region 表格相关类

    /// <summary>
    /// Word 簇（同一单元格的多个 word）
    /// </summary>
    internal class WordCluster
    {
        public string Text { get; set; } = "";
        public double Left { get; set; }
        public double Right { get; set; }
        public double Top { get; set; }
    }

    /// <summary>
    /// 行簇（包含多个列簇的一行）
    /// </summary>
    internal class RowCluster
    {
        public int LineIndex { get; set; }
        public List<WordCluster> Clusters { get; set; } = new();
        public double Y { get; set; }
        public double LeftMost { get; set; }
        public double RightMost { get; set; }
    }

    /// <summary>
    /// 表格区域
    /// </summary>
    internal class TableRegion
    {
        public List<RowCluster> Rows { get; set; } = new();
    }

    #endregion

    #region 表格检测

    /// <summary>
    /// 检测页面中的表格区域
    /// </summary>
    internal void DetectTables(List<Word> pageWords, List<UglyToad.PdfPig.Graphics.PdfPath> pagePaths, List<PdfLineClassifier.LineInfo> lineInfos)
    {
        if (pageWords.Count == 0 || lineInfos.Count == 0) return;

        // 计算 median fontSize 用于宽松行聚类
        var fontSizes = pageWords.Select(w => PdfStyles.GetFontSize(w)).Where(f => f > 0).ToList();
        double medianFontSize = fontSizes.Count > 0
            ? fontSizes.OrderBy(f => f).ElementAt(fontSizes.Count / 2)
            : 12;

        // 用宽松 Y 阈值重新把 words 组成行
        double lineThreshold = Math.Max(medianFontSize * 1.0, 10);
        var looseLines = GroupWordsIntoLinesLoose(pageWords, lineThreshold);

        // 分析每行的 word 簇
        var rowClusters = new List<RowCluster>();
        for (int i = 0; i < looseLines.Count; i++)
        {
            var line = looseLines[i];
            if (line.Count == 0) continue;

            var clusters = ClusterWordsByGap(line);
            if (clusters.Count >= 2)
            {
                rowClusters.Add(new RowCluster
                {
                    LineIndex = i,
                    Clusters = clusters,
                    Y = line.Average(w => w.BoundingBox.Top),
                    LeftMost = clusters.Min(c => c.Left),
                    RightMost = clusters.Max(c => c.Right)
                });
            }
        }

        if (rowClusters.Count < 2) return;

        // 将 Y 值接近的多列行分组为连续表格区域
        var tableRegions = GroupIntoTableRegions(rowClusters, medianFontSize, pagePaths.Count > 5);

        // 如果 page 有 Path 线条，放宽标准
        int minRows = pagePaths.Count > 5 ? 2 : 3;
        tableRegions = tableRegions.Where(r => r.Rows.Count >= minRows).ToList();

        if (tableRegions.Count == 0) return;

        // 将检测到的表格区域映射回 lineInfos
        foreach (var region in tableRegions)
        {
            MarkTableRegionInLineInfos(region, looseLines, lineInfos, medianFontSize);
        }
    }

    /// <summary>
    /// 用宽松 Y 阈值将 words 组成行
    /// </summary>
    private List<List<Word>> GroupWordsIntoLinesLoose(List<Word> words, double lineThreshold)
    {
        if (words.Count == 0) return new List<List<Word>>();

        var sorted = words.OrderBy(w => -w.BoundingBox.Top)
                         .ThenBy(w => w.BoundingBox.Left)
                         .ToList();

        var lines = new List<List<Word>>();
        var currentLine = new List<Word> { sorted[0] };
        double lineY = sorted[0].BoundingBox.Top;

        for (int i = 1; i < sorted.Count; i++)
        {
            var wordY = sorted[i].BoundingBox.Top;

            if (Math.Abs(wordY - lineY) <= lineThreshold)
            {
                currentLine.Add(sorted[i]);
            }
            else
            {
                if (currentLine.Count > 0)
                    lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());
                currentLine = new List<Word> { sorted[i] };
                lineY = wordY;
            }
        }

        if (currentLine.Count > 0)
            lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());

        return lines;
    }

    /// <summary>
    /// 将一行中的 words 按水平间隔聚类为单元格簇
    /// </summary>
    private List<WordCluster> ClusterWordsByGap(List<Word> lineWords)
    {
        if (lineWords.Count <= 1)
            return new List<WordCluster>();

        var sorted = lineWords.OrderBy(w => w.BoundingBox.Left).ToList();
        var clusters = new List<WordCluster>();
        var current = new List<Word> { sorted[0] };

        double avgCharWidth = sorted.Select(w =>
        {
            int len = w.Text.Length;
            return len > 0 ? w.BoundingBox.Width / len : 6;
        }).Average();

        double gapThreshold = Math.Max(avgCharWidth * 3, 15);

        for (int i = 1; i < sorted.Count; i++)
        {
            var prevRight = current.Last().BoundingBox.Right;
            var currLeft = sorted[i].BoundingBox.Left;
            double gap = currLeft - prevRight;

            if (gap > gapThreshold)
            {
                clusters.Add(MakeCluster(current));
                current = new List<Word> { sorted[i] };
            }
            else
            {
                current.Add(sorted[i]);
            }
        }
        clusters.Add(MakeCluster(current));

        return clusters;
    }

    private WordCluster MakeCluster(List<Word> words)
    {
        var text = string.Join("", words.Select(w => w.Text));
        return new WordCluster
        {
            Text = text.Trim(),
            Left = words.Min(w => w.BoundingBox.Left),
            Right = words.Max(w => w.BoundingBox.Right),
            Top = words.Min(w => w.BoundingBox.Top)
        };
    }

    /// <summary>
    /// 将多列行按 Y 接近分组为连续表格区域
    /// </summary>
    private List<TableRegion> GroupIntoTableRegions(List<RowCluster> rowClusters, double medianFontSize, bool hasTablePaths)
    {
        double normalLineSpacing = medianFontSize * 1.5;
        double maxRowGap = hasTablePaths ? normalLineSpacing * 4.0 : normalLineSpacing * 2.5;
        var regions = new List<TableRegion>();
        var currentRegion = new TableRegion { Rows = new List<RowCluster> { rowClusters[0] } };

        for (int i = 1; i < rowClusters.Count; i++)
        {
            var prevY = rowClusters[i - 1].Y;
            var currY = rowClusters[i].Y;
            double gap = prevY - currY;

            if (gap > maxRowGap)
            {
                if (currentRegion.Rows.Count >= 2)
                    regions.Add(currentRegion);
                currentRegion = new TableRegion { Rows = new List<RowCluster> { rowClusters[i] } };
            }
            else
            {
                currentRegion.Rows.Add(rowClusters[i]);
            }
        }
        if (currentRegion.Rows.Count >= 2)
            regions.Add(currentRegion);

        return regions.Where(r => r.Rows.Count >= 3).ToList();
    }

    /// <summary>
    /// 根据列边界将一行 words 分配到各列
    /// </summary>
    private List<PdfLineClassifier.TableCell> AssignWordsToColumns(List<Word> words, List<double> columnBoundaries)
    {
        if (words.Count == 0 || columnBoundaries.Count == 0)
            return new List<PdfLineClassifier.TableCell>();

        var splits = new List<double>();
        for (int i = 0; i < columnBoundaries.Count - 1; i++)
        {
            splits.Add((columnBoundaries[i] + columnBoundaries[i + 1]) / 2.0);
        }

        var cells = new List<PdfLineClassifier.TableCell>();
        var currentColWords = new List<Word>();
        int currentCol = 0;

        foreach (var word in words.OrderBy(w => w.BoundingBox.Left))
        {
            double wordCenter = (word.BoundingBox.Left + word.BoundingBox.Right) / 2.0;

            int targetCol = 0;
            for (int s = 0; s < splits.Count; s++)
            {
                if (wordCenter > splits[s])
                    targetCol = s + 1;
                else
                    break;
            }

            targetCol = Math.Min(targetCol, columnBoundaries.Count - 1);

            if (targetCol != currentCol && currentColWords.Count > 0)
            {
                cells.Add(MakeCellFromWords(currentColWords, currentCol));
                currentColWords = new List<Word>();
            }
            currentCol = targetCol;
            currentColWords.Add(word);
        }

        if (currentColWords.Count > 0)
            cells.Add(MakeCellFromWords(currentColWords, currentCol));

        return cells;
    }

    private PdfLineClassifier.TableCell MakeCellFromWords(List<Word> words, int colIndex)
    {
        return new PdfLineClassifier.TableCell
        {
            Text = string.Join("", words.Select(w => w.Text)).Trim(),
            Left = words.Min(w => w.BoundingBox.Left),
            Right = words.Max(w => w.BoundingBox.Right),
            ColumnIndex = colIndex
        };
    }

    /// <summary>
    /// 将检测到的表格区域映射回 lineInfos
    /// </summary>
    private void MarkTableRegionInLineInfos(TableRegion region, List<List<Word>> looseLines, List<PdfLineClassifier.LineInfo> lineInfos, double medianFontSize)
    {
        // 从数据行确定列边界
        var allLefts = region.Rows.SelectMany(r => r.Clusters).Select(c => c.Left).ToList();
        var columnBoundaries = ClusterPositions(allLefts, 20);

        if (columnBoundaries.Count < 2) return;

        // 确定表格区域的 Y 范围
        double regionTop = region.Rows.Max(r => r.Y) + medianFontSize * 1.0;
        double regionBottom = region.Rows.Min(r => r.Y) - medianFontSize * 1.0;

        // 对所有在表格 Y 范围内的宽松行，用列边界重新分配 words 到各列
        var tableLooseLines = new List<(int looseLineIdx, List<PdfLineClassifier.TableCell> cells, double Y)>();
        for (int li = 0; li < looseLines.Count; li++)
        {
            var ll = looseLines[li];
            if (ll.Count == 0) continue;
            double y = ll.Average(w => w.BoundingBox.Top);
            if (y >= regionBottom && y <= regionTop)
            {
                var cells = AssignWordsToColumns(ll, columnBoundaries);
                if (cells.Any(c => !string.IsNullOrWhiteSpace(c.Text)))
                {
                    tableLooseLines.Add((li, cells, y));
                }
            }
        }

        if (tableLooseLines.Count < 2) return;

        // 质量检查：多数行应该有2个以上非空cell
        int rowsWithMultipleCells = tableLooseLines.Count(t => t.cells.Count(c => !string.IsNullOrWhiteSpace(c.Text)) >= 2);
        if ((double)rowsWithMultipleCells / tableLooseLines.Count < 0.4) return;

        var usedLineIndices = new HashSet<int>();

        foreach (var (looseLineIdx, cells, y) in tableLooseLines)
        {
            double rowTop = y + medianFontSize * 0.6;
            double rowBottom = y - medianFontSize * 0.6;

            var matchingIndices = new List<int>();
            for (int i = 0; i < lineInfos.Count; i++)
            {
                if (usedLineIndices.Contains(i)) continue;
                var info = lineInfos[i];
                if (info.Merged || info.Type == PdfLineClassifier.LineType.Empty) continue;
                if (info.TopPosition >= rowBottom && info.TopPosition <= rowTop)
                {
                    matchingIndices.Add(i);
                }
            }

            if (matchingIndices.Count == 0) continue;

            var firstIdx = matchingIndices[0];
            lineInfos[firstIdx].Type = PdfLineClassifier.LineType.Table;
            lineInfos[firstIdx].TableCells = cells;
            lineInfos[firstIdx].Text = string.Join(" ", cells.Where(c => !string.IsNullOrWhiteSpace(c.Text)).Select(c => c.Text));
            usedLineIndices.Add(firstIdx);

            for (int j = 1; j < matchingIndices.Count; j++)
            {
                lineInfos[matchingIndices[j]].Merged = true;
                usedLineIndices.Add(matchingIndices[j]);
            }
        }

        // 处理表格区域内的非多列行
        for (int i = 0; i < lineInfos.Count; i++)
        {
            var info = lineInfos[i];
            if (info.Merged || info.Type == PdfLineClassifier.LineType.Table || info.Type == PdfLineClassifier.LineType.Empty) continue;
            if (usedLineIndices.Contains(i)) continue;
            if (info.TopPosition >= regionBottom && info.TopPosition <= regionTop)
            {
                if (info.Type == PdfLineClassifier.LineType.BodyText && info.Text.Length < 60 && !PdfStyles.EndsWithParagraphMark(info.Text))
                {
                    info.Type = PdfLineClassifier.LineType.Table;
                    info.TableCells = new List<PdfLineClassifier.TableCell>
                    {
                        new PdfLineClassifier.TableCell { Text = info.Text, Left = info.LeftMargin, ColumnIndex = 0 }
                    };
                    usedLineIndices.Add(i);
                }
            }
        }
    }

    /// <summary>
    /// 将一组位置值聚类为列边界（层次聚类）
    /// </summary>
    private List<double> ClusterPositions(List<double> positions, double tolerance)
    {
        if (positions.Count == 0) return new List<double>();

        var sorted = positions.OrderBy(p => p).ToList();
        var clusters = new List<List<double>> { new List<double> { sorted[0] } };

        for (int i = 1; i < sorted.Count; i++)
        {
            var currentCluster = clusters.Last();
            var clusterAvg = currentCluster.Average();
            if (Math.Abs(sorted[i] - clusterAvg) <= tolerance)
            {
                currentCluster.Add(sorted[i]);
            }
            else
            {
                clusters.Add(new List<double> { sorted[i] });
            }
        }

        return clusters.Select(c => c.Average()).OrderBy(x => x).ToList();
    }

    private int FindNearestColumn(List<double> columnBoundaries, double left)
    {
        int best = 0;
        double bestDist = double.MaxValue;
        for (int i = 0; i < columnBoundaries.Count; i++)
        {
            var dist = Math.Abs(left - columnBoundaries[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
    }

    #endregion
}
