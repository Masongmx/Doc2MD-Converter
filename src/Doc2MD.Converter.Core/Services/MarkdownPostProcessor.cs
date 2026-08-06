using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 统一后处理器：在 Parser 产出的原始 Markdown 上注入
/// frontmatter、AI_AGENT_NOTICE、block_id、source markers 等。
/// </summary>
public static class MarkdownPostProcessor
{
    /// <summary>
    /// 对原始 Markdown 执行后处理，返回加工后的 Markdown 以及 block 统计。
    /// </summary>
    public static PostProcessResult Process(string rawMarkdown, ConversionResult conversionResult)
    {
        if (string.IsNullOrWhiteSpace(rawMarkdown))
            return new PostProcessResult { Markdown = rawMarkdown, BlockCount = 0, UnsupportedObjectCount = 0 };

        // 0. AIGC 水印过滤（在后处理前清除水印污染）
        var aigcResult = AigcWatermarkFilter.Filter(rawMarkdown);
        if (aigcResult.HasWatermark)
        {
            conversionResult.Warnings.Add(ConversionWarning.Create(
                "W_AIGC_WATERMARK",
                $"检测到 AIGC 水印污染，已过滤 {aigcResult.RemovedBlocks} 处（类型: {string.Join(", ", aigcResult.DetectedTypes.Distinct())}）",
                "全文"));
        }
        var cleanMarkdown = aigcResult.Markdown;

        // 0.5 公文元数据提取（v2.0 新增）
        var govMeta = GovMetadataExtractor.Extract(cleanMarkdown, conversionResult.SourceFileName);
        conversionResult.GovMetadata = govMeta;

        var sb = new StringBuilder();

        // 1. 注入 frontmatter（YAML 格式，含公文元数据）
        AppendFrontmatter(sb, conversionResult, govMeta);

        // 2. 注入 AI_AGENT_NOTICE
        AppendAgentNotice(sb, conversionResult);

        // 3. 对内容注入 block_id 和 source markers
        var content = cleanMarkdown;
        int blockCount = 0;
        int unsupportedCount = 0;

        content = InjectBlockIds(content, ref blockCount);
        unsupportedCount = CountUnsupportedObjects(content);

        sb.Append(content);

        // 4. 保存处理后 Markdown
        conversionResult.ProcessedMarkdown = sb.ToString();

        // 5. 统计
        return new PostProcessResult
        {
            Markdown = sb.ToString(),
            BlockCount = blockCount,
            UnsupportedObjectCount = unsupportedCount
        };
    }

    /// <summary>
    /// 生成文件 SHA-256 哈希
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    #region Frontmatter

