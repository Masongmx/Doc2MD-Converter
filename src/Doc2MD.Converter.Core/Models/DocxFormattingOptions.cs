using Doc2MD.Constants;

namespace Doc2MD.Models;

/// <summary>
/// 统一排版选项模型：涵盖所有可配置的排版属性。
/// Pipeline（Markdown→DOCX）与 FormatDoc（一键排版）模式共用此类，
/// 通过工厂方法（<see cref="OfficialBasic"/> / <see cref="EnterpriseEnhanced"/> / <see cref="GeneralDocument"/>）
/// 区分使用场景。默认值对应 GB/T 9704-2012 公文格式。
/// </summary>
public class DocxFormattingOptions
{
    /// <summary>排版方案名称</summary>
    public string SchemeName { get; set; } = "公文格式（基础）";

    // ---- 公文标题（文件标题，如"关于XXX的通知"） ----

    /// <summary>公文标题字体</summary>
    public string TitleFont { get; set; } = "方正小标宋简体";

    /// <summary>公文标题字号（pt）。GB/T 9704: 二号 = 22pt</summary>
    public double TitleFontSizePt { get; set; } = 22.0;

    /// <summary>公文标题是否加粗</summary>
    public bool TitleBold { get; set; } = true;

    // ---- 正文字体与字号 ----

    /// <summary>正文字体</summary>
    public string BodyFont { get; set; } = "仿宋_GB2312";

    /// <summary>正文字号（pt）。GB/T 9704: 三号 = 16pt</summary>
    public double BodyFontSizePt { get; set; } = 16.0;

    // ---- 结构层次标题字体（GB/T 9704 7.3.3："第一层黑体、第二层楷体、第三四层仿宋"） ----

    /// <summary>一级标题字体（"一、"）。国标：黑体</summary>
    public string Heading1Font { get; set; } = "黑体";

    /// <summary>一级标题字号（pt）。国标：与正文同字号 = 16pt</summary>
    public double Heading1FontSizePt { get; set; } = 16.0;

    /// <summary>一级标题是否加粗。国标未要求加粗，黑体字本身视觉偏重</summary>
    public bool Heading1Bold { get; set; } = false;

    /// <summary>一级标题首行缩进字符数。国标 7.3.3："每个自然段左空二字"</summary>
    public double Heading1IndentChars { get; set; } = 2.0;

    /// <summary>二级标题字体（"（一）"）。国标：楷体</summary>
    public string Heading2Font { get; set; } = "楷体_GB2312";

    /// <summary>二级标题字号（pt）。国标：与正文同字号 = 16pt</summary>
    public double Heading2FontSizePt { get; set; } = 16.0;

    /// <summary>二级标题是否加粗</summary>
    public bool Heading2Bold { get; set; } = false;

    /// <summary>二级标题首行缩进字符数。国标：左空二字</summary>
    public double Heading2IndentChars { get; set; } = 2.0;

    /// <summary>三级标题字体（"1."）。国标：仿宋</summary>
    public string Heading3Font { get; set; } = "仿宋_GB2312";

    /// <summary>三级标题字号（pt）。国标：与正文同字号 = 16pt</summary>
    public double Heading3FontSizePt { get; set; } = 16.0;

    /// <summary>三级标题是否加粗</summary>
    public bool Heading3Bold { get; set; } = false;

    /// <summary>三级标题首行缩进字符数</summary>
    public double Heading3IndentChars { get; set; } = 2.0;

    /// <summary>四级及以下标题字体（"（1）"）。国标：仿宋</summary>
    public string Heading4Font { get; set; } = "仿宋_GB2312";

    /// <summary>四级及以下标题字号（pt）</summary>
    public double Heading4FontSizePt { get; set; } = 16.0;

    /// <summary>四级及以下标题是否加粗</summary>
    public bool Heading4Bold { get; set; } = false;

    /// <summary>四级及以下标题首行缩进字符数</summary>
    public double Heading4IndentChars { get; set; } = 2.0;

    // ---- Markdown 标题字体（FormatDoc 模式，如 "## 章节"） ----

    /// <summary>Markdown 二级标题（"##"）字体。国标：黑体</summary>
    public string HeadingFont { get; set; } = Gb9704Constants.HeadingFont;

    /// <summary>Markdown 二级标题字号（pt）。国标：三号 = 16pt</summary>
    public double HeadingFontSizePt { get; set; } = Gb9704Constants.HeadingFontSize / 2.0;

    /// <summary>Markdown 三级标题（"###"）字体。国标：楷体</summary>
    public string SubheadingFont { get; set; } = Gb9704Constants.SubheadingFont;

    /// <summary>Markdown 三级标题字号（pt）。国标：三号 = 16pt</summary>
    public double SubheadingFontSizePt { get; set; } = Gb9704Constants.SubheadingFontSize / 2.0;

    // ---- 代码块 ----

    /// <summary>代码块字体</summary>
    public string CodeBlockFont { get; set; } = "Consolas";

    /// <summary>代码块字号（pt）</summary>
    public double CodeBlockFontSizePt { get; set; } = 10.5;

    // ---- 重点强调 ----

    /// <summary>重点强调字体。国标无此规定，部分企业标准要求黑体强调</summary>
    public string EmphasisFont { get; set; } = "黑体";

    /// <summary>重点强调字号（pt），0 表示与正文同字号</summary>
    public double EmphasisFontSizePt { get; set; } = 0.0;

    // ---- 行距与字间距 ----

    /// <summary>固定行距（pt）。GB/T 9704: 28pt</summary>
    public double LineSpacingPt { get; set; } = 28.0;

