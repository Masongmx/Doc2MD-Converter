using System.Diagnostics;
using System.IO;
using System.Text;
using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Parsers;

public class TextParser : IDocumentParser
{
    public FileType SupportedType => FileType.Text;
    public ConversionTarget Target => ConversionTarget.Markdown;

    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".txt";
    }

    public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            result.SourceFilePath = filePath;
            result.SourceType = "Text";

            cancellationToken.ThrowIfCancellationRequested();
            var content = TextFileReader.ReadAllText(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            var sb = new StringBuilder();
            sb.AppendLine($"# {fileName}");
            sb.AppendLine();
            sb.Append(content);

            result.SourceFileName = Path.GetFileName(filePath);
            result.RawMarkdown = sb.ToString();
            result.Success = true;
            result.OutputPath = Path.Combine(outputDirectory, 
                Path.GetFileNameWithoutExtension(filePath) + ".md");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }
}
