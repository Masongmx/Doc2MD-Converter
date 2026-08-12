using System.Diagnostics;
using System.IO;

namespace Doc2MD.Services;

/// <summary>
/// 通过本机/随程序部署的 LibreOffice 完成旧式二进制 Office 文件的离线转档。
/// 旧的 .doc/.xls/.ppt 没有可靠的通用纯 .NET 排版解析器；转为 OpenXML 后复用现有解析器。
/// </summary>
public static class LegacyOfficeConverter
{
    public static LegacyConversionResult Convert(string sourcePath, string targetExtension, CancellationToken cancellationToken)
    {
        var executable = FindLibreOffice();
        if (executable is null)
            return LegacyConversionResult.Fail("未找到 LibreOffice。请安装 LibreOffice，或将其放入程序目录 tools\\LibreOffice 后重试。");

        var tempDirectory = Path.Combine(Path.GetTempPath(), "Doc2MD", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add("--headless");
            process.StartInfo.ArgumentList.Add("--convert-to");
            process.StartInfo.ArgumentList.Add(targetExtension.TrimStart('.'));
            process.StartInfo.ArgumentList.Add("--outdir");
            process.StartInfo.ArgumentList.Add(tempDirectory);
            process.StartInfo.ArgumentList.Add(sourcePath);

            process.Start();
            // 仅异步读取 stderr（用于错误诊断），避免 stdout/stderr 同时异步读取导致死锁
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!WaitForExit(process, TimeSpan.FromMinutes(2), cancellationToken))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                TryDelete(tempDirectory);
                return cancellationToken.IsCancellationRequested
                    ? LegacyConversionResult.Fail("已取消旧版 Office 转换。")
                    : LegacyConversionResult.Fail("LibreOffice 转换超时（120 秒）。");
            }

            var convertedPath = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(sourcePath) + targetExtension);
            if (process.ExitCode != 0 || !File.Exists(convertedPath))
            {
                var error = errorTask.GetAwaiter().GetResult().Trim();
                TryDelete(tempDirectory);
                return LegacyConversionResult.Fail(string.IsNullOrWhiteSpace(error)
                    ? "LibreOffice 未能转换该旧格式文件，文件可能损坏或受密码保护。"
                    : $"LibreOffice 转换失败: {error}");
            }
            // 进程退出后同步读取 stdout 并丢弃（避免管道缓冲区残留）
            _ = process.StandardOutput.ReadToEndAsync().GetAwaiter().GetResult();
            return LegacyConversionResult.Success(convertedPath, tempDirectory);
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempDirectory);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempDirectory);
            return LegacyConversionResult.Fail($"启动 LibreOffice 失败: {ex.Message}");
        }
    }

    private static bool WaitForExit(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopwatch.Elapsed >= timeout)
                return false;
            process.WaitForExit(200);
        }

        return true;
    }

    public static void Cleanup(LegacyConversionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.TempDirectory)) TryDelete(result.TempDirectory);
    }

    private static string? FindLibreOffice()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("DOC2MD_LIBREOFFICE_PATH"),
            Path.Combine(appDirectory, "tools", "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    /// <summary>
    /// 检测旧式二进制 Office 格式（.doc/.xls/.ppt）转换所需的兜底工具是否可用。
    /// 结果供 UI 层在设置页展示状态或添加旧格式文件时提示用户。
    /// </summary>
    public static bool IsLibreOfficeAvailable()
    {
        return FindLibreOffice() != null;
    }

    /// <summary>判断给定扩展名是否为需要 LibreOffice/Office COM 兜底的旧式二进制格式。</summary>
    public static bool IsLegacyOfficeFormat(string? extension)
    {
        return extension?.ToLowerInvariant() is ".doc" or ".xls" or ".ppt";
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
                LoggingService.Debug($"已清理临时目录: {directory}");
            }
        }
        catch (Exception ex)
        {
            LoggingService.Warning($"清理临时目录失败: {directory}, 错误: {ex.Message}");
        }
    }
}

public sealed record LegacyConversionResult(bool IsSuccess, string? ConvertedPath, string? TempDirectory, string? ErrorMessage)
{
    public static LegacyConversionResult Success(string path, string tempDirectory) => new(true, path, tempDirectory, null);
    public static LegacyConversionResult Fail(string message) => new(false, null, null, message);
}
