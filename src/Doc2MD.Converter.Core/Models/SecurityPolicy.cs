namespace Doc2MD.Models;

/// <summary>
/// 安全策略：控制文档转换工具的行为边界
/// </summary>
public class SecurityPolicy
{
    /// <summary>离线模式：不允许访问公网</summary>
    public bool OfflineMode { get; set; } = true;

    /// <summary>不允许上传文件</summary>
    public bool AllowUpload { get; set; } = false;

    /// <summary>不允许删除源文件</summary>
    public bool AllowDeleteSource { get; set; } = false;

    /// <summary>不允许覆盖已有输出</summary>
    public bool AllowOverwrite { get; set; } = false;

    /// <summary>不在日志中记录正文内容</summary>
    public bool LogContent { get; set; } = false;

    /// <summary>允许访问的目录白名单（空=本地上下文不限制）</summary>
    public List<string> AllowedDirectories { get; set; } = [];

    /// <summary>
    /// 是否为本地上下文（GUI）。
    /// 本地上下文下 AllowedDirectories 为空时不限制路径访问。
    /// </summary>
    public bool IsLocalContext { get; set; } = true;

    /// <summary>允许的文件类型（空=全部允许）</summary>
    public List<string> AllowedFileTypes { get; set; } = [];

    /// <summary>最大文件大小（字节），0=不限</summary>
    public long MaxFileSizeBytes { get; set; } = 0;

    /// <summary>输出目录与源目录隔离</summary>
    public bool OutputIsolatedFromSource { get; set; } = true;

    /// <summary>单个文件失败不影响整批任务</summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>创建默认安全策略</summary>
    public static SecurityPolicy Default => new();
}
