namespace Doc2MD.Services;

/// <summary>
/// 日志服务接口。将日志写入与调用方解耦，支持通过 DI 注入替换实现，
/// 便于单元测试隔离（可注入内存/空实现）与未来扩展（如异步队列写入）。
/// </summary>
public interface ILoggingService
{
    void Log(string level, string message, Exception? exception = null);
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? ex = null);
    void Debug(string message);
}