    private static void AppendFrontmatter(StringBuilder sb, ConversionResult result, GovMetadata? govMeta)
    {
        sb.AppendLine("---");

        if (!string.IsNullOrEmpty(result.SourceFilePath))
            sb.AppendLine($"source_file: \"{EscapeYaml(Path.GetFileName(result.SourceFilePath))}\"");

        if (!string.IsNullOrEmpty(result.SourceType))
            sb.AppendLine($"source_type: \"{result.SourceType}\"");

        if (result.SourceFileSize > 0)
            sb.AppendLine($"source_size: {result.SourceFileSize}");

        if (!string.IsNullOrEmpty(result.SourceFileHashSha256))
            sb.AppendLine($"source_sha256: \"{result.SourceFileHashSha256}\"");

        if (result.PageCount > 0)
            sb.AppendLine($"page_count: {result.PageCount}");

        if (result.SheetCount > 0)
            sb.AppendLine($"sheet_count: {result.SheetCount}");

        if (result.SlideCount > 0)
            sb.AppendLine($"slide_count: {result.SlideCount}");

        sb.AppendLine($"ocr_used: {result.OcrUsed.ToString().ToLowerInvariant()}");
        sb.AppendLine($"converted_at: \"{DateTimeOffset.Now:O}\"");
        sb.AppendLine($"converter: \"{Doc2MD.Constants.AppVersion.Converter}\"");

        // 公文元数据（v2.0 新增）
        if (govMeta != null)
        {
            if (!string.IsNullOrEmpty(govMeta.Title))
                sb.AppendLine($"gov_title: \"{EscapeYaml(govMeta.Title)}\"");

            if (!string.IsNullOrEmpty(govMeta.DocumentNumber))
                sb.AppendLine($"gov_doc_number: \"{EscapeYaml(govMeta.DocumentNumber)}\"");

            if (!string.IsNullOrEmpty(govMeta.IssuingAuthority))
                sb.AppendLine($"gov_issuing_authority: \"{EscapeYaml(govMeta.IssuingAuthority)}\"");

            if (!string.IsNullOrEmpty(govMeta.PublishDate))
                sb.AppendLine($"gov_publish_date: \"{govMeta.PublishDate}\"");

            if (!string.IsNullOrEmpty(govMeta.DocumentType))
                sb.AppendLine($"gov_document_type: \"{govMeta.DocumentType}\"");

            if (govMeta.SubjectKeywords.Count > 0)
            {
                var kwJson = string.Join(", ", govMeta.SubjectKeywords.Select(k => $"\"{k}\""));
                sb.AppendLine($"gov_keywords: [{kwJson}]");
            }

            if (govMeta.Confidence > 0)
                sb.AppendLine($"gov_confidence: {govMeta.Confidence:F2}");

            if (govMeta.IsGovDocument)
                sb.AppendLine("gov_document: true");
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static string EscapeYaml(string value)
    {
        if (value.Contains('"'))
            return value.Replace("\"", "\\\"");
        return value;
    }

    #endregion

    #region AI_AGENT_NOTICE

    private static void AppendAgentNotice(StringBuilder sb, ConversionResult result)
    {
        if (result.Warnings.Count == 0 && !result.OcrUsed)
            return;

        sb.AppendLine("<!-- AI_AGENT_NOTICE: START -->");
        sb.AppendLine("<!-- 此文档由机器自动转换生成，部分内容可能存在丢失或降级，请参阅 .quality_report.json 获取详情 -->");

        if (result.OcrUsed)
        {
            sb.AppendLine("<!-- OCR_MODE: 原文档无可提取文字，已使用 OCR 识别，结果可能不够准确 -->");
        }

        foreach (var warning in result.Warnings)
        {
            var loc = string.IsNullOrEmpty(warning.Location) ? "" : $" @ {warning.Location}";
            sb.AppendLine($"<!-- WARNING: [{warning.Code}] {warning.Message}{loc} -->");
        }

        sb.AppendLine("<!-- AI_AGENT_NOTICE: END -->");
        sb.AppendLine();
    }

    #endregion

    #region Block ID 注入

    /// <summary>
    /// 在每个 Markdown 块级元素后注入 block_id 注释。
    /// 块类型：heading, paragraph, list_item, table, image_placeholder, 
    ///         footnote_placeholder, comment_placeholder
    /// </summary>
    private static string InjectBlockIds(string markdown, ref int blockCount)
    {
        var lines = markdown.Split('\n');
        var result = new List<string>();
        int currentBlockId = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            result.Add(line);

            // 判断行是否是块级元素的开始
            var blockType = ClassifyBlock(line);
            if (blockType != null)
            {
                // 检查下一行是否是同一个块的一部分（如表格的多行）
                if (blockType == "table")
                {
                    // 表格：跳过后续的表格行，在表格结束后注入
                    while (i + 1 < lines.Length && IsTableContinuation(lines[i + 1]))
                    {
                        i++;
                        result.Add(lines[i]);
                    }
                }

                currentBlockId++;
                blockCount++;
                result.Add($"<!-- block_id=b{currentBlockId:D4} type={blockType} -->");
            }
        }

        return string.Join('\n', result);
    }

    private static string? ClassifyBlock(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var trimmed = line.TrimStart();

        // 标题
        if (trimmed.StartsWith('#'))
            return "heading";

        // 表格行
        if (trimmed.StartsWith('|'))
            return "table";

        // 无序列表
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
            return "list_item";

        // 有序列表
        if (Regex.IsMatch(trimmed, @"^\d+\.\s"))
            return "list_item";

        // 图片占位符（P0 中由 Parser 插入的占位文字）
        if (trimmed.StartsWith("![") || trimmed.Contains("<!-- IMAGE_PLACEHOLDER"))
            return "image_placeholder";

        // 脚注占位符
        if (trimmed.Contains("<!-- FOOTNOTE_PLACEHOLDER"))
            return "footnote_placeholder";

        // 批注占位符
        if (trimmed.Contains("<!-- COMMENT_PLACEHOLDER"))
            return "comment_placeholder";

        // 空行后的非空行 = 段落开始
        // 但为了避免把 frontmatter/AI_AGENT_NOTICE 行也标记为段落，
        // 只对不以 <!-- 开头的行做段落标记
        if (!trimmed.StartsWith("<!--") && !trimmed.StartsWith("---") && trimmed.Length > 0)
            return "paragraph";

        return null;
    }

    private static bool IsTableContinuation(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('|') || trimmed.StartsWith("<!-- block_id=");
    }

    #endregion

    #region 不支持对象计数

    private static int CountUnsupportedObjects(string markdown)
    {
        int count = 0;
        count += Regex.Matches(markdown, @"<!-- IMAGE_PLACEHOLDER").Count;
        count += Regex.Matches(markdown, @"<!-- FOOTNOTE_PLACEHOLDER").Count;
        count += Regex.Matches(markdown, @"<!-- COMMENT_PLACEHOLDER").Count;
        count += Regex.Matches(markdown, @"<!-- TABLE_TRUNCATED").Count;
        count += Regex.Matches(markdown, @"<!-- TABLE_DEGRADED").Count;
        return count;
    }

    #endregion
}

public class PostProcessResult
{
    public string Markdown { get; set; } = string.Empty;
    public int BlockCount { get; set; }
    public int UnsupportedObjectCount { get; set; }
}