    /// <summary>字间距加宽量（pt）。0 = 不加宽。部分企业标准要求 0.4pt</summary>
    public double CharSpacingPt { get; set; } = 0.0;

    // ---- 正文缩进 ----

    /// <summary>正文首行缩进字符数。国标 7.3.3："每个自然段左空二字" = 2</summary>
    public double FirstLineIndentChars { get; set; } = 2.0;

    // ---- 页边距（cm） ----

    /// <summary>上边距（cm）。GB/T 9704: 3.7cm</summary>
    public double PageMarginTopCm { get; set; } = 3.7;

    /// <summary>下边距（cm）。GB/T 9704: 3.5cm</summary>
    public double PageMarginBottomCm { get; set; } = 3.5;

    /// <summary>左边距（cm）。GB/T 9704: 2.8cm</summary>
    public double PageMarginLeftCm { get; set; } = 2.8;

    /// <summary>右边距（cm）。GB/T 9704: 2.6cm</summary>
    public double PageMarginRightCm { get; set; } = 2.6;

    // ---- 页码 ----

    /// <summary>页码字号（pt）。GB/T 9704: 四号 = 14pt</summary>
    public double PageNumberFontSizePt { get; set; } = 14.0;

    /// <summary>页码字体</summary>
    public string PageNumberFont { get; set; } = "宋体";

    /// <summary>页码对齐方式："center" | "left" | "right" | "alternate"（国标：左右交替）</summary>
    public string PageNumberAlignment { get; set; } = "alternate";

    // ---- 文档网格 ----

    /// <summary>每页行数。GB/T 9704: 22</summary>
    public int LinesPerPage { get; set; } = 22;

    /// <summary>每行字数。GB/T 9704: 28</summary>
    public int CharsPerLine { get; set; } = 28;

    // ---- 段前段后间距 ----

    /// <summary>段前间距（pt），默认 0</summary>
    public double BeforeSpacingPt { get; set; } = 0.0;

    /// <summary>段后间距（pt），默认 0</summary>
    public double AfterSpacingPt { get; set; } = 0.0;

    // ==== 工厂方法 ====

    /// <summary>创建默认排版选项（GB/T 9704-2012 公文格式）</summary>
    public static DocxFormattingOptions Default() => OfficialBasic();

    /// <summary>创建 GB/T 9704-2012 公文格式排版选项</summary>
    public static DocxFormattingOptions OfficialBasic() => new()
    {
        SchemeName = "公文格式（基础）"
    };

    /// <summary>
    /// 创建巡察文档模板排版选项（与 GB/T 9704-2012 有冲突的独立规范）。
    /// 标题小一号(24pt)方正小标宋简体，正文小二号(18pt)方正仿宋简体，
    /// 行距31磅，页边距3.2/3.2/2.5/2.5cm，21行×24字，字间距加宽0.4磅。
    /// </summary>
    public static DocxFormattingOptions EnterpriseEnhanced() => new()
    {
        SchemeName = "巡察文档模板",
        // 公文标题
        TitleFont = "方正小标宋简体",
        TitleFontSizePt = 24.0,  // 小一号
        TitleBold = true,
        // 正文
        BodyFont = "方正仿宋简体",
        BodyFontSizePt = 18.0,   // 小二号
        // 一级标题
        Heading1Font = "方正黑体简体",
        Heading1FontSizePt = 18.0,
        Heading1Bold = false,
        Heading1IndentChars = 2.0,
        // 二级标题
        Heading2Font = "方正楷体简体",
        Heading2FontSizePt = 18.0,
        Heading2Bold = false,
        Heading2IndentChars = 2.0,
        // 三级标题
        Heading3Font = "方正仿宋简体",
        Heading3FontSizePt = 18.0,
        Heading3Bold = false,
        Heading3IndentChars = 2.0,
        // 四级及以下
        Heading4Font = "方正仿宋简体",
        Heading4FontSizePt = 18.0,
        Heading4Bold = false,
        Heading4IndentChars = 2.0,
        // 重点强调
        EmphasisFont = "方正黑体简体",
        EmphasisFontSizePt = 18.0,
        // 行距与字间距
        LineSpacingPt = 31.0,
        CharSpacingPt = 0.4,
        // 页边距
        PageMarginTopCm = 3.2,
        PageMarginBottomCm = 3.2,
        PageMarginLeftCm = 2.5,
        PageMarginRightCm = 2.5,
        // 页码
        PageNumberFontSizePt = 14.0,
        PageNumberFont = "宋体",
        PageNumberAlignment = "center",
        // 文档网格
        LinesPerPage = 21,
        CharsPerLine = 24
    };

    /// <summary>
    /// 创建普通文档排版选项（1英寸边距，适合会议纪要等非公文场景）。
    /// 使用国标字体/字号体系，但页边距为常规 Word 默认值。
    /// </summary>
    public static DocxFormattingOptions GeneralDocument() => new()
    {
        SchemeName = "普通文档",
        // 页边距：1英寸 = 2.54cm
        PageMarginTopCm = 2.54,
        PageMarginBottomCm = 2.54,
        PageMarginLeftCm = 2.54,
        PageMarginRightCm = 2.54,
        // 文档网格
        LinesPerPage = 22,
        CharsPerLine = 28
    };

    /// <summary>
    /// 根据字号（pt）计算首行缩进的 twips 值。
    /// 1字符 = 当前字号宽度，1pt = 20twips。
    /// </summary>
    public int CalcIndentTwips(double indentChars, double fontSizePt)
    {
        return (int)Math.Round(indentChars * fontSizePt * 20.0);
    }
}
