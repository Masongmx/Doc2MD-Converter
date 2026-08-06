namespace Doc2MD.Constants;

/// <summary>
/// 全局版本号常量。所有输出（frontmatter、meta.json、日志）统一引用此常量。
/// </summary>
public static class AppVersion
{
    public const string Version = "2.0.0";

    /// <summary>对外显示的完整版本标识</summary>
    public static string FullString => $"Doc2MD Converter v{Version}";

    /// <summary>converter 字段用于 frontmatter 和 meta.json</summary>
    public static string Converter => FullString;
}
