namespace Doc2MD.Parsers;

using Doc2MD.Models;

/// <summary>
/// 默认解析器注册表。集中登记所有内置解析器；新增格式时仅需在此注册一次，
/// 无需修改 ConversionService。可通过构造函数注入外部解析器集合以扩展或覆盖默认实现。
/// </summary>
public class DocumentParserRegistry : IParserRegistry
{
    private readonly IReadOnlyList<IDocumentParser> _parsers;

    /// <summary>使用默认内置解析器集合。</summary>
    public DocumentParserRegistry()
        : this(
        [
            new WordParser(),
            new ExcelParser(),
            new PowerPointParser(),
            new TextParser(),
            new PdfParser()
        ])
    {
    }

    /// <summary>使用外部提供的解析器集合（便于测试替换或未来扩展）。</summary>
    public DocumentParserRegistry(IEnumerable<IDocumentParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    public IReadOnlyList<IDocumentParser> Parsers => _parsers;

    public IDocumentParser? Resolve(ConversionTarget target, string filePath)
    {
        return _parsers.FirstOrDefault(p => p.Target == target && p.CanParse(filePath));
    }

    public void ConfigureAll(AppConfig? config)
    {
        foreach (var parser in _parsers)
        {
            parser.Configure(config);
        }
    }
}
