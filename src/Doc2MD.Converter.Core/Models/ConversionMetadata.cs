using System.Text.Json.Serialization;
using Doc2MD.Services;

namespace Doc2MD.Models;

/// <summary>
/// 转换元数据：来源信息 + 文档统计 + 公文元数据。
/// 从 ConversionResult 拆分（C4），职责聚焦"转换前/转换过程中的文档信息"。
/// </summary>
public class ConversionMetadata
{
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

    // === 公文元数据（v2.0 新增） ===

    /// <summary>公文元数据提取结果（后处理阶段填充）</summary>
    [JsonIgnore]
    public GovMetadata? GovMetadata { get; set; }
}
