using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 生成 document.meta.json，包含文档来源、结构、统计信息。
/// </summary>
public static class MetaGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Generate(ConversionResult result)
    {
        var meta = new DocumentMeta
        {
            SourceFile = Path.GetFileName(result.Metadata.SourceFilePath),
            SourceType = result.Metadata.SourceType,
            SourceSizeBytes = result.Metadata.SourceFileSize > 0 ? result.Metadata.SourceFileSize : null,
            SourceSha256 = result.Metadata.SourceFileHashSha256,
            PageCount = result.Metadata.PageCount > 0 ? result.Metadata.PageCount : null,
            SheetCount = result.Metadata.SheetCount > 0 ? result.Metadata.SheetCount : null,
            SlideCount = result.Metadata.SlideCount > 0 ? result.Metadata.SlideCount : null,
            OcrUsed = result.Metadata.OcrUsed,
            BlockCount = result.Quality.BlockCount,
            UnsupportedObjectCount = result.Quality.UnsupportedObjectCount,
            ConvertedAt = DateTimeOffset.Now,
            Converter = Doc2MD.Constants.AppVersion.Converter
        };

        return JsonSerializer.Serialize(meta, JsonOptions);
    }
}

/// <summary>
/// 文档元数据结构
/// </summary>
public class DocumentMeta
{
    public string? SourceFile { get; set; }
    public string? SourceType { get; set; }
    public long? SourceSizeBytes { get; set; }
    public string? SourceSha256 { get; set; }
    public int? PageCount { get; set; }
    public int? SheetCount { get; set; }
    public int? SlideCount { get; set; }
    public bool OcrUsed { get; set; }
    public int BlockCount { get; set; }
    public int UnsupportedObjectCount { get; set; }
    public DateTimeOffset ConvertedAt { get; set; }
    public string Converter { get; set; } = string.Empty;
}
