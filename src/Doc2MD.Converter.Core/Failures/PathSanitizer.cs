using System.IO;
using System.Text.RegularExpressions;

namespace Doc2MD.Failures;

/// <summary>
/// 路径与隐私脱敏工具
/// </summary>
public static class PathSanitizer
{
    private static readonly Regex WindowsUserPathRegex = new(
        @"([a-zA-Z]:[\\/](?:Users|用户)[\\/])([^\\/]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 对路径中的用户名进行匿名化处理（例如 C:\Users\Alice\docs -> C:\Users\***\docs）
    /// </summary>
    public static string Anonymize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        return WindowsUserPathRegex.Replace(path, "$1***");
    }

    /// <summary>
    /// 仅保留目录结构层级与文件名
    /// </summary>
    public static string SanitizeDirectory(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;
        try
        {
            var dir = Path.GetDirectoryName(fullPath) ?? string.Empty;
            var fileName = Path.GetFileName(fullPath);
            return Path.Combine(Anonymize(dir), fileName);
        }
        catch
        {
            return Anonymize(fullPath);
        }
    }
}
