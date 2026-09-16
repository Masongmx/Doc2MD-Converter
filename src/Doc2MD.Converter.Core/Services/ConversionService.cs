using System.IO;
using Doc2MD.Failures;
using Doc2MD.Humanizer;
using Doc2MD.Models;
using Doc2MD.Parsers;
using Doc2MD.Services;

namespace Doc2MD.Services;

public class ConversionService
{
    private readonly IParserRegistry _parserRegistry;
    private readonly ILoggingService _logger;
    private readonly FailureOrchestrator _failureOrchestrator;
    private readonly HumanizerPipeline _humanizerPipeline;

    public event EventHandler<FileItem>? FileCompleted;

    public FailureOrchestrator FailureOrchestrator => _failureOrchestrator;
    public HumanizerPipeline HumanizerPipeline => _humanizerPipeline;

    public ConversionService()
        : this(new DocumentParserRegistry(), LoggingService.Logger, new FailureOrchestrator(), new HumanizerPipeline())
    {
    }

    public ConversionService(IParserRegistry parserRegistry)
        : this(parserRegistry, LoggingService.Logger, new FailureOrchestrator(), new HumanizerPipeline())
    {
    }

    public ConversionService(IParserRegistry parserRegistry, ILoggingService logger)
        : this(parserRegistry, logger, new FailureOrchestrator(), new HumanizerPipeline())
    {
    }

    public ConversionService(
        IParserRegistry parserRegistry,
        ILoggingService logger,
        FailureOrchestrator failureOrchestrator,
        HumanizerPipeline humanizerPipeline)
    {
        _parserRegistry = parserRegistry;
        _logger = logger;
        _failureOrchestrator = failureOrchestrator;
        _humanizerPipeline = humanizerPipeline;
    }

    /// <summary>
    /// 转换单个文件。返回 ConversionResult 供调用方读取质量评分等明细
    /// （F4 历史记录）；解析器不存在时返回 null。
    /// </summary>
    public async Task<ConversionResult?> ConvertFileAsync(
        FileItem file,
        string outputDirectory,
        bool preserveStructure,
        string? inputRoot,
        ConversionTarget target,
        AppConfig? config,
        CancellationToken cancellationToken)
    {
        file.Status = FileStatus.Processing;
        _logger.Info($"[Conversion] 开始处理: {file.FullPath}");

        var parser = _parserRegistry.Resolve(target, file.FullPath);
        if (parser == null)
        {
            file.Status = FileStatus.Failed;
            file.ErrorMessage = $"不支持的文件格式: {Path.GetExtension(file.FullPath)}";
            var nullResult = new ConversionResult { Success = false, ErrorMessage = file.ErrorMessage };
            _failureOrchestrator.HandleFailure(nullResult, null, file.FullPath);
            FileCompleted?.Invoke(this, file);
            return nullResult;
        }

        var currentOutputDirectory = ResolveOutputDirectory(file, outputDirectory, preserveStructure, inputRoot);
        Directory.CreateDirectory(currentOutputDirectory);

        // 通过接口钩子注入配置（如 PDF 的 OCR 开关），无需针对具体类型做向下转型
        parser.Configure(config);

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
                    result.Quality.BlockCount = postResult.BlockCount;
                    result.Quality.UnsupportedObjectCount = postResult.UnsupportedObjectCount;

                    // === 可读性增强中间件 ===
                    var finalMarkdown = postResult.Markdown;
                    if (config?.Humanizer != null && config.Humanizer.Enabled)
                    {
                        finalMarkdown = await _humanizerPipeline.ProcessAsync(finalMarkdown, result, config.Humanizer, cancellationToken);
                    }

                    var metaJson = MetaGenerator.Generate(result);
                    var qualityJson = QualityChecker.GenerateReport(result);

                    var packageMode = config?.Conversion.OutputPackageMode ?? OutputPackageMode.HybridPackage;
                    var writeResult = OutputPackageWriter.Write(
                        finalMarkdown,
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
                _logger.Info($"[Conversion] 完成: {file.FullPath} -> {result.OutputPath}");
                return result;
            }

            // 诊断与记录失败案例
            _failureOrchestrator.HandleFailure(result, null, file.FullPath);
            file.Status = FileStatus.Failed;
            file.ErrorMessage = result.ErrorMessage;
            _logger.Warning($"[Conversion] 失败: {file.FullPath} - {result.ErrorMessage}");
            return result;
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
            var failResult = new ConversionResult { Success = false, ErrorMessage = ex.Message };
            _failureOrchestrator.HandleFailure(failResult, ex, file.FullPath);
            file.Status = FileStatus.Failed;
            file.ErrorMessage = failResult.ErrorMessage;
            LoggingService.Error($"[Conversion] 异常: {file.FullPath}", ex);
            return failResult;
        }
        finally
        {
            FileCompleted?.Invoke(this, file);
        }
    }

    private void FillSourceInfo(ConversionResult result, string filePath)
    {
        var metadata = result.Metadata;
        metadata.SourceFilePath = filePath;
        metadata.SourceFileName ??= Path.GetFileName(filePath);
        metadata.SourceType = metadata.SourceType ?? Path.GetExtension(filePath).ToLowerInvariant() switch
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
            metadata.SourceFileSize = fi.Length;

            if (fi.Length > 0)
            {
                metadata.SourceFileHashSha256 = MarkdownPostProcessor.ComputeSha256(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"[Conversion] 无法读取文件信息: {filePath} - {ex.Message}");
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
