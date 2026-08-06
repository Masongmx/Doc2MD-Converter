using System.Text.Json.Serialization;
using Doc2MD.Services;

namespace Doc2MD.Models;

public class ConversionResult
{
    // === 原有字段 ===
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }

    // === 来源信息 ===

    /// <summary>源文件完整路径</summary>
    public string? SourceFilePath { get; set; }

    /// <summary>源文件名（仅文件名，不含路径）</summary>
    public string? SourceFileName { get; set; }

    /// <summary>源文件类型 (PDF/Word/Excel/PowerPoint/Text)</summary>
    public string? SourceType { get; set; }

    /// <summary>源文件大小（字节）</summary>
    public long SourceFileSize { get; set; }

    /// <summary>源文件 SHA-256 哈希</summary>
    public string? SourceFileHashSha256 { get; set; }

    // === 文档统计 ===

    /// <summary>PDF 页数</summary>
    public int PageCount { get; set; }

    /// <summary>Excel 工作表数</summary>
    public int SheetCount { get; set; }

    /// <summary>PPT 幻灯片数</summary>
    public int SlideCount { get; set; }

    /// <summary>是否使用了 OCR</summary>
    public bool OcrUsed { get; set; }

    // === 质量与警告 ===

    /// <summary>转换过程中产生的警告列表</summary>
    public List<ConversionWarning> Warnings { get; set; } = [];

    /// <summary>Markdown 内容中的 block 数量（后处理阶段填充）</summary>
    public int BlockCount { get; set; }

    /// <summary>不支持的对象数量（后处理阶段填充）</summary>
    public int UnsupportedObjectCount { get; set; }

    /// <summary>质量评分（0.0 - 1.0）</summary>
    public double QualityScore { get; set; } = 1.0;

    /// <summary>文档语言</summary>
    public string Language { get; set; } = "zh-CN";

    // === 输出包信息 ===

    /// <summary>输出包中包含的所有文件路径（相对路径）</summary>
    public List<string> OutputFiles { get; set; } = [];

    /// <summary>原始 Markdown 内容（Parser 产出，后处理阶段使用）</summary>
    [JsonIgnore]
    public string? RawMarkdown { get; set; }

    /// <summary>后处理后的 Markdown 内容</summary>
    [JsonIgnore]
    public string? ProcessedMarkdown { get; set; }

    // === 图片资产导出（用于 assets/ 目录）===
    public List<ImageExport> ImageExports { get; set; } = [];

    // === 表格 CSV 导出（用于 tables/ 目录）===
    public List<TableExport> TableExports { get; set; } = [];

    // === 公文元数据（v2.0 新增） ===

    /// <summary>公文元数据提取结果（后处理阶段填充）</summary>
    [JsonIgnore]
    public GovMetadata? GovMetadata { get; set; }

    /// <summary>导入建议等级（v2.0 新增）：recommended | review | not_recommended</summary>
    public string ImportRecommendation { get; set; } = "recommended";
}

/// <summary>
/// 转换警告
/// </summary>
public class ConversionWarning
{
    /// <summary>警告代码，如 W_IMG_LOST, W_TABLE_DEGRADE 等</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>警告描述</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>警告来源位置（页码/Sheet名/幻灯片号等）</summary>
    public string? Location { get; set; }

    /// <summary>严重程度：high | medium | low</summary>
    public string Severity { get; set; } = "low";

    /// <summary>
    /// 根据警告代码自动分类严重程度
    /// </summary>
    public static string ClassifySeverity(string code) => code switch
    {
        "W_IMG_LOST" => "high",
        "W_TABLE_DEGRADE" => "high",
        "W_TABLE_TRUNCATED" => "medium",
        "W_MERGED_CELLS" => "medium",
        "W_FOOTNOTE_LOST" => "medium",
        "W_COMMENT_LOST" => "medium",
        "W_HYPERLINK_URL_LOST" => "low",
        "W_ORDERED_LIST_FLAT" => "low",
        "W_OCR_FAILED" => "high",
        "W_OCR_LOW_CONFIDENCE" => "medium",
        "W_REVISION_LOST" => "low",
        "W_FORMULA_LOST" => "low",
        "W_CHART_LOST" => "medium",
        "W_EMBEDDED_OBJECT_LOST" => "medium",
        "W_TWO_COLUMN_PDF" => "medium",
        "W_HIDDEN_ROW" => "low",
        "W_AIGC_WATERMARK" => "high",
        "W_LEGACY_FALLBACK" => "medium",
        _ => "low"
    };

    /// <summary>
    /// 创建带自动严重程度分类的警告
    /// </summary>
    public static ConversionWarning Create(string code, string message, string? location = null)
    {
        return new ConversionWarning
        {
            Code = code,
            Severity = ClassifySeverity(code),
            Message = message,
            Location = location
        };
    }
}

/// <summary>
/// 图片资产导出条目：一个图片对应一个文件
/// </summary>
public class ImageExport
{
    /// <summary>图片文件名（不含路径，如 image_001.png）</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>图片二进制数据</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>图片 MIME 类型（如 image/png, image/jpeg）</summary>
    public string MimeType { get; set; } = "image/png";

    /// <summary>Markdown 中引用此图片的描述文本</summary>
    public string AltText { get; set; } = "图片";

    /// <summary>来源位置描述（如"第 2 页"、"幻灯片 3"）</summary>
    public string? Location { get; set; }
}

/// <summary>
/// 表格 CSV 导出条目：一个大表对应一个 CSV 文件
/// </summary>
public class TableExport
{
    /// <summary>CSV 文件名（不含路径，如 Sheet1.csv）</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>完整 CSV 内容（含表头行）</summary>
    public string CsvContent { get; set; } = string.Empty;
}
