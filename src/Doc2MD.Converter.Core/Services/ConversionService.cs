using System.IO;
using Doc2MD.Models;
using Doc2MD.Parsers;
using Doc2MD.Services;

namespace Doc2MD.Services;

public class ConversionService
{
    private readonly List<IDocumentParser> _parsers;

    public event EventHandler<FileItem>? FileCompleted;

    public ConversionService()
    {
        _parsers =
        [
            new WordParser(),
            new ExcelParser(),
            new PowerPointParser(),
            new TextParser(),
            new PdfParser(),
            new MarkdownToDocxParser()
        ];
    }

    public async Task ConvertFileAsync(
        FileItem file,
        string outputDirectory,
        bool preserveStructure,
        string? inputRoot,
        ConversionTarget target,
        AppConfig? config,
        CancellationToken cancellationToken)
    {
        await ConvertFileCoreAsync(file, outputDirectory, preserveStructure, inputRoot, target, config, null, cancellationToken);
    }

    /// <summary>
    /// MarkdownToDocx 专用：传入 MarkdownToDocxPreviewSettings 以驱动排版参数
    /// </summary>
    public async Task ConvertFileAsync(
        FileItem file,
        string outputDirectory,
        bool preserveStructure,
        string? inputRoot,
        ConversionTarget target,
        MarkdownToDocxPreviewSettings? md2docxSettings,
        CancellationToken cancellationToken)
    {
        await ConvertFileCoreAsync(file, outputDirectory, preserveStructure, inputRoot, target, null, md2docxSettings, cancellationToken);
    }

    /// <summary>
    /// 向后兼容：旧调用方式不传 config 和 md2docxSettings
    /// </summary>
    public Task ConvertFileAsync(
        FileItem file,
        string outputDirectory,
        bool preserveStructure,
        string? inputRoot,
        ConversionTarget target,
        CancellationToken cancellationToken)
    {
        return ConvertFileCoreAsync(file, outputDirectory, preserveStructure, inputRoot, target, null, null, cancellationToken);
    }

