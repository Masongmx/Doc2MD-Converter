using System.Text.Json.Serialization;
using Doc2MD.Services;

namespace Doc2MD.Models;

/// <summary>
/// 转换结果（核心输出）。
/// C4 拆分：来源/元数据见 <see cref="ConversionMetadata"/>，质量/警告见 <see cref="ConversionQuality"/>。
/// </summary>
public class ConversionResult
{
    // === 核心输出 ===

    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }

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

    // === 组合子对象（C4 拆分） ===

    /// <summary>来源信息与文档统计（C4）</summary>
    public ConversionMetadata Metadata { get; set; } = new();

    /// <summary>质量评分与警告（C4）</summary>
    public ConversionQuality Quality { get; set; } = new();
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
