using System.Text.RegularExpressions;

namespace Doc2MD.Services;

/// <summary>
/// AIGC 水印过滤器：识别并移除 AIGC 水印污染
///
/// 处理以下污染形式：
/// 1. YAML frontmatter 中的 AIGC 块（含多重 frontmatter）
/// 2. 正文中 AIGC: / AIGC标识: 前缀块
/// 3. 散布在正文中的 AIGC 元信息字段
/// 4. 裸露的 AIGC 标识行（AIGC标识: xxx）
/// 5. 独立的 UUID 行（AIGC 水印常见残留）
/// 6. 零宽字符水印（11 种不可见字符编码）
/// </summary>
public static class AigcWatermarkFilter
{
    /// <summary>AIGC 水印过滤结果</summary>
    public class FilterResult
    {
        public string Markdown { get; set; } = string.Empty;
        public bool HasWatermark { get; set; }
        public int RemovedBlocks { get; set; }
        public List<string> DetectedTypes { get; set; } = [];
    }

    // === AIGC frontmatter 识别 ===
    private static readonly Regex AigcFrontmatterRegex = new(
        @"\A---\s*\n(.*?)\n---",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly HashSet<string> AigcFrontmatterKeys =
    [
        "ContentProducer", "ContentPropagator", "ProduceID", "PropagateID",
        "ReservedCode", "Label"
    ];

    // === 正文 AIGC 块识别 ===
    private static readonly Regex AigcBlockRegex = new(
        @"^[ \t]*AIGC:[ \t]*\n(?:[ \t]+(?:ContentProducer|ContentPropagator|ProduceID|PropagateID|ReservedCode|Label)[^\n]*\n)*",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 匹配独立一行的 AIGC: 标记（包括 AIGC:UUID 形式）
    private static readonly Regex AigcStandaloneLineRegex = new(
        @"^[ \t]*AIGC:[ \t]*(?:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})?[ \t]*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 匹配散布的 AIGC 元信息行
    private static readonly Regex AigcMetaLineRegex = new(
        @"^[ \t]*(?:ContentProducer|ContentPropagator|ProduceID|PropagateID|ReservedCode\d*)[ \t]*[:=]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 匹配 "AIGC标识:" 行（中文标识形式）
    private static readonly Regex AigcLabelLineRegex = new(
        @"^[ \t]*AIGC标识[：:][ \t]*.*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 匹配独立的 UUID 行（AIGC 水印常见残留：行首缩进 + 纯 UUID）
    private static readonly Regex AigcUuidLineRegex = new(
        @"^[ \t]*'?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'?[ \t]*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 匹配正文中的 AIGC frontmatter 块（---\nAIGC:\n  ...\n---）
    private static readonly Regex AigcBodyFrontmatterRegex = new(
        @"^[ \t]*---\s*\n[ \t]*AIGC:[^\n]*\n(?:[ \t]+(?:ContentProducer|ContentPropagator|ProduceID|PropagateID|ReservedCode\d*|Label)[^\n]*\n)*[ \t]*---",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // === 零宽字符水印识别 ===
    // U+200B (ZWSP), U+200C (ZWNJ), U+200D (ZWJ), U+200E (LRM), U+200F (RLM),
    // U+FEFF (BOM/ZWNBSP), U+2060 (WJ), U+2061-2063 (Invisible operators),
    // U+180E (MVS)
    private static readonly HashSet<char> ZeroWidthChars = new()
    {
        '\u200B', '\u200C', '\u200D', '\u200E', '\u200F',
        '\uFEFF', '\u2060', '\u2061', '\u2062', '\u2063',
        '\u180E'
    };

    private const double ZeroWidthDensityThreshold = 0.005; // 0.5%
    private const int MinZeroWidthCount = 10;

    // === 残留检测 ===
    private static readonly Regex AigcResidualRegex = new(
        @"(?:ContentProducer|ContentPropagator|ProduceID|PropagateID|ReservedCode)",
        RegexOptions.Compiled);

    /// <summary>
    /// 过滤 Markdown 中的 AIGC 水印
    /// </summary>
    public static FilterResult Filter(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new FilterResult { Markdown = markdown };

        var result = new FilterResult();
        var content = markdown;

        // 1. 处理 YAML frontmatter 中的 AIGC 块（多次扫描，处理多重 frontmatter）
        content = FilterAigcFrontmatterIterative(content, result);

        // 2. 处理正文中的 AIGC frontmatter 块
        content = FilterAigcBodyFrontmatter(content, result);

        // 3. 处理正文中的 AIGC 块
        content = FilterAigcBlocks(content, result);

        // 4. 处理散布的 AIGC 元信息行
        content = FilterAigcMetaLines(content, result);

        // 5. 处理 "AIGC标识:" 行
        content = FilterAigcLabelLines(content, result);

        // 6. 处理独立的 UUID 行（仅当已检测到 AIGC 水印时，避免误判合法 UUID）
        content = FilterAigcUuidLines(content, result);

        // 7. 检测并过滤零宽字符水印
        content = FilterZeroWidthChars(content, result);

        result.Markdown = content;
        result.HasWatermark = result.RemovedBlocks > 0;

        return result;
    }

    /// <summary>
    /// 检测 Markdown 中是否仍含 AIGC 水印残留
    /// </summary>
    public static bool HasResidual(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return false;

        // 检测零宽字符残留
        if (DetectZeroWidthWatermark(markdown).IsWatermark)
            return true;

        // 检测 frontmatter 中的残留
        var fmMatch = AigcFrontmatterRegex.Match(markdown);
        if (fmMatch.Success)
        {
            var fmContent = fmMatch.Groups[1].Value;
            if (fmContent.Contains("AIGC:") || AigcResidualRegex.IsMatch(fmContent))
                return true;
        }

        // 检测正文中的残留（排除注释行）
        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("<!--")) continue;
            if (AigcResidualRegex.IsMatch(trimmed)) return true;
            if (trimmed.StartsWith("AIGC:")) return true;
            if (trimmed.StartsWith("AIGC标识:") || trimmed.StartsWith("AIGC标识：")) return true;
        }

        return false;
    }

    // === 零宽字符水印检测与过滤 ===

    /// <summary>
    /// 零宽字符检测结果
    /// </summary>
    public class ZeroWidthDetectionResult
    {
        public bool IsWatermark { get; set; }
        public int ZeroWidthCount { get; set; }
        public int VisibleCharCount { get; set; }
        public double Density { get; set; }
        public Dictionary<char, int> CharBreakdown { get; set; } = [];
    }

    /// <summary>
    /// 检测文本中的零宽字符是否构成 AIGC 水印
    /// </summary>
    public static ZeroWidthDetectionResult DetectZeroWidthWatermark(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new ZeroWidthDetectionResult();

        int zwCount = 0;
        int visibleCount = 0;
        var breakdown = new Dictionary<char, int>();

        foreach (var c in text)
        {
            if (ZeroWidthChars.Contains(c))
            {
                zwCount++;
                if (!breakdown.ContainsKey(c))
                    breakdown[c] = 0;
                breakdown[c]++;
            }
            else if (!char.IsWhiteSpace(c))
            {
                visibleCount++;
            }
        }

        int denominator = zwCount + visibleCount;
        double density = denominator > 0 ? (double)zwCount / denominator : 0;
        bool isWatermark = zwCount >= MinZeroWidthCount && density >= ZeroWidthDensityThreshold;

        return new ZeroWidthDetectionResult
        {
            IsWatermark = isWatermark,
            ZeroWidthCount = zwCount,
            VisibleCharCount = visibleCount,
            Density = density,
            CharBreakdown = breakdown
        };
    }

    // === 内部实现 ===

    private static string FilterAigcFrontmatterIterative(string markdown, FilterResult result)
    {
        var content = markdown;
        int prevLength;
        int maxIterations = 5;

        do
        {
            prevLength = content.Length;
            content = FilterAigcFrontmatter(content, result);
            maxIterations--;
        }
        while (content.Length != prevLength && maxIterations > 0);

        return content;
    }

    private static string FilterAigcBodyFrontmatter(string markdown, FilterResult result)
    {
        var content = AigcBodyFrontmatterRegex.Replace(markdown, m =>
        {
            result.RemovedBlocks++;
            result.DetectedTypes.Add("aigc_body_frontmatter");
            return "";
        });

        content = Regex.Replace(content, @"\n{3,}", "\n\n");
        return content;
    }

    private static string FilterAigcLabelLines(string markdown, FilterResult result)
    {
        var content = AigcLabelLineRegex.Replace(markdown, m =>
        {
            result.RemovedBlocks++;
            result.DetectedTypes.Add("aigc_label_line");
            return "";
        });

        content = Regex.Replace(content, @"\n{3,}", "\n\n");
        return content;
    }

    private static string FilterAigcUuidLines(string markdown, FilterResult result)
    {
        // 仅当已检测到 AIGC 水印时才过滤 UUID 行（避免误判合法 UUID 内容）
        if (result.DetectedTypes.Count == 0) return markdown;

        var content = AigcUuidLineRegex.Replace(markdown, m =>
        {
            result.RemovedBlocks++;
            result.DetectedTypes.Add("aigc_uuid_line");
            return "";
        });

        content = Regex.Replace(content, @"\n{3,}", "\n\n");
        return content;
    }

    private static string FilterAigcFrontmatter(string markdown, FilterResult result)
    {
        var match = AigcFrontmatterRegex.Match(markdown);
        if (!match.Success) return markdown;

        var fmContent = match.Groups[1].Value;

        if (!fmContent.Contains("AIGC:") && !AigcResidualRegex.IsMatch(fmContent))
            return markdown;

        var fmLines = fmContent.Split('\n');
        var filteredLines = new List<string>();
        bool inAigcBlock = false;

        foreach (var line in fmLines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("AIGC:") || trimmed == "AIGC")
            {
                inAigcBlock = true;
                result.DetectedTypes.Add("frontmatter_aigc_block");
                continue;
            }

            if (inAigcBlock)
            {
                if (line.StartsWith("  ") || line.StartsWith("\t") || string.IsNullOrEmpty(trimmed))
                {
                    if (string.IsNullOrEmpty(trimmed))
                        inAigcBlock = false;
                    continue;
                }
                inAigcBlock = false;
            }

            bool isAigcKey = false;
            foreach (var key in AigcFrontmatterKeys)
            {
                if (trimmed.StartsWith(key + ":") || trimmed.StartsWith(key + " ="))
                {
                    isAigcKey = true;
                    result.DetectedTypes.Add($"frontmatter_{key.ToLower()}");
                    break;
                }
            }

            if (!isAigcKey)
                filteredLines.Add(line);
        }

        if (result.DetectedTypes.Count > 0)
        {
            result.RemovedBlocks++;

            var newFm = string.Join("\n", filteredLines).Trim();

            if (newFm.Length == 0)
            {
                var afterFm = markdown.Substring(match.Index + match.Length);
                if (afterFm.StartsWith("\n"))
                    afterFm = afterFm.Substring(1);
                return afterFm;
            }

            return $"---\n{newFm}\n---" + markdown.Substring(match.Index + match.Length);
        }

        return markdown;
    }

    private static string FilterAigcBlocks(string markdown, FilterResult result)
    {
        var content = AigcBlockRegex.Replace(markdown, m =>
        {
            result.RemovedBlocks++;
            result.DetectedTypes.Add("aigc_block");
            return "";
        });

        content = AigcStandaloneLineRegex.Replace(content, m =>
        {
            result.RemovedBlocks++;
            result.DetectedTypes.Add("aigc_standalone");
            return "";
        });

        content = Regex.Replace(content, @"\n{3,}", "\n\n");

        return content;
    }

    private static string FilterAigcMetaLines(string markdown, FilterResult result)
    {
        var content = AigcMetaLineRegex.Replace(markdown, m =>
        {
            result.RemovedBlocks++;
            result.DetectedTypes.Add("aigc_meta_line");
            return "";
        });

        content = Regex.Replace(content, @"\n{3,}", "\n\n");

        return content;
    }

    private static string FilterZeroWidthChars(string markdown, FilterResult result)
    {
        var detection = DetectZeroWidthWatermark(markdown);
        if (!detection.IsWatermark)
            return markdown;

        var sb = new System.Text.StringBuilder(markdown.Length);
        foreach (var c in markdown)
        {
            if (!ZeroWidthChars.Contains(c))
                sb.Append(c);
        }

        result.RemovedBlocks++;
        result.DetectedTypes.Add("aigc_zero_width_chars");

        return sb.ToString();
    }
}
