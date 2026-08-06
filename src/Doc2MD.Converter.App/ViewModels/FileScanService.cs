using System.IO;
using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.ViewModels;

/// <summary>
/// 文件系统扫描服务，负责扫描文件夹并过滤支持的文件类型
/// </summary>
internal sealed class FileScanService
{
    private readonly AppConfig _config;

    public FileScanService(AppConfig config)
    {
        _config = config;
    }

    public FolderScanResult ScanFolder(
        string folderPath,
        AppMode mode,
        CancellationToken cancellationToken,
        IProgress<ScanProgressInfo>? progress)
    {
        var result = new FolderScanResult();
        var stack = new Stack<string>();
        stack.Push(folderPath);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = System.IO.Directory.EnumerateFileSystemEntries(current);
            }
            catch (Exception ex)
            {
                LoggingService.Warning($"无法访问目录: {current} - {ex.Message}");
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ShouldIgnorePath(entry))
                {
                    continue;
                }

                if (System.IO.Directory.Exists(entry))
                {
                    if (_config.Conversion.RecursiveScan)
                    {
                        stack.Push(entry);
                    }

                    continue;
                }

                result.Found++;

                if (IsSupportedForMode(entry, mode))
                {
                    result.Supported++;
                    result.Files.Add(CreateFileItem(entry));
                }
                else
                {
                    result.Unsupported++;
                }

                progress?.Report(new ScanProgressInfo(result.Found, result.Supported));
            }
        }

        return result;
    }

    private bool ShouldIgnorePath(string path)
    {
        if (!_config.Conversion.IgnoreHiddenFiles)
        {
            return false;
        }

        try
        {
            var attributes = System.IO.File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.Hidden) || Path.GetFileName(path).StartsWith('.');
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsSupportedForMode(string filePath, AppMode mode)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return mode switch
        {
            AppMode.MarkdownToDocx => ext is ".md" or ".markdown",
            AppMode.FormatDoc => ext is ".doc" or ".docx",
            _ => ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx"
        };
    }

    private static FileItem CreateFileItem(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return new FileItem
        {
            FullPath = filePath,
            FileName = Path.GetFileName(filePath),
            Directory = Path.GetDirectoryName(filePath) ?? string.Empty,
            Extension = ext,
            Type = FileItem.GetFileType(ext),
            Status = FileStatus.Pending
        };
    }

    internal readonly record struct ScanProgressInfo(int Found, int Supported);

    internal sealed class FolderScanResult
    {
        public List<FileItem> Files { get; } = new();
        public int Found { get; set; }
        public int Supported { get; set; }
        public int Unsupported { get; set; }
    }
}
