using System.Globalization;
using System.Resources;

namespace Doc2MD.Services;

/// <summary>
/// 多语言资源管理服务。通过 ResourceManager 从 .resx 文件中读取本地化字符串。
/// 默认使用中文（zh-CN），支持运行时切换语言并通知所有订阅者刷新 UI。
/// </summary>
public static class LanguageService
{
    private static readonly ResourceManager ResourceManager = new("Doc2MD.Resources.Strings", typeof(LanguageService).Assembly);
    private static CultureInfo _currentCulture = new("zh-CN");

    /// <summary>语言变更时触发，UI 层可订阅以刷新绑定文本。</summary>
    public static event Action? LanguageChanged;

    public static CultureInfo CurrentCulture => _currentCulture;

    /// <summary>获取指定键的本地化字符串。</summary>
    public static string GetString(string key)
    {
        return ResourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
    }

    /// <summary>获取格式化后的本地化字符串。</summary>
    public static string GetFormatted(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(_currentCulture, template, args);
        }
        catch
        {
            return template;
        }
    }

    /// <summary>切换语言并通知所有订阅者刷新。</summary>
    public static void SetLanguage(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        if (_currentCulture.Name == culture.Name) return;

        _currentCulture = culture;
        LanguageChanged?.Invoke();
    }
}
