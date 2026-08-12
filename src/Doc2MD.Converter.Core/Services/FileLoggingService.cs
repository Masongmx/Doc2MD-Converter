using System.IO;

namespace Doc2MD.Services;

/// <summary>
/// 将日志写入应用数据目录下按日期分文件的日志文件。
/// 通过 <see cref="ILoggingService"/> 接口注册，供 DI 容器与单元测试使用。
/// </summary>
public sealed class FileLoggingService : ILoggingService
{
    private readonly object _lock = new();
    private readonly string _logDirectory;

    public FileLoggingService()
        : this(AppPaths.LogDirectory)
    {
    }

    /// <summary>允许测试注入自定义日志目录。</summary>
    public FileLoggingService(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    public void Log(string level, string message, Exception? exception = null)
    {
        try
        {
            var logEntry = FormatEntry(level, message, exception);
            lock (_lock)
            {
                Directory.CreateDirectory(_logDirectory);
                var logFilePath = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private static string FormatEntry(string level, string message, Exception? exception)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{level}] {message}";

        if (exception != null)
        {
            // 记录完整异常链（含 InnerException），便于排查 XAML 解析等深层错误
            entry += Environment.NewLine + $"  Exception: {exception}";
        }

        return entry;
    }

    public void Info(string message) => Log("INFO", message);
    public void Warning(string message) => Log("WARN", message);
    public void Error(string message, Exception? ex = null) => Log("ERROR", message, ex);
    public void Debug(string message) => Log("DEBUG", message);
}
