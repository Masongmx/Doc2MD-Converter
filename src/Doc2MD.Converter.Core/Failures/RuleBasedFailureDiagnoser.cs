using System.IO;

namespace Doc2MD.Failures;

/// <summary>
/// 基于规则的失败根因诊断引擎
/// </summary>
public class RuleBasedFailureDiagnoser : IFailureDiagnoser
{
    public FailureContext Diagnose(Exception? ex, string? filePath, string? errorMessage = null)
    {
        var category = DetermineCategory(ex, filePath, errorMessage);

        return new FailureContext
        {
            SourceFilePath = filePath,
            SourceFileName = filePath != null ? Path.GetFileName(filePath) : null,
            Category = category,
            ExceptionType = ex?.GetType().FullName,
            ErrorMessage = errorMessage ?? ex?.Message ?? category.GetDescription(),
            StackTrace = ex?.StackTrace,
            SuggestedAction = category.GetSuggestedAction(),
            IsRetryable = category.IsRetryable(),
            OccurredAt = DateTime.UtcNow
        };
    }

    private static FailureCategory DetermineCategory(Exception? ex, string? filePath, string? errorMessage)
    {
        if (ex is OperationCanceledException)
        {
            return FailureCategory.UserCancelled;
        }

        var fullMsg = $"{errorMessage} {ex?.Message} {ex?.InnerException?.Message}".ToLowerInvariant();

        // 1. 用户取消
        if (fullMsg.Contains("cancel") || fullMsg.Contains("已取消") || fullMsg.Contains("operation was canceled"))
        {
            return FailureCategory.UserCancelled;
        }

        // 2. 文件锁定 / IO 占用
        if (fullMsg.Contains("being used by another process") ||
            fullMsg.Contains("正由另一进程使用") ||
            fullMsg.Contains("另一个程序正在使用此文件") ||
            fullMsg.Contains("进程无法访问") ||
            fullMsg.Contains("cannot access the file") ||
            (ex is IOException && (fullMsg.Contains("used") || fullMsg.Contains("lock") || fullMsg.Contains("占用") || fullMsg.Contains("access"))))
        {
            return FailureCategory.FileLocked;
        }

        // 3. 加密 / 密码保护
        if (fullMsg.Contains("encrypt") || fullMsg.Contains("password") || fullMsg.Contains("crypt") || fullMsg.Contains("密码") || fullMsg.Contains("加密"))
        {
            return FailureCategory.SourceFileEncrypted;
        }

        // 4. 依赖缺失（LibreOffice / Tesseract）
        if (fullMsg.Contains("libreoffice") && (fullMsg.Contains("未安装") || fullMsg.Contains("不可用") || fullMsg.Contains("not found")))
        {
            return FailureCategory.DependencyMissing;
        }
        if (fullMsg.Contains("ocrmypdf") || fullMsg.Contains("tesseract") && (fullMsg.Contains("not found") || fullMsg.Contains("未找到")))
        {
            return FailureCategory.DependencyMissing;
        }

        // 5. 资源耗尽
        if (ex is OutOfMemoryException || fullMsg.Contains("out of memory") || fullMsg.Contains("disk full") || fullMsg.Contains("空间不足"))
        {
            return FailureCategory.ResourceExhausted;
        }

        // 6. 格式不支持
        if (fullMsg.Contains("unsupported") || fullMsg.Contains("不支持") || fullMsg.Contains("unknown format"))
        {
            return FailureCategory.FormatUnsupported;
        }

        // 7. 损坏或解析失败（OpenXmlPackageException、PdfPig 解析错误等）
        if (fullMsg.Contains("corrupt") || fullMsg.Contains("损坏") || fullMsg.Contains("bad format") || fullMsg.Contains("invalid") || fullMsg.Contains("xml") || fullMsg.Contains("eof"))
        {
            return FailureCategory.SourceFileCorrupted;
        }

        // 8. 默认回退为内部异常
        return FailureCategory.InternalException;
    }
}
