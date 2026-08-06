namespace Doc2MD.Pipeline.Models;

/// <summary>
/// 语义文档模型：Markdown 解析后的结构化中间表示。
/// MarkdownConverter 只负责语义解析，不包含任何 Word 样式逻辑。
/// 所有渲染决策由 DocxTemplate + StyleApplier 在 DocxRenderer 中完成。
/// </summary>
public class SemanticDocument
{
    /// <summary>文档包含的语义块列表</summary>
    public List<SemanticBlock> Blocks { get; } = new();
}

/// <summary>语义块基类</summary>
public abstract class SemanticBlock
{
    /// <summary>块类型标识</summary>
    public abstract string BlockType { get; }
}

/// <summary>标题块（H1-H6）</summary>
public sealed class HeadingBlock : SemanticBlock
{
    public override string BlockType => "heading";

    /// <summary>标题级别 1-6</summary>
    public int Level { get; init; }

    /// <summary>原始文本内容</summary>
    public string Content { get; init; } = "";

    /// <summary>行内格式片段列表</summary>
    public List<InlineRun> Runs { get; init; } = new();
}

/// <summary>正文段落块</summary>
public sealed class ParagraphBlock : SemanticBlock
{
    public override string BlockType => "paragraph";

    /// <summary>原始文本内容</summary>
    public string Content { get; init; } = "";

    /// <summary>行内格式片段列表</summary>
    public List<InlineRun> Runs { get; init; } = new();
}

/// <summary>表格块</summary>
public sealed class TableBlock : SemanticBlock
{
    public override string BlockType => "table";

    /// <summary>表格行数据（每行为单元格列表）</summary>
    public List<List<TableCellContent>> Rows { get; init; } = new();
}

/// <summary>表格单元格内容</summary>
public class TableCellContent
{
    /// <summary>原始文本</summary>
    public string RawText { get; init; } = "";

    /// <summary>行内格式片段列表</summary>
    public List<InlineRun> Runs { get; init; } = new();
}

/// <summary>列表块（含有序和无序）</summary>
public sealed class ListBlock : SemanticBlock
{
    public override string BlockType => "list";

    /// <summary>是否为有序列表</summary>
    public bool IsOrdered { get; init; }

    /// <summary>列表项</summary>
    public List<ListItem> Items { get; init; } = new();
}

/// <summary>列表项</summary>
public class ListItem
{
    /// <summary>序号（0 = 无序）</summary>
    public int Order { get; init; }

    /// <summary>原始文本</summary>
    public string Content { get; init; } = "";

    /// <summary>行内格式片段列表</summary>
    public List<InlineRun> Runs { get; init; } = new();
}

/// <summary>引用块</summary>
public sealed class QuoteBlock : SemanticBlock
{
    public override string BlockType => "blockquote";

    /// <summary>原始文本内容（可追加合并连续引用行）</summary>
    public string Content { get; set; } = "";

    /// <summary>行内格式片段列表</summary>
    public List<InlineRun> Runs { get; init; } = new();
}

/// <summary>水平分隔线块</summary>
public sealed class HorizontalRuleBlock : SemanticBlock
{
    public override string BlockType => "horizontal_rule";
}

/// <summary>
/// 行内格式片段：一段文本 + 格式属性。
/// 从 MarkdownToDocxConverter 迁移而来，现为语义层标准类型。
/// </summary>
public class InlineRun
{
    public string Text { get; set; } = string.Empty;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Strikethrough { get; set; }
    public bool Code { get; set; }
}
