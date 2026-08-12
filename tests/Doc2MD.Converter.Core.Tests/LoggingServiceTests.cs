using System.IO;
using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// 供测试使用的内存日志收集实现，验证日志调用的级别、消息与异常。
/// </summary>
public sealed class RecordingLoggingService : ILoggingService
{
    public List<(string Level, string Message, Exception? Exception)> Entries { get; } = new();

    public void Log(string level, string message, Exception? exception = null)
        => Entries.Add((level, message, exception));

    public void Info(string message) => Entries.Add(("INFO", message, null));
    public void Warning(string message) => Entries.Add(("WARN", message, null));
    public void Error(string message, Exception? ex = null) => Entries.Add(("ERROR", message, ex));
    public void Debug(string message) => Entries.Add(("DEBUG", message, null));
}

/// <summary>
/// C1: 静态服务 → DI 迁移测试。
/// 验证 ILoggingService 接口、FileLoggingService 实例实现及 LoggingService 门面的可替换性。
/// </summary>
public class LoggingServiceTests
{
    [Fact]
    public void FileLoggingService_WritesLogFileInDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Doc2MD_Test_Log_" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new FileLoggingService(dir);
            logger.Info("hello world");

            var files = Directory.GetFiles(dir, "*.log");
            Assert.NotEmpty(files);

            var content = File.ReadAllText(files[0]);
            Assert.Contains("hello world", content);
            Assert.Contains("[INFO]", content);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FileLoggingService_FormatsExceptionDetails()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Doc2MD_Test_Log_" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new FileLoggingService(dir);
            var ex = new InvalidOperationException("boom");
            logger.Error("oops", ex);

            var files = Directory.GetFiles(dir, "*.log");
            var content = File.ReadAllText(files[0]);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("oops", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("boom", content);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FileLoggingService_WritesDateBasedFileName()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Doc2MD_Test_Log_" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new FileLoggingService(dir);
            logger.Debug("marker");

            var expectedPrefix = $"app-{DateTime.Now:yyyy-MM-dd}.log";
            var file = Path.Combine(dir, expectedPrefix);
            Assert.True(File.Exists(file));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FileLoggingService_EmptyMessageStillWrites()
    {
        var dir = Path.Combine(Path.GetTempPath(), "Doc2MD_Test_Log_" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new FileLoggingService(dir);
            logger.Warning(string.Empty);

            var files = Directory.GetFiles(dir, "*.log");
            Assert.NotEmpty(files);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoggingService_SetLogger_ReplacesUnderlyingImplementation()
    {
        // 备份原门面实现，测试后恢复，避免污染其他测试
        var original = LoggingService.Logger;
        try
        {
            var recording = new RecordingLoggingService();
            LoggingService.SetLogger(recording);

            LoggingService.Info("m1");
            LoggingService.Warning("m2");
            LoggingService.Error("m3");

            Assert.Equal(3, recording.Entries.Count);
            Assert.Equal(("INFO", "m1", null), recording.Entries[0]);
            Assert.Equal(("WARN", "m2", null), recording.Entries[1]);
            Assert.Equal(("ERROR", "m3", null), recording.Entries[2]);
        }
        finally
        {
            LoggingService.SetLogger(original);
        }
    }

    [Fact]
    public void LoggingService_SetLogger_NullIsIgnored()
    {
        var original = LoggingService.Logger;
        try
        {
            var recording = new RecordingLoggingService();
            LoggingService.SetLogger(recording);

            // 传入 null 应被忽略，底层实现保持不变
            LoggingService.SetLogger(null!);

            LoggingService.Info("still-here");
            Assert.Equal(("INFO", "still-here", null), recording.Entries[0]);
        }
        finally
        {
            LoggingService.SetLogger(original);
        }
    }

    [Fact]
    public void LoggingService_Log_DelegatesLevelAndException()
    {
        var original = LoggingService.Logger;
        try
        {
            var recording = new RecordingLoggingService();
            LoggingService.SetLogger(recording);

            var ex = new IOException("disk");
            LoggingService.Log("CUSTOM", "custom-msg", ex);

            Assert.Single(recording.Entries);
            Assert.Equal("CUSTOM", recording.Entries[0].Level);
            Assert.Equal("custom-msg", recording.Entries[0].Message);
            Assert.Same(ex, recording.Entries[0].Exception);
        }
        finally
        {
            LoggingService.SetLogger(original);
        }
    }
}
