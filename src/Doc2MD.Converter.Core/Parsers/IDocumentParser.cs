namespace Doc2MD.Parsers;

using Doc2MD.Models;

/// <summary>
/// 文档解析器策略接口。所有格式（Word/Excel/PPT/Text/PDF 等）统一实现此接口，
/// 由 <see cref="IParserRegistry"/> 依据文件路径与转换目标分发给对应实现。
/// </summary>
public interface IDocumentParser
{
    /// <summary>声明支持的文档格式类型。</summary>
    FileType SupportedType { get; }

    /// <summary>转换目标（目前仅 Markdown）。</summary>
    ConversionTarget Target { get; }

    /// <summary>根据文件路径判断当前解析器是否能够处理。</summary>
    bool CanParse(string filePath);

    /// <summary>执行解析，返回转换结果。</summary>
    ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// 在解析前向解析器注入全局配置。默认无操作；
    /// 需要读取配置的解析器（如 PDF 的 OCR 开关）可覆写此方法。
    /// 这样调用方无需针对具体类型做向下转型。
    /// </summary>
    void Configure(AppConfig? config) { }
}
