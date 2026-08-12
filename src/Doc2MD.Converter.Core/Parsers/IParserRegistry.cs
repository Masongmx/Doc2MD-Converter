namespace Doc2MD.Parsers;

using Doc2MD.Models;

/// <summary>
/// 解析器注册表接口。负责维护可用解析器的集合，并依据文件路径与转换目标
/// 分发给对应的解析器实现。使用方（如 ConversionService）依赖此接口而非
/// 直接持有具体解析器，从而提升可扩展性与可测试性。
/// </summary>
public interface IParserRegistry
{
    /// <summary>已注册的全部解析器（只读视图）。</summary>
    IReadOnlyList<IDocumentParser> Parsers { get; }

    /// <summary>
    /// 依据转换目标与文件路径解析出第一个匹配的解析器；无匹配则返回 null。
    /// </summary>
    IDocumentParser? Resolve(ConversionTarget target, string filePath);

    /// <summary>向所有解析器注入全局配置（解析前调用一次即可）。</summary>
    void ConfigureAll(AppConfig? config);
}
