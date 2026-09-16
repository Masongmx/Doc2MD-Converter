namespace Doc2MD.Humanizer;

/// <summary>
/// Markdown 可读性增强配置选项
/// </summary>
public class HumanizerOptions
{
    /// <summary>是否全局启用可读性增强（默认 false，保证纯 AI 路径零回归）</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>是否启用长文档自动 TOC 目录生成</summary>
    public bool EnableTocGenerator { get; set; } = true;

    /// <summary>自动触发 TOC 的标题数量阈值（>= 4 个标题）</summary>
    public int TocHeadingThreshold { get; set; } = 4;

    /// <summary>是否启用标题跳级修复与规范化（例如 # 直接跳 ###）</summary>
    public bool EnableHeadingNormalizer { get; set; } = true;

    /// <summary>是否启用表格对齐与列宽优化</summary>
    public bool EnableTableFormatter { get; set; } = true;

    /// <summary>是否启用嵌套列表规范化</summary>
    public bool EnableListNormalizer { get; set; } = true;

    /// <summary>是否启用公文关键要素（发文字号、发文机关、成文日期）加粗高亮</summary>
    public bool EnableDocHighlight { get; set; } = true;
}
