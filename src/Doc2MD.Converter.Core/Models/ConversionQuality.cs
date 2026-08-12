namespace Doc2MD.Models;

/// <summary>
/// 转换质量：警告列表 + 质量评分 + 导入建议。
/// 从 ConversionResult 拆分（C4），职责聚焦"转换结果质量评估"。
/// </summary>
public class ConversionQuality
{
    /// <summary>转换过程中产生的警告列表</summary>
    public List<ConversionWarning> Warnings { get; set; } = [];

    /// <summary>Markdown 内容中的 block 数量（后处理阶段填充）</summary>
    public int BlockCount { get; set; }

    /// <summary>不支持的对象数量（后处理阶段填充）</summary>
    public int UnsupportedObjectCount { get; set; }

    /// <summary>质量评分（0.0 - 1.0）</summary>
    public double QualityScore { get; set; } = 1.0;

    /// <summary>文档语言</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>导入建议等级（v2.0 新增）：recommended | review | not_recommended</summary>
    public string ImportRecommendation { get; set; } = "recommended";
}
