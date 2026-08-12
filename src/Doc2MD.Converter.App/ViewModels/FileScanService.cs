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

    /// <summary>进度上报节流间隔（毫秒），避免大文件夹扫描时高频 UI 回调。</summary>
    private const long ProgressThrottleMs = 100;

    public FolderScanResult ScanFolder(
        string folderPath,
        AppMode mode,
        CancellationToken cancellationToken,
        IProgress<ScanProgressInfo>? progress)
    {
        var result = new FolderScanResult();
        var stack = new Stack<string>();
        stack.Push(folderPath);

        var maxFiles = Math.Max(1, _config.Conversion.MaxScanFileCount);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

                // R3: 达到扫描数量上限时停止（丢弃剩余目录），避免超大文件夹卡顿
                if (result.Found >= maxFiles)
                {
                    result.Truncated = true;
                    stack.Clear();
                    break;
                }

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

                // R3: 节流——每 100ms 最多上报一次，减少 UI 线程压力
                if (stopwatch.ElapsedMilliseconds >= ProgressThrottleMs)
                {
                    progress?.Report(new ScanProgressInfo(result.Found, result.Supported));
                    stopwatch.Restart();
                }
            }
        }

        // 循环结束后强制上报一次，保证最终计数准确
        progress?.Report(new ScanProgressInfo(result.Found, result.Supported));

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

        /// <summary>R3: 是否因达到扫描数量上限而截断。</summary>
        public bool Truncated { get; set; }
    }
}
