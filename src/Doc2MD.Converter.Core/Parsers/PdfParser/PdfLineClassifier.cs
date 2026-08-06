using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace Doc2MD.Parsers;

/// <summary>
/// PDF 行分类和标题检测逻辑
/// </summary>
internal class PdfLineClassifier
{
    #region 行信息结构

    /// <summary>
    /// 行信息
    /// </summary>
    internal class LineInfo
    {
        public string Text { get; set; } = "";
        public double LeftMargin { get; set; }
        public double TopPosition { get; set; }
        public double FontSize { get; set; }
        public bool IsBold { get; set; }
        public LineType Type { get; set; } = LineType.BodyText;
        public int HeadingLevel { get; set; }
        public int ListNumber { get; set; } = -1;
        public bool Merged { get; set; }  // 已被合并到前一行，跳过输出
        public bool IsPageBreak { get; set; }  // 是否为页边界行
        public List<TableCell>? TableCells { get; set; }  // 表格行的单元格列表
    }

    /// <summary>
    /// 表格单元格
    /// </summary>
    internal class TableCell
    {
        public string Text { get; set; } = "";
        public double Left { get; set; }
        public double Right { get; set; }
        public int ColumnIndex { get; set; }  // 所在列号（全局列分配后赋值）
    }

    /// <summary>
    /// 行类型
    /// </summary>
    internal enum LineType
    {
        BodyText,
        Heading,
        OrderedList,
        UnorderedList,
        Empty,
        Table
    }

    #endregion

    #region 行分类

    /// <summary>
    /// 构建行信息列表
    /// </summary>
    internal List<LineInfo> BuildLineInfos(List<List<Word>> rawLines)
    {
        var result = new List<LineInfo>();

        foreach (var line in rawLines)
        {
            if (line.Count == 0) continue;

            var text = MergeWordsWithGaps(line);
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(new LineInfo { Type = LineType.Empty });
                continue;
            }

            text = PdfStyles.RemovePageArtifacts(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(new LineInfo { Type = LineType.Empty });
                continue;
            }

            text = PdfStyles.NormalizeSpaces(text);

            var leftMargin = line.Min(w => w.BoundingBox.Left);
            var topPosition = line.Average(w => w.BoundingBox.Top);
            var fontSize = line.Average(w => PdfStyles.GetFontSize(w));
            var isBold = line.Any(w => PdfStyles.IsBold(w));

            result.Add(new LineInfo
            {
                Text = text,
                LeftMargin = leftMargin,
                TopPosition = topPosition,
                FontSize = fontSize,
                IsBold = isBold,
                Type = LineType.BodyText
            });
        }

