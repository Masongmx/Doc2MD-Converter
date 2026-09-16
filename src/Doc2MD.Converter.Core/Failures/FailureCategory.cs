using System.ComponentModel;

namespace Doc2MD.Failures;

/// <summary>
/// 失败分类枚举（8 大典型根因类型）
/// </summary>
public enum FailureCategory
{
    /// <summary>源文件损坏（如解析异常、结构损坏，可尝试 OCR 兜底）</summary>
    [Description("源文件损坏")]
    SourceFileCorrupted = 1,

    /// <summary>源文件已加密/受密码保护（不可无脑重试，需用户解密）</summary>
    [Description("源文件加密")]
    SourceFileEncrypted = 2,

    /// <summary>格式异常或当前不支持（如伪装成 docx 的二进制）</summary>
    [Description("格式不支持")]
    FormatUnsupported = 3,

    /// <summary>外部依赖缺失（如未安装 LibreOffice 或 Tesseract）</summary>
    [Description("依赖缺失")]
    DependencyMissing = 4,

    /// <summary>程序内部未捕获异常/解析逻辑 bug</summary>
    [Description("内部异常")]
    InternalException = 5,

    /// <summary>用户主动取消转换</summary>
    [Description("用户取消")]
    UserCancelled = 6,

    /// <summary>系统资源不足（内存不足、磁盘满等）</summary>
    [Description("资源不足")]
    ResourceExhausted = 7,

    /// <summary>文件被其他进程锁定（如 Word 正在打开编辑）</summary>
    [Description("文件被占用")]
    FileLocked = 8
}

/// <summary>
/// 失败分类元数据扩展
/// </summary>
public static class FailureCategoryExtensions
{
    public static string GetDescription(this FailureCategory category) => category switch
    {
        FailureCategory.SourceFileCorrupted => "源文件损坏，无法通过常规解析器读取",
        FailureCategory.SourceFileEncrypted => "源文件受密码保护或已被加密",
        FailureCategory.FormatUnsupported => "文档格式不受支持或文件损坏严重",
        FailureCategory.DependencyMissing => "缺少必要的外部转换工具或运行环境",
        FailureCategory.InternalException => "转换引擎内部发生未处理的逻辑异常",
        FailureCategory.UserCancelled => "转换操作已由用户手动取消",
        FailureCategory.ResourceExhausted => "系统内存或磁盘空间不足",
        FailureCategory.FileLocked => "文件正在被其他程序（如 Word/Excel）独占占用",
        _ => "未知失败原因"
    };

    public static bool IsRetryable(this FailureCategory category) => category switch
    {
        FailureCategory.SourceFileCorrupted => true,   // 可通过 OCR 降级重试
        FailureCategory.FileLocked => true,            // 可等待解锁后重试
        FailureCategory.ResourceExhausted => true,     // 可降级单线程并发后重试
        FailureCategory.InternalException => true,     // 可安全重试 1 次
        _ => false                                     // 加密、不支持、依赖缺失、取消等无需盲目自动重试
    };

    public static string GetSuggestedAction(this FailureCategory category) => category switch
    {
        FailureCategory.SourceFileCorrupted => "已自动尝试 OCR 兜底提取；若仍失败，建议检查源文件完整性",
        FailureCategory.SourceFileEncrypted => "请先使用 PDF 阅读器或 Office 解除密码保护后重新添加",
        FailureCategory.FormatUnsupported => "请确认文件扩展名与真实格式是否一致，或查看支持格式列表",
        FailureCategory.DependencyMissing => "旧格式文件需依赖 LibreOffice 运行环境，请先安装或下载完整安装包",
        FailureCategory.InternalException => "建议稍后重试；若持续失败，可右键复制错误信息向开发者反馈",
        FailureCategory.UserCancelled => "操作已中止，可随时再次点击生成按钮",
        FailureCategory.ResourceExhausted => "建议关闭其他高负载软件，或在设置中降低并发转换线程数",
        FailureCategory.FileLocked => "请先关闭正在查看该文件的 Word/Excel 等软件后再行转换",
        _ => "请检查文件后重试"
    };
}