    private async Task ConvertFileCoreAsync(
        FileItem file,
        string outputDirectory,
        bool preserveStructure,
        string? inputRoot,
        ConversionTarget target,
        AppConfig? config,
        MarkdownToDocxPreviewSettings? md2docxSettings,
        CancellationToken cancellationToken)
    {
        file.Status = FileStatus.Processing;
        LoggingService.Info($"[Conversion] 开始处理: {file.FullPath}");

        var parser = _parsers.FirstOrDefault(p => p.Target == target && p.CanParse(file.FullPath));
        if (parser == null)
        {
            file.Status = FileStatus.Failed;
            file.ErrorMessage = $"不支持的文件格式: {Path.GetExtension(file.FullPath)}";
            FileCompleted?.Invoke(this, file);
            return;
        }

        var currentOutputDirectory = ResolveOutputDirectory(file, outputDirectory, preserveStructure, inputRoot);
        Directory.CreateDirectory(currentOutputDirectory);

        // 如果是 MarkdownToDocxParser，在 Parse 前注入 PreviewSettings
        if (parser is MarkdownToDocxParser md2docxParser && md2docxSettings is not null)
        {
            md2docxParser.PreviewSettings = md2docxSettings;
        }

        // 如果是 PdfParser，注入 OCR 配置
        if (parser is PdfParser pdfParser && config != null)
        {
            pdfParser.EnableOcr = config.Preview.DocumentToMarkdown.EnableOcr;
        }

        try
        {
            var result = await Task.Run(
                () => parser.Parse(file.FullPath, currentOutputDirectory, cancellationToken),
                cancellationToken);

            if (result.Success)
            {
                // === 填充来源信息 ===
                FillSourceInfo(result, file.FullPath);

                // === 获取 RawMarkdown（Parser 可能写文件也可能存在 RawMarkdown） ===
                var rawMarkdown = result.RawMarkdown;
                if (string.IsNullOrEmpty(rawMarkdown) && !string.IsNullOrEmpty(result.OutputPath) && File.Exists(result.OutputPath))
                {
                    rawMarkdown = await File.ReadAllTextAsync(result.OutputPath, cancellationToken);
                }

                // === 如果只有 ToMarkdown 方向需要后处理 ===
                if (target == ConversionTarget.Markdown && !string.IsNullOrEmpty(rawMarkdown))
                {
                    // === 后处理管线 ===
                    var postResult = MarkdownPostProcessor.Process(rawMarkdown, result);
                    result.BlockCount = postResult.BlockCount;
                    result.UnsupportedObjectCount = postResult.UnsupportedObjectCount;

                    var metaJson = MetaGenerator.Generate(result);
                    var qualityJson = QualityChecker.GenerateReport(result);

                    var packageMode = config?.Conversion.OutputPackageMode ?? OutputPackageMode.HybridPackage;
                    var writeResult = OutputPackageWriter.Write(
                        postResult.Markdown,
                        metaJson,
                        qualityJson,
                        result,
                        currentOutputDirectory,
                        packageMode);

                    // 删除 Parser 生成的旧文件（如果有）
                    if (!string.IsNullOrEmpty(result.OutputPath) && File.Exists(result.OutputPath) && result.OutputPath != writeResult.PrimaryOutputPath)
                    {
                        try { File.Delete(result.OutputPath); } catch { /* 忽略删除失败 */ }
                    }

                    result.OutputPath = writeResult.PrimaryOutputPath;
                    result.OutputFiles = writeResult.OutputFiles;
                }

                file.Status = FileStatus.Done;
                file.OutputPath = result.OutputPath;
                file.ErrorMessage = null;
                LoggingService.Info($"[Conversion] 完成: {file.FullPath} -> {result.OutputPath}");
            }
            else
            {
                file.Status = FileStatus.Failed;
                file.ErrorMessage = result.ErrorMessage;
                LoggingService.Warning($"[Conversion] 失败: {file.FullPath} - {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            file.Status = FileStatus.Pending;
            file.ErrorMessage = null;
            LoggingService.Warning($"[Conversion] 已取消: {file.FullPath}");
            throw;
        }
        catch (Exception ex)
        {
            file.Status = FileStatus.Failed;
            file.ErrorMessage = ex.Message;
            LoggingService.Error($"[Conversion] 异常: {file.FullPath}", ex);
        }
        finally
        {
            FileCompleted?.Invoke(this, file);
        }
    }

    private static void FillSourceInfo(ConversionResult result, string filePath)
    {
        result.SourceFilePath = filePath;
        result.SourceFileName ??= Path.GetFileName(filePath);
        result.SourceType = result.SourceType ?? Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".pdf" => "PDF",
            ".docx" or ".doc" => "Word",
            ".xlsx" or ".xls" => "Excel",
            ".pptx" or ".ppt" => "PowerPoint",
            ".txt" => "Text",
            ".md" or ".markdown" => "Markdown",
            _ => "Unknown"
        };

        try
        {
            var fi = new FileInfo(filePath);
            result.SourceFileSize = fi.Length;

            if (fi.Length > 0)
            {
                result.SourceFileHashSha256 = MarkdownPostProcessor.ComputeSha256(filePath);
            }
        }
        catch (Exception ex)
        {
            LoggingService.Warning($"[Conversion] 无法读取文件信息: {filePath} - {ex.Message}");
        }
    }

    private static string ResolveOutputDirectory(
        FileItem file,
        string outputDirectory,
        bool preserveStructure,
        string? inputRoot)
    {
        if (!preserveStructure || string.IsNullOrWhiteSpace(inputRoot))
        {
            return outputDirectory;
        }

        var fileDir = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
        if (!fileDir.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase))
        {
            return outputDirectory;
        }

        var relativePath = fileDir[inputRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativePath)
            ? outputDirectory
            : Path.Combine(outputDirectory, relativePath);
    }
}
