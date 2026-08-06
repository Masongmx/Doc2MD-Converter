namespace Doc2MD.Parsers;

using Doc2MD.Models;

public interface IDocumentParser
{
    FileType SupportedType { get; }
    ConversionTarget Target { get; }
    bool CanParse(string filePath);
    ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken);
}
