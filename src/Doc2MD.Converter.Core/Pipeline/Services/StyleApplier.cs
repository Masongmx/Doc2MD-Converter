using Doc2MD.Pipeline.Models;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// 极简样式映射器：Phase 1 仅允许静态映射。
/// 将 SemanticDocument 中的块类型映射为 Word 样式 ID 和格式参数。
/// 禁止用户自定义 mapping、动态规则、配置化扩展（Phase 2 才允许）。
/// </summary>
public static class StyleApplier
{
    /// <summary>标题级别 → Word 样式 ID 静态映射</summary>
    public static string GetHeadingStyleId(int level) => level switch
    {
        1 => "Heading1",
        2 => "Heading2",
        3 => "Heading3",
        _ => "Heading4"
    };

    /// <summary>段落块 → Word 样式 ID</summary>
    public static string GetParagraphStyleId() => "Normal";

    /// <summary>获取标题级别的字体/字号/加粗参数（从 DocxFormattingOptions 读取）</summary>
    public static (string font, double sizePt, bool bold) GetHeadingFormat(int level, DocxFormattingOptions opts) => level switch
    {
        1 => (opts.TitleFont, opts.TitleFontSizePt, opts.TitleBold),
        2 => (opts.Heading1Font, opts.Heading1FontSizePt, opts.Heading1Bold),
        3 => (opts.Heading2Font, opts.Heading2FontSizePt, opts.Heading2Bold),
        4 => (opts.Heading3Font, opts.Heading3FontSizePt, opts.Heading3Bold),
        _ => (opts.Heading4Font, opts.Heading4FontSizePt, opts.Heading4Bold)
    };

    /// <summary>获取标题级别的缩进参数</summary>
    public static (double indentChars, double fontSizePt) GetHeadingIndent(int level, DocxFormattingOptions opts) => level switch
    {
        1 => (0, opts.TitleFontSizePt),   // 文件标题不缩进
        2 => (opts.Heading1IndentChars, opts.Heading1FontSizePt),
        3 => (opts.Heading2IndentChars, opts.Heading2FontSizePt),
        4 => (opts.Heading3IndentChars, opts.Heading3FontSizePt),
        _ => (opts.Heading4IndentChars, opts.Heading4FontSizePt)
    };

    /// <summary>获取排版方案中标题级别的前后间距（twips）</summary>
    public static (int beforeTwips, int afterTwips) GetHeadingSpacing(int level) => level switch
    {
        1 => (0, 160),
        2 => (160, 80),
        3 => (120, 60),
        4 => (80, 40),
        _ => (80, 40)
    };

    /// <summary>标题对齐方式：H1 居中，其余左对齐</summary>
    public static string GetHeadingAlignment(int level) => level == 1 ? "center" : "left";
}
