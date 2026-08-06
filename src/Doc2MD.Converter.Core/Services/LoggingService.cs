using System.IO;

namespace Doc2MD.Services;

public static class LoggingService
{
    private static readonly object Lock = new();

    public static void Log(string level, string message, Exception? exception = null)
    {
        try
        {
            var logEntry = FormatEntry(level, message, exception);
            lock (Lock)
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                var logFilePath = Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");
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
            entry += Environment.NewLine + $"  Exception: {exception.GetType().Name}: {exception.Message}";
            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                entry += Environment.NewLine + $"  StackTrace: {exception.StackTrace}";
            }
        }

        return entry;
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warning(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null) => Log("ERROR", message, ex);
    public static void Debug(string message) => Log("DEBUG", message);
}
