namespace Doc2MD.Services;

/// <summary>
/// 日志静态门面。作为既有静态调用的兼容入口，内部委托给可替换的
/// <see cref="ILoggingService"/> 实例。DI 容器可在启动时将实例注入门面
/// （<see cref="SetLogger"/>），新代码应优先通过构造函数注入 <see cref="ILoggingService"/>。
/// </summary>
public static class LoggingService
{
    private static ILoggingService _logger = new FileLoggingService();
    private static readonly object Lock = new();

    /// <summary>
    /// 替换门面底层实现。由 DI 容器在启动时调用，将容器注册的
    /// <see cref="ILoggingService"/> 实例同步到门面，使既有静态调用也能使用注入的实现。
    /// </summary>
    public static void SetLogger(ILoggingService logger)
    {
        if (logger == null) return;
        lock (Lock)
        {
            _logger = logger;
        }
    }

    /// <summary>
    /// 当前门面底层实现。供测试、诊断及 DI 容器获取已注册的实例以注入其他服务。
    /// </summary>
    public static ILoggingService Logger
    {
        get
        {
            lock (Lock)
            {
                return _logger;
            }
        }
    }

    public static void Log(string level, string message, Exception? exception = null)
        => Logger.Log(level, message, exception);

    public static void Info(string message) => Logger.Info(message);
    public static void Warning(string message) => Logger.Warning(message);
    public static void Error(string message, Exception? ex = null) => Logger.Error(message, ex);
    public static void Debug(string message) => Logger.Debug(message);
}
