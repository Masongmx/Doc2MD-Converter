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
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("DOC2MD_OCRMYPDF_PATH"),
            Path.Combine(appDirectory, "tools", "OCRmyPDF", "ocrmypdf.exe")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }
}

public sealed record OcrResult(bool IsSuccess, string? Text, string? ErrorMessage)
{
    public static OcrResult Success(string text) => new(true, text, null);
    public static OcrResult Fail(string message) => new(false, null, message);
}