        return result;
    }

    private string MergeWordsWithGaps(List<Word> lineWords)
    {
        if (lineWords.Count == 0) return "";

        var result = new StringBuilder();
        result.Append(lineWords[0].Text);

        if (lineWords.Count == 1) return result.ToString();

        for (int i = 1; i < lineWords.Count; i++)
        {
            var currentWord = lineWords[i];
            var prevWord = lineWords[i - 1];

            bool prevHasCJK = prevWord.Text.Any(c => PdfStyles.IsCJK(c));
            bool currHasCJK = currentWord.Text.Any(c => PdfStyles.IsCJK(c));

            if (prevHasCJK && currHasCJK)
            {
                result.Append(currentWord.Text);
            }
            else
            {
                double gap = currentWord.BoundingBox.Left - prevWord.BoundingBox.Right;
                double avgCharWidth = prevWord.Text.Length > 0
                    ? prevWord.BoundingBox.Width / prevWord.Text.Length
                    : 6;
                double normalizedGap = avgCharWidth > 0 ? gap / avgCharWidth : 1;

                if (normalizedGap <= 0.5)
                {
                    result.Append(currentWord.Text);
                }
                else
                {
                    result.Append(' ');
                    result.Append(currentWord.Text);
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 分类每行
    /// </summary>
    internal void ClassifyLines(List<LineInfo> lines)
    {
        double? previousFontSize = null;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Type == LineType.Empty || line.Type == LineType.Table) continue;

            var text = line.Text;
            var (lineType, headingLevel, listNumber) = ClassifyLine(text, line.FontSize, line.IsBold, previousFontSize);
            line.Type = lineType;
            line.HeadingLevel = headingLevel;
            line.ListNumber = listNumber;

            previousFontSize = line.FontSize;
        }
    }

    private (LineType type, int headingLevel, int listNumber) ClassifyLine(
        string text, double fontSize, bool isBold, double? previousFontSize)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return (LineType.Empty, 0, -1);

        // 第X章/节 → 标题 H3
        if (Regex.IsMatch(text, @"^第[一二三四五六七八九十百千零\d]+[章节]"))
        {
            return (LineType.Heading, 3, -1);
        }

        // 第X条 → 标题 H4
        if (Regex.IsMatch(text, @"^第[一二三四五六七八九十百千零\d]+条"))
        {
            return (LineType.Heading, 4, -1);
        }

        // 一、二、→ 标题 H4（中文序号段标题）
        if (Regex.IsMatch(text, @"^[一二三四五六七八九十]+[、]"))
        {
            return (LineType.Heading, 4, -1);
        }

        // （一）（二）→ 有序列表（子条款，不是H5标题）
        if (Regex.IsMatch(text, @"^（[一二三四五六七八九十零]+）"))
        {
            var chineseNumMatch = Regex.Match(text, @"^（([一二三四五六七八九十零]+)）");
            int num = PdfStyles.ChineseNumToInt(chineseNumMatch.Groups[1].Value);
            return (LineType.OrderedList, 0, num);
        }

        // （1）（2）→ 有序列表
        var match = Regex.Match(text, @"^（(\d+)）\s*");
        if (match.Success)
        {
            var num = int.TryParse(match.Groups[1].Value, out var n) ? n : 1;
            return (LineType.OrderedList, 0, num);
        }

        // 1. 2. → 有序列表（排除小数）
        match = Regex.Match(text, @"^(\d+)[.．、]\s*");
        if (match.Success)
        {
            var afterDot = text.Substring(match.Length);
            if (!Regex.IsMatch(text, @"^\d+[.．]\d"))
            {
                var num = int.TryParse(match.Groups[1].Value, out var n) ? n : 1;
                return (LineType.OrderedList, 0, num);
            }
        }

        // 项目符号 •●○◆▪→ 无序列表
        if (Regex.IsMatch(text, @"^[•●○◆▪]\s*"))
        {
            return (LineType.UnorderedList, 0, -1);
        }

        // 短粗体行 → 标题 H4
        if (isBold && text.Length <= 30)
        {
            return (LineType.Heading, 4, -1);
        }

        // 字号显著大于上一行 → 标题 H4
        if (text.Length < 30 && previousFontSize.HasValue &&
            fontSize > previousFontSize.Value * 1.3)
        {
            return (LineType.Heading, 4, -1);
        }

        return (LineType.BodyText, 0, -1);
    }

    #endregion

    #region 标题跨行合并

    /// <summary>
    /// 合并标题跨行：PDF中标题经常被折行，需要将连续同类标题行合并
    /// </summary>
    internal void MergeHeadingLines(List<LineInfo> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Type != LineType.Heading || lines[i].Merged) continue;

            var current = lines[i];
            var currentPrefix = ExtractHeadingPrefix(current.Text);
            int j = i + 1;
            bool mergedAny = false;

            while (j < lines.Count)
            {
                var next = lines[j];

                // 跳过空行继续查找（但最多跳过1个空行）
                if (next.Type == LineType.Empty)
                {
                    if (mergedAny)
                        break;
                    j++;
                    continue;
                }

                // 如果下一行是另一个条款标题，不合并
                if (next.Type == LineType.Heading)
                {
                    var nextPrefix = ExtractHeadingPrefix(next.Text);
                    if (!string.IsNullOrEmpty(nextPrefix))
                    {
                        break;
                    }
                    if (next.HeadingLevel == current.HeadingLevel)
                    {
                        if (!ContainsNewHeadingMark(next.Text, currentPrefix))
                        {
                            MergeText(current, next);
                            next.Merged = true;
                            j++;
                            mergedAny = true;
                            continue;
                        }
                    }
                    break;
                }

                // 如果下一行是正文且不以新段落标记开头，可能是标题的续行
                if (next.Type == LineType.BodyText && !IsNewParagraphStart(next.Text))
                {
                    if (current.FontSize > 0 && next.FontSize > 0 &&
                        Math.Abs(current.FontSize - next.FontSize) / current.FontSize > 0.15)
                    {
                        break;
                    }

                    if (!ContainsNewHeadingMark(next.Text, currentPrefix))
                    {
                        MergeText(current, next);
                        next.Merged = true;
                        j++;
                        mergedAny = true;
                        continue;
                    }
                }

                break;
            }
        }
    }

    /// <summary>
    /// 提取标题开头的条款前缀
    /// </summary>
    private string ExtractHeadingPrefix(string text)
    {
        var match = Regex.Match(text, @"^(第[一二三四五六七八九十百千零\d]+[章节条])");
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(text, @"^([一二三四五六七八九十]+[、])");
        if (match.Success) return match.Groups[1].Value;

        return "";
    }

    /// <summary>
    /// 检查文本中是否包含新的条款/章节标记
    /// </summary>
    private bool ContainsNewHeadingMark(string text, string currentPrefix)
    {
        var matches = Regex.Matches(text, @"第[一二三四五六七八九十百千零\d]+[章节条]");
        foreach (Match m in matches)
        {
            if (m.Value != currentPrefix)
                return true;
        }

        if (Regex.IsMatch(text, @"[一二三四五六七八九十]+[、]"))
            return true;

        return false;
    }

    /// <summary>
    /// 合并两段文本到 current
    /// </summary>
    internal void MergeText(LineInfo current, LineInfo next)
    {
        if (PdfStyles.EndsWithCJK(current.Text) || PdfStyles.StartsWithCJK(next.Text))
        {
            current.Text += next.Text;
        }
        else
        {
            current.Text += " " + next.Text;
        }
    }

    /// <summary>
    /// 判断文本是否像新段落的开头
    /// </summary>
    internal bool IsNewParagraphStart(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        text = text.TrimStart();

        if (Regex.IsMatch(text, @"^第[一二三四五六七八九十百千零\d]+")) return true;
        if (Regex.IsMatch(text, @"^[一二三四五六七八九十]+[、]")) return true;
        if (Regex.IsMatch(text, @"^（[一二三四五六七八九十零\d]+）")) return true;
        if (Regex.IsMatch(text, @"^\d+[.．、]\s")) return true;

        return false;
    }

    #endregion
}
