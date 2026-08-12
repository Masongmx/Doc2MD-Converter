using Doc2MD.Models;

namespace Doc2MD.Pipeline.Models;

/// <summary>
/// 统一 Word 模板实体：替代 FormattingProfile + MarkdownToWordProfile 的分裂结构。
/// 所有排版决策完全由 DocxTemplate.Options (DocxFormattingOptions) 决定。
/// </summary>
public class DocxTemplate
{
    /// <summary>模板唯一标识，如 "official-report"</summary>
    public string Id { get; init; } = "";

    /// <summary>模板名称（等同于旧 MarkdownToWordProfile.DisplayName）</summary>
    public string Name { get; init; } = "";

    /// <summary>模板类型：内置 / 用户</summary>
    public DocxTemplateType Type { get; init; } = DocxTemplateType.BuiltIn;

    /// <summary>模板元数据（描述、标签等）</summary>
    public DocxTemplateMetadata Metadata { get; init; } = new();

    /// <summary>唯一格式来源：所有排版参数均来自此对象</summary>
    public DocxFormattingOptions Options { get; init; } = new();

    // ==== 内置模板工厂方法 ====

    /// <summary>
    /// 正式报告模板（默认）：参照 GB/T 9704-2012 标准。
    /// 合并旧 official-report (md2docx) + official-basic (quick-format)。
    /// </summary>
    public static DocxTemplate OfficialReport() => new()
    {
        Id = "official-report",
        Name = "正式报告",
        Type = DocxTemplateType.BuiltIn,
        Metadata = new DocxTemplateMetadata
        {
            DisplayName = "正式报告",
            Description = "适合工作总结、调研报告、汇报材料等正式文档。参照 GB/T 9704-2012 标准。统一字体、标题、段落、行距和页边距。",
            Version = "1.0",
            IsBuiltIn = true,
            Tags = new[] { "报告", "公文" },
            PreviewText = "适合工作总结、调研报告、汇报材料等正式文档。"
        },
        Options = DocxFormattingOptions.OfficialBasic()
    };

    /// <summary>
    /// 会议纪要模板：使用国标字体/字号体系，但页边距为常规 Word 默认值（1英寸）。
    /// </summary>
    public static DocxTemplate MeetingMinutes() => new()
    {
        Id = "meeting-minutes",
        Name = "会议纪要",
        Type = DocxTemplateType.BuiltIn,
        Metadata = new DocxTemplateMetadata
        {
            DisplayName = "会议纪要",
            Description = "适合会议记录、会议纪要和事项整理。使用国标字体但边距更宽松。",
            Version = "1.0",
            IsBuiltIn = true,
            Tags = new[] { "会议", "纪要" },
            PreviewText = "适合会议记录、会议纪要和事项整理。"
        },
        Options = DocxFormattingOptions.GeneralDocument()
    };

    /// <summary>
    /// 巡察文档模板（与企业增强版一致）：与 GB/T 9704-2012 存在差异的独立规范。
    /// 标题小一号方正小标宋简体，正文小二号方正仿宋简体，
    /// 行距31磅，页边距3.2/3.2/2.5/2.5cm，21行x24字，字间距加宽0.4磅。
    /// </summary>
    public static DocxTemplate InspectionReport() => new()
    {
        Id = "inspection-report",
        Name = "巡察文档模板",
        Type = DocxTemplateType.BuiltIn,
        Metadata = new DocxTemplateMetadata
        {
            DisplayName = "巡察文档模板",
            Description = "标题小一号方正小标宋简体，正文小二号方正仿宋简体，行距31磅，页边距3.2/3.2/2.5/2.5cm，21行×24字，字间距加宽0.4磅。与 GB/T 9704-2012 存在差异。",
            Version = "1.0",
            IsBuiltIn = true,
            Tags = new[] { "巡察", "报告" },
            PreviewText = "巡察报告专用排版方案，适合巡察文档的特定格式要求。"
        },
        Options = DocxFormattingOptions.EnterpriseEnhanced()
    };
}

/// <summary>模板类型枚举</summary>
public enum DocxTemplateType
{
    /// <summary>内置模板</summary>
    BuiltIn,
    /// <summary>用户自定义模板（Phase 2 支持）</summary>
    User
}

/// <summary>模板元数据：展示信息、分类标签等</summary>
public class DocxTemplateMetadata
{
    /// <summary>用户可见的显示名称</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>模板适用场景描述</summary>
    public string Description { get; init; } = "";

    /// <summary>模板版本号</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>是否为内置模板</summary>
    public bool IsBuiltIn { get; init; } = true;

    /// <summary>标签列表（用于后续筛选或分类）</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>预览文本：模板适用场景的简要说明</summary>
    public string PreviewText { get; init; } = "";
}
