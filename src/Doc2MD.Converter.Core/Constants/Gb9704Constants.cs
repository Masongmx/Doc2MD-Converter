namespace Doc2MD.Constants;

using DocumentFormat.OpenXml.Wordprocessing;

/// <summary>
/// GB/T 9704-2012 党政机关公文格式标准常量
/// </summary>
public static class Gb9704Constants
{
    // 字体名称
    public const string BodyFont = "仿宋_GB2312";
    public const string TitleFont = "方正小标宋简体";
    public const string HeadingFont = "黑体";
    public const string SubheadingFont = "楷体_GB2312";

    // 字号（半磅单位，三号=16磅=32半磅）
    public const int BodyFontSize = 32;        // 三号字
    public const int TitleFontSize = 44;       // 二号字
    public const int HeadingFontSize = 36;      // 三号字加粗
    public const int SubheadingFontSize = 32;   // 三号字

    // 行间距（固定值28磅 = 28 * 20 = 560 twips）
    public const string LineSpacing = "560";
    public const string AutoLineSpacing = "528";

    // 首行缩进（2个中文字符）
    public const int FirstLineIndent = 560;     // twips

    // 段落间距
    public const string BeforeSpacing = "0";
    public const string AfterSpacing = "0";

    // 标题级别对应的格式（唯一真相来源）
    public static readonly Dictionary<int, HeadingFormat> HeadingFormats = new()
    {
        { 1, new(TitleFont, TitleFontSize, JustificationValues.Center, "240") },
        { 2, new(HeadingFont, HeadingFontSize, JustificationValues.Left, "160") },
        { 3, new(SubheadingFont, SubheadingFontSize, JustificationValues.Left, "120") },
        { 4, new(BodyFont, BodyFontSize, JustificationValues.Left, "80") }
    };

    /// <summary>
    /// 获取标题格式，级别超出范围时返回正文格式
    /// </summary>
    public static HeadingFormat GetHeadingFormat(int level)
    {
        return HeadingFormats.GetValueOrDefault(level, new(BodyFont, BodyFontSize, JustificationValues.Left, "80"));
    }
}

/// <summary>
/// 标题格式定义
/// </summary>
public record HeadingFormat(string Font, int Size, JustificationValues Alignment, string BeforeSpacing);
