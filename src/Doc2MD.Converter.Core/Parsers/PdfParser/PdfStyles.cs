using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace Doc2MD.Parsers;

/// <summary>
/// PDF 样式处理和文本清洗方法
/// </summary>
internal static class PdfStyles
{
    /// <summary>
    /// 规范化空格
    /// </summary>
    internal static string NormalizeSpaces(string text)
    {
        text = Regex.Replace(text, @"\s+", " ");
        text = Regex.Replace(text, @"\s+([,，;:;:。!！?？）】」』】])", "$1");
        return text.Trim();
    }

    /// <summary>
    /// 移除 PDF 页面元素（页码、装饰线等）
    /// </summary>
    internal static string RemovePageArtifacts(string text)
    {
        text = Regex.Replace(text, @"—+\s*\d+\s*—+", "");
        text = Regex.Replace(text, @"第\s*\d+\s*页\s*—\s*\d+\s*—", "");
        text = Regex.Replace(text, @"第\s*\d+\s*页", "");
        text = Regex.Replace(text, @"共\s*\d+\s*页", "");
        text = Regex.Replace(text, @"^\s*\d{1,3}\s*$", "");
        text = Regex.Replace(text, @"^[\s\d—\-]+$", "");
        text = Regex.Replace(text, @"[­​‌‍﻿]", "");

        return text.Trim();
    }

    /// <summary>
    /// 判断文本是否以段落结束标点结尾
    /// </summary>
    internal static bool EndsWithParagraphMark(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var lastChar = text.TrimEnd().LastOrDefault();
        return lastChar == '。' || lastChar == '！' || lastChar == '？' ||
               lastChar == '.' || lastChar == '!' || lastChar == '?' ||
               lastChar == '」' || lastChar == '』' || lastChar == '】' ||
               lastChar == '…' || lastChar == '；' || lastChar == ';';
    }

    /// <summary>
    /// 判断文本是否以中日韩字符结尾
    /// </summary>
    internal static bool EndsWithCJK(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return IsCJK(text.TrimEnd().LastOrDefault());
    }

    /// <summary>
    /// 判断文本是否以中日韩字符开头
    /// </summary>
    internal static bool StartsWithCJK(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return IsCJK(text.TrimStart().FirstOrDefault());
    }

    /// <summary>
    /// 判断字符是否为中日韩统一表意文字
    /// </summary>
    internal static bool IsCJK(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FFF) ||
               (c >= 0x3400 && c <= 0x4DBF) ||
               (c >= 0xFF00 && c <= 0xFFEF) ||
               (c >= 0x3000 && c <= 0x303F) ||
               (c >= 0x3040 && c <= 0x309F) ||
               (c >= 0x30A0 && c <= 0x30FF);
    }

    /// <summary>
    /// 中文数字转阿拉伯数字（一~九十九）
    /// </summary>
    internal static int ChineseNumToInt(string chinese)
    {
        var map = new Dictionary<char, int>
        {
            {'一', 1}, {'二', 2}, {'三', 3}, {'四', 4}, {'五', 5},
            {'六', 6}, {'七', 7}, {'八', 8}, {'九', 9}, {'十', 10},
            {'零', 0}
        };

        if (chinese.Length == 1 && map.ContainsKey(chinese[0]))
            return map[chinese[0]];

        // 处理 "十一" ~ "十九"
        if (chinese.Length == 2 && chinese[0] == '十')
            return 10 + (map.ContainsKey(chinese[1]) ? map[chinese[1]] : 0);

        // 处理 "二十" ~ "九十九"
        if (chinese.Length == 2 && chinese[1] == '十')
            return (map.ContainsKey(chinese[0]) ? map[chinese[0]] : 0) * 10;

        // 处理 "二十一" ~ "九十九"
        if (chinese.Length == 3 && chinese[1] == '十')
            return (map.ContainsKey(chinese[0]) ? map[chinese[0]] : 0) * 10
                 + (map.ContainsKey(chinese[2]) ? map[chinese[2]] : 0);

        // 简单回退
        int result = 0;
        foreach (var c in chinese)
        {
            if (map.ContainsKey(c))
                result = result * 10 + map[c];
        }
        return result > 0 ? result : 1;
    }

    /// <summary>
    /// 从 Word 获取字体大小
    /// </summary>
    internal static double GetFontSize(Word word)
    {
        try
        {
            if (word.Letters.Count > 0)
            {
                var fs = word.Letters[0].FontSize;
                // PdfPig对某些PDF返回极小的FontSize(如1.0)，这种情况回退到BoundingBox.Height
                if (fs >= 5) return fs;
            }
            var bbox = word.BoundingBox;
            return bbox.Height > 0 ? bbox.Height : 10;
        }
        catch
        {
            return 10;
        }
    }

    /// <summary>
    /// 判断 Word 是否为粗体
    /// </summary>
    internal static bool IsBold(Word word)
    {
        try
        {
            if (word.Letters.Count > 0)
            {
                var fontName = word.Letters[0].FontName ?? "";
                var lower = fontName.ToLowerInvariant();
                return lower.Contains("bold") ||
                       lower.Contains("bd") ||
                       lower.Contains("black") ||
                       lower.Contains("heavy");
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
