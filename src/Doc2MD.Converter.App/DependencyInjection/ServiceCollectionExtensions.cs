using Doc2MD.Parsers;
using Doc2MD.Services;
using Doc2MD.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Doc2MD.DependencyInjection;

/// <summary>
/// 服务注册扩展（DI 迁移 C1）。
/// 集中定义应用级服务的生命周期与依赖关系，替换散落的 new 与静态类访问。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册应用核心服务。启动时调用，构建容器并同步日志门面。
    /// </summary>
    public static IServiceProvider BuildApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, FileLoggingService>();
        services.AddSingleton<ConfigService>();

        services.AddSingleton<IParserRegistry, DocumentParserRegistry>();
        services.AddSingleton<ConversionService>();

        services.AddTransient<MainViewModel>();

        var provider = services.BuildServiceProvider();

        // 将容器中的日志实例同步到静态门面，使既有静态调用也能使用同一实例。
        LoggingService.SetLogger(provider.GetRequiredService<ILoggingService>());

        return provider;
    }
}
