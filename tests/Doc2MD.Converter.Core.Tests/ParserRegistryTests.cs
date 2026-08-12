using Doc2MD.Models;
using Doc2MD.Parsers;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// C3: Parser 接口抽象测试
/// 验证 DocumentParserRegistry 的解析分发、配置注入与可扩展性。
/// </summary>
public class ParserRegistryTests
{
    [Fact]
    public void DefaultRegistry_ContainsAllBuiltInParsers()
    {
        var registry = new DocumentParserRegistry();

        Assert.NotEmpty(registry.Parsers);
        // 至少包含 Word/Excel/PowerPoint/Text/PDF 五种内置解析器
        Assert.Contains(registry.Parsers, p => p.SupportedType == FileType.Word);
        Assert.Contains(registry.Parsers, p => p.SupportedType == FileType.Excel);
        Assert.Contains(registry.Parsers, p => p.SupportedType == FileType.PowerPoint);
        Assert.Contains(registry.Parsers, p => p.SupportedType == FileType.Text);
        Assert.Contains(registry.Parsers, p => p.SupportedType == FileType.PDF);
    }

    [Theory]
    [InlineData("report.docx", FileType.Word)]
    [InlineData("book.xlsx", FileType.Excel)]
    [InlineData("slides.pptx", FileType.PowerPoint)]
    [InlineData("notes.txt", FileType.Text)]
    [InlineData("scan.pdf", FileType.PDF)]
    public void Resolve_SelectsCorrectParserByExtension(string fileName, FileType expected)
    {
        var registry = new DocumentParserRegistry();

        var parser = registry.Resolve(ConversionTarget.Markdown, "C:\\temp\\" + fileName);

        Assert.NotNull(parser);
        Assert.Equal(expected, parser!.SupportedType);
    }

    [Fact]
    public void Resolve_ReturnsNullForUnsupportedFile()
    {
        var registry = new DocumentParserRegistry();

        var parser = registry.Resolve(ConversionTarget.Markdown, "C:\\temp\\unknown.bin");

        Assert.Null(parser);
    }

    [Fact]
    public void Resolve_IgnoresParsersForDifferentTarget()
    {
        var registry = new DocumentParserRegistry();

        // 当前仅支持 Markdown 目标，其他目标应无法解析
        var parser = registry.Resolve((ConversionTarget)99, "C:\\temp\\report.docx");

        Assert.Null(parser);
    }

    [Fact]
    public void ConfigureAll_InjectsConfigIntoPdfParser()
    {
        var registry = new DocumentParserRegistry();
        var config = new AppConfig { Preview = new PreviewSettings() };

        registry.ConfigureAll(config);

        // PDF 解析器的 OCR 开关应已从配置注入
        var pdfParser = (PdfParser)registry.Parsers.First(p => p is PdfParser);
        Assert.Equal(config.Preview.DocumentToMarkdown.EnableOcr, pdfParser.EnableOcr);
    }

    [Fact]
    public void CustomParsers_CanBeRegistered()
    {
        var custom = new StubParser(FileType.Text, ".custom");
        var registry = new DocumentParserRegistry([custom]);

        Assert.Single(registry.Parsers);
        Assert.Same(custom, registry.Resolve(ConversionTarget.Markdown, "C:\\temp\\file.custom"));
    }

    /// <summary>测试用桩解析器，验证注册表可扩展性。</summary>
    private sealed class StubParser : IDocumentParser
    {
        private readonly string _extension;

        public StubParser(FileType type, string extension)
        {
            SupportedType = type;
            _extension = extension;
        }

        public FileType SupportedType { get; }
        public ConversionTarget Target => ConversionTarget.Markdown;

        public bool CanParse(string filePath) =>
            Path.GetExtension(filePath).Equals(_extension, StringComparison.OrdinalIgnoreCase);

        public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Stub parser should not parse");
    }
}
