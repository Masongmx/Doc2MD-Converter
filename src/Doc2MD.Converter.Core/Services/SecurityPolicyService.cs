using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 安全策略服务：验证操作是否符合安全策略
/// 核心安全承诺：
/// - 默认不修改源文件
/// - 输出目录与源目录隔离
/// - 文件名净化（防止路径穿越）
/// - 覆盖保护
/// </summary>
public static class SecurityPolicyService
{
    /// <summary>
    /// 验证文件路径是否在允许的目录内。
    /// AllowedDirectories 为空时：本地上下文不限制，非本地上下文拒绝所有。
    /// 使用规范化路径 + 目录分隔符后缀防止 StartsWith 绕过。
    /// </summary>
    public static bool IsPathAllowed(string path, SecurityPolicy policy)
    {
        if (policy.AllowedDirectories.Count == 0)
            return policy.IsLocalContext;

        var fullPath = Path.GetFullPath(path);
        var fullDir = fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;

        return policy.AllowedDirectories.Any(allowed =>
        {
            var normalizedAllowed = Path.GetFullPath(allowed);
            var allowedWithSep = normalizedAllowed.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedAllowed
                : normalizedAllowed + Path.DirectorySeparatorChar;
            return string.Equals(fullPath, normalizedAllowed, StringComparison.OrdinalIgnoreCase)
                || fullDir.StartsWith(allowedWithSep, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// 验证输出是否会覆盖已有文件
    /// </summary>
    public static bool WouldOverwrite(string outputPath, SecurityPolicy policy)
    {
        return !policy.AllowOverwrite && File.Exists(outputPath);
    }

    /// <summary>Windows 保留设备名（不可作为文件名，否则创建/访问会失败或产生安全隐患）</summary>
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// 清理文件名，防止路径穿越（去除 .. 和绝对路径组件），
    /// 并对 Windows 保留设备名（CON/NUL/AUX/COM1-9/LPT1-9）添加下划线前缀，
    /// 避免生成无法写入或指向设备端口的文件名。
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        safeName = safeName.Trim().Trim('.');

        // 含扩展名的保留名同样受限（如 "CON.txt"），需检查不含扩展名的部分
        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
        if (ReservedDeviceNames.Contains(nameWithoutExt))
        {
            safeName = "_" + safeName;
        }

        return safeName;
    }

    /// <summary>
    /// 验证输出路径与源路径是否隔离（不在同一目录树下）
    /// </summary>
    public static bool IsOutputIsolated(string sourcePath, string outputPath)
    {
        var fullSource = Path.GetFullPath(sourcePath);
        var fullOutput = Path.GetFullPath(outputPath);

        // 确保尾部有分隔符再比较
        var sourceDir = fullSource.EndsWith(Path.DirectorySeparatorChar)
            ? fullSource
            : fullSource + Path.DirectorySeparatorChar;
        var outputDir = fullOutput.EndsWith(Path.DirectorySeparatorChar)
            ? fullOutput
            : fullOutput + Path.DirectorySeparatorChar;

        // 输出不能在源目录树内，源也不能在输出目录树内
        return !fullOutput.StartsWith(sourceDir, StringComparison.OrdinalIgnoreCase)
            && !fullSource.StartsWith(outputDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证文件类型是否在允许列表内
    /// </summary>
    public static bool IsFileTypeAllowed(string filePath, SecurityPolicy policy)
    {
        if (policy.AllowedFileTypes.Count == 0)
            return true;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return policy.AllowedFileTypes.Contains(ext);
    }

    /// <summary>
    /// 验证文件大小是否在限制内
    /// </summary>
    public static bool IsFileSizeAllowed(string filePath, SecurityPolicy policy)
    {
        if (policy.MaxFileSizeBytes <= 0)
            return true;

        try
        {
            var info = new FileInfo(filePath);
            return info.Length <= policy.MaxFileSizeBytes;
        }
        catch
        {
            return false;
        }
    }
}
