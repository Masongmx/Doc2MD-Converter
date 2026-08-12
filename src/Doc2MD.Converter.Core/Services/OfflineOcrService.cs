using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Doc2MD.Services;

/// <summary>
/// 调用离线 OCRmyPDF（内部使用 Tesseract）处理扫描 PDF，不发送任何文件到网络。
/// 可通过 DOC2MD_OCRMYPDF_PATH 指定 ocrmypdf.exe 的绝对路径。
/// </summary>
public static class OfflineOcrService
{
    public static OcrResult ExtractPdfText(string pdfPath, CancellationToken cancellationToken)
    {
        var executable = FindExecutable();
        if (executable is null)
            return OcrResult.Fail("该 PDF 没有可提取文本，且未找到 OCRmyPDF。请安装 OCRmyPDF + 中文语言包 chi_sim，或设置 DOC2MD_OCRMYPDF_PATH。");

        // 为 OCRmyPDF 进程提供内置 Tesseract 的 PATH 与 TESSDATA_PREFIX（便携版自包含）
        var tesseractDir = FindTesseractDirectory();
        var envVars = new Dictionary<string, string>();
        if (tesseractDir is not null)
        {
            envVars["PATH"] = tesseractDir + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
            var tessdataDir = Path.Combine(tesseractDir, "tessdata");
            if (Directory.Exists(tessdataDir))
                envVars["TESSDATA_PREFIX"] = tessdataDir;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "Doc2MD", "ocr", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var sidecar = Path.Combine(tempDirectory, "result.txt");
            var outputPdf = Path.Combine(tempDirectory, "ocr.pdf");
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
            foreach (var (key, value) in envVars)
                process.StartInfo.Environment[key] = value;
            process.StartInfo.ArgumentList.Add("--force-ocr");
            process.StartInfo.ArgumentList.Add("--deskew");
            process.StartInfo.ArgumentList.Add("--rotate-pages");
            process.StartInfo.ArgumentList.Add("--language");
            process.StartInfo.ArgumentList.Add("chi_sim+eng");
            process.StartInfo.ArgumentList.Add("--sidecar");
            process.StartInfo.ArgumentList.Add(sidecar);
            process.StartInfo.ArgumentList.Add(pdfPath);
            process.StartInfo.ArgumentList.Add(outputPdf);
            process.Start();
            // 仅异步读取 stderr（用于错误诊断），避免 stdout/stderr 同时异步读取导致死锁
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!WaitForExit(process, TimeSpan.FromMinutes(10), cancellationToken))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return cancellationToken.IsCancellationRequested
                    ? OcrResult.Fail("已取消扫描 PDF OCR。")
                    : OcrResult.Fail("扫描 PDF OCR 超时（10 分钟）。");
            }
            if (process.ExitCode != 0 || !File.Exists(sidecar))
            {
                var error = errorTask.GetAwaiter().GetResult().Trim();
                return OcrResult.Fail(string.IsNullOrWhiteSpace(error) ? "OCRmyPDF 未能完成识别。" : $"OCR 失败: {error}");
            }
            // 进程退出后同步读取 stdout 并丢弃（避免管道缓冲区残留）
            _ = process.StandardOutput.ReadToEndAsync().GetAwaiter().GetResult();
            var text = File.ReadAllText(sidecar, Encoding.UTF8).Trim();
            return string.IsNullOrWhiteSpace(text) ? OcrResult.Fail("OCR 完成但未识别到文本。") : OcrResult.Success(text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OcrResult.Fail($"启动 OCR 引擎失败: {ex.Message}");
        }
        finally
        {
            try { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true); } catch { }
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

    private static string? FindExecutable()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("DOC2MD_OCRMYPDF_PATH"),
            // 完整版：Python 环境（ocrmypdf.exe 位于 Scripts\ 子目录）
            Path.Combine(appDirectory, "tools", "OCRmyPDF", "Scripts", "ocrmypdf.exe"),
            Path.Combine(appDirectory, "tools", "OCRmyPDF", "ocrmypdf.exe"),
            // 精简版：干净 venv
            Path.Combine(appDirectory, "tools", "OCRmyPDF-slim", "Scripts", "ocrmypdf.exe"),
            Path.Combine(appDirectory, "tools", "OCRmyPDF-slim", "ocrmypdf.exe")
        };
        // 系统安装版（外网 pip install ocrmypdf）：常见 Python Scripts 目录 + PATH 查找
        foreach (var pythonDir in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Python")
                 })
        {
            if (Directory.Exists(pythonDir))
            {
                foreach (var versionDir in Directory.GetDirectories(pythonDir))
                {
                    candidates.Add(Path.Combine(versionDir, "Scripts", "ocrmypdf.exe"));
                }
            }
        }
        var byPath = FindOnPath("ocrmypdf.exe");
        if (byPath is not null) candidates.Add(byPath);

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    /// <summary>在 PATH 中查找可执行文件（Windows，不区分大小写）。</summary>
    private static string? FindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return null;
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';');
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
            foreach (var ext in extensions)
            {
                var withExt = full + ext.ToLowerInvariant();
                if (File.Exists(withExt)) return withExt;
            }
        }
        return null;
    }

    /// <summary>
    /// 查找 Tesseract 目录：优先便携包内置，其次系统安装版。
    /// 需要目录中存在 tesseract.exe 才视为有效。
    /// </summary>
    private static string? FindTesseractDirectory()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDirectory, "tools", "OCRmyPDF", "tesseract"),
            Path.Combine(appDirectory, "tools", "OCRmyPDF-slim", "tesseract"),
            // 系统安装版（UB-Mannheim 安装器默认安装位置）
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR")
        };
        var dir = candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "tesseract.exe")));
        if (dir is not null) return dir;
        // 回退：PATH 中查找
        var byPath = FindOnPath("tesseract.exe");
        return byPath is null ? null : Path.GetDirectoryName(byPath);
    }
}

public sealed record OcrResult(bool IsSuccess, string? Text, string? ErrorMessage)
{
    public static OcrResult Success(string text) => new(true, text, null);
    public static OcrResult Fail(string message) => new(false, null, message);
}
