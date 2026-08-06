using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Doc2MD.Constants;

namespace Doc2MD.Models;

/// <summary>
/// 统一排版选项模型：涵盖所有可配置的排版属性。
/// 两个引擎（MarkdownToDocxParser、DocxFormatter）最终从此模型读取排版参数。
/// </summary>
public class DocxFormattingOptions
{
    // === 字体 ===
    public string TitleFont { get; set; } = Gb9704Constants.TitleFont;
    public string HeadingFont { get; set; } = Gb9704Constants.HeadingFont;
    public string SubheadingFont { get; set; } = Gb9704Constants.SubheadingFont;
    public string BodyFont { get; set; } = Gb9704Constants.BodyFont;
    public string CodeBlockFont { get; set; } = "Consolas";

    // === 字号（磅） ===
    public double TitleFontSizePt { get; set; } = Gb9704Constants.TitleFontSize / 2.0;
    public double HeadingFontSizePt { get; set; } = Gb9704Constants.HeadingFontSize / 2.0;
    public double SubheadingFontSizePt { get; set; } = Gb9704Constants.SubheadingFontSize / 2.0;
    public double BodyFontSizePt { get; set; } = Gb9704Constants.BodyFontSize / 2.0;
    public double CodeBlockFontSizePt { get; set; } = 10.5;

    // === 间距 ===
    public double LineSpacingPt { get; set; } = 28.0;
    public double FirstLineIndentChars { get; set; } = 2.0;
    public double BeforeSpacingPt { get; set; } = 0;
    public double AfterSpacingPt { get; set; } = 0;

    // === 页边距（cm） ===
    public double PageMarginTopCm { get; set; } = 3.7;
    public double PageMarginBottomCm { get; set; } = 3.5;
    public double PageMarginLeftCm { get; set; } = 2.8;
    public double PageMarginRightCm { get; set; } = 2.6;
}

/// <summary>
/// 排版方案：一个命名 + 描述 + 完整排版选项。
/// 内置方案不可修改（只读），自定义方案可保存/加载为 JSON 文件。
/// </summary>
public class FormattingProfile
{
    public const string StandardOfficial = "标准公文格式";
    public const string EnterpriseEnhanced = "企业增强版";
    public const string AcademicThesis = "学术论文格式";
    public const string Custom = "自定义";

    public string Name { get; set; } = StandardOfficial;
    public string Description { get; set; } = "";
    public bool IsBuiltIn { get; set; } = true;
    public DocxFormattingOptions Options { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 获取所有内置排版方案
    /// </summary>
    public static List<FormattingProfile> GetBuiltInProfiles() =>
    [
        new()
        {
            Name = StandardOfficial,
            Description = "GB/T 9704-2012 党政机关公文格式标准",
            IsBuiltIn = true,
            Options = new DocxFormattingOptions() // 默认值即 GB/T 9704
        },
        new()
        {
            Name = EnterpriseEnhanced,
            Description = "字号大一号、行距31磅、页边距加宽、字间距0.4pt",
            IsBuiltIn = true,
            Options = new DocxFormattingOptions
            {
                TitleFontSizePt = 22,
                HeadingFontSizePt = 18,
                SubheadingFontSizePt = 16,
                BodyFontSizePt = 16,
                LineSpacingPt = 31,
                FirstLineIndentChars = 2,
                BeforeSpacingPt = 0,
                AfterSpacingPt = 0,
                PageMarginTopCm = 3.2,
                PageMarginBottomCm = 3.2,
                PageMarginLeftCm = 2.5,
                PageMarginRightCm = 2.5
            }
        },
        new()
        {
            Name = AcademicThesis,
            Description = "学术论文常用格式：宋体正文、小四号字、1.5倍行距",
            IsBuiltIn = true,
            Options = new DocxFormattingOptions
            {
                TitleFont = "黑体",
                HeadingFont = "黑体",
                SubheadingFont = "黑体",
                BodyFont = "宋体",
                CodeBlockFont = "Consolas",
                TitleFontSizePt = 16,
                HeadingFontSizePt = 14,
                SubheadingFontSizePt = 13,
                BodyFontSizePt = 12,
                CodeBlockFontSizePt = 9,
                LineSpacingPt = 21, // 1.5 倍行距约等于 21pt（12pt × 1.5 单倍行距≈18pt，固定21更舒适）
                FirstLineIndentChars = 2,
                BeforeSpacingPt = 6,
                AfterSpacingPt = 6,
                PageMarginTopCm = 2.54,
                PageMarginBottomCm = 2.54,
                PageMarginLeftCm = 3.17,
                PageMarginRightCm = 3.17
            }
        }
    ];

    /// <summary>
    /// 导出为 JSON 文件
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var json = JsonSerializer.Serialize(this, JsonOpts);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 从 JSON 文件加载排版方案
    /// </summary>
    public static FormattingProfile? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<FormattingProfile>(json, JsonOpts);
            if (profile != null)
            {
                profile.IsBuiltIn = false;
                profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? Custom : profile.Name;
            }
            return profile;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 根据方案名称获取内置方案，未匹配则返回标准公文格式
    /// </summary>
    public static FormattingProfile GetBuiltIn(string name)
    {
        return GetBuiltInProfiles().FirstOrDefault(p => p.Name == name)
            ?? GetBuiltInProfiles()[0];
    }
}
