using System.IO;
using System.Text.Json;
using Doc2MD.Models;
using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// 统一 DocxFormattingOptions 单元测试。
/// 覆盖：工厂方法关键值、缩进计算、FormattingProfile JSON 往返、
/// FormattingProfileService 与设置对象双向映射。
/// </summary>
public class DocxFormattingOptionsTests
{
    // === 命名空间唯一性（防止重复类型回归） ===

    [Fact]
    public void DocxFormattingOptions_IsSingleType_InModelsNamespace()
    {
        var type = typeof(DocxFormattingOptions);
        Assert.Equal("Doc2MD.Models", type.Namespace);
        Assert.NotNull(type.GetProperty(nameof(DocxFormattingOptions.SchemeName)));
    }

    // === 工厂方法 ===

    [Fact]
    public void Default_ReturnsOfficialBasic()
    {
        var opts = DocxFormattingOptions.Default();
        Assert.Equal("公文格式（基础）", opts.SchemeName);
    }

    [Fact]
    public void OfficialBasic_MatchesGb9704Defaults()
    {
        var opts = DocxFormattingOptions.OfficialBasic();
        // 标题：二号 22pt 方正小标宋
        Assert.Equal("方正小标宋简体", opts.TitleFont);
        Assert.Equal(22.0, opts.TitleFontSizePt);
        Assert.True(opts.TitleBold);
        // 正文：三号 16pt 仿宋
        Assert.Equal("仿宋_GB2312", opts.BodyFont);
        Assert.Equal(16.0, opts.BodyFontSizePt);
        // 层次标题：一黑二楷三仿宋
        Assert.Equal("黑体", opts.Heading1Font);
        Assert.Equal("楷体_GB2312", opts.Heading2Font);
        Assert.Equal("仿宋_GB2312", opts.Heading3Font);
        // 行距 28pt、缩进 2 字
        Assert.Equal(28.0, opts.LineSpacingPt);
        Assert.Equal(2.0, opts.FirstLineIndentChars);
        // 页边距 3.7/3.5/2.8/2.6 cm
        Assert.Equal(3.7, opts.PageMarginTopCm);
        Assert.Equal(3.5, opts.PageMarginBottomCm);
        Assert.Equal(2.8, opts.PageMarginLeftCm);
        Assert.Equal(2.6, opts.PageMarginRightCm);
        // 网格 22 行 × 28 字
        Assert.Equal(22, opts.LinesPerPage);
        Assert.Equal(28, opts.CharsPerLine);
    }

    [Fact]
    public void EnterpriseEnhanced_MatchesSpec()
    {
        var opts = DocxFormattingOptions.EnterpriseEnhanced();
        Assert.Equal("企业增强版", opts.SchemeName);
        Assert.Equal(24.0, opts.TitleFontSizePt);   // 小一号
        Assert.Equal(18.0, opts.BodyFontSizePt);    // 小二号
        Assert.Equal(31.0, opts.LineSpacingPt);     // 行距 31 磅
        Assert.Equal(0.4, opts.CharSpacingPt);      // 字间距 0.4 磅
        Assert.Equal(3.2, opts.PageMarginTopCm);
        Assert.Equal(3.2, opts.PageMarginBottomCm);
        Assert.Equal(2.5, opts.PageMarginLeftCm);
        Assert.Equal(2.5, opts.PageMarginRightCm);
        Assert.Equal(21, opts.LinesPerPage);
        Assert.Equal(24, opts.CharsPerLine);
    }

    [Fact]
    public void GeneralDocument_UsesInchMargins()
    {
        var opts = DocxFormattingOptions.GeneralDocument();
        Assert.Equal("普通文档", opts.SchemeName);
        Assert.Equal(2.54, opts.PageMarginTopCm);
        Assert.Equal(2.54, opts.PageMarginBottomCm);
        Assert.Equal(2.54, opts.PageMarginLeftCm);
        Assert.Equal(2.54, opts.PageMarginRightCm);
    }

    // === CalcIndentTwips ===

    [Theory]
    [InlineData(2.0, 16.0, 640)]   // 2 字符 × 16pt × 20twips
    [InlineData(2.0, 18.0, 720)]   // 企业增强版：小二号
    [InlineData(0.0, 22.0, 0)]     // 标题不缩进
    [InlineData(1.0, 10.5, 210)]   // 代码块 10.5pt
    public void CalcIndentTwips_ComputesTwips(double chars, double fontSizePt, int expected)
    {
        var opts = new DocxFormattingOptions();
        Assert.Equal(expected, opts.CalcIndentTwips(chars, fontSizePt));
    }

    // === FormattingProfile JSON 往返 ===

    [Fact]
    public void FormattingProfile_SaveAndLoad_PreservesOptions()
    {
        var profile = FormattingProfile.GetBuiltIn(FormattingProfile.EnterpriseEnhanced);
        var path = Path.Combine(Path.GetTempPath(), $"profile_{Guid.NewGuid():N}.json");
        try
        {
            profile.SaveToFile(path);
            var loaded = FormattingProfile.LoadFromFile(path);
            Assert.NotNull(loaded);
            Assert.Equal(FormattingProfile.EnterpriseEnhanced, loaded!.Name);
            Assert.Equal(31.0, loaded.Options.LineSpacingPt);
            Assert.Equal(0.4, loaded.Options.CharSpacingPt);
            Assert.Equal(24.0, loaded.Options.TitleFontSizePt);
            Assert.Equal(21, loaded.Options.LinesPerPage);
            Assert.Equal("方正黑体简体", loaded.Options.Heading1Font);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void FormattingProfile_AcademicThesis_SerializesAllProperties()
    {
        var profile = FormattingProfile.GetBuiltIn(FormattingProfile.AcademicThesis);
        // 与 FormattingProfile.SaveToFile 一致的 camelCase 策略
        var json = JsonSerializer.Serialize(profile.Options, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.Contains("\"bodyFont\"", json);
        Assert.Contains("\"lineSpacingPt\"", json);
        var back = JsonSerializer.Deserialize<DocxFormattingOptions>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(back);
        Assert.Equal("宋体", back!.BodyFont);
        Assert.Equal(12.0, back.BodyFontSizePt);
        Assert.Equal(21.0, back.LineSpacingPt);
        Assert.Equal("Consolas", back.CodeBlockFont);
    }

    // === FormattingProfileService 双向映射 ===

    [Fact]
    public void ApplyTo_MapsAllFieldsToSettings()
    {
        var profile = FormattingProfile.GetBuiltIn(FormattingProfile.EnterpriseEnhanced);
        var settings = new FormatDocPreviewSettings();
        FormattingProfileService.ApplyTo(profile, settings);

        Assert.Equal(profile.Options.TitleFont, settings.TitleFont);
        Assert.Equal(profile.Options.HeadingFont, settings.HeadingFont);
        Assert.Equal(profile.Options.SubheadingFont, settings.SubheadingFont);
        Assert.Equal(profile.Options.BodyFont, settings.BodyFont);
        Assert.Equal(profile.Options.CodeBlockFont, settings.CodeBlockFont);
        Assert.Equal(profile.Options.TitleFontSizePt, settings.TitleFontSizePt);
        Assert.Equal(profile.Options.HeadingFontSizePt, settings.HeadingFontSizePt);
        Assert.Equal(profile.Options.SubheadingFontSizePt, settings.SubheadingFontSizePt);
        Assert.Equal(profile.Options.BodyFontSizePt, settings.BodyFontSizePt);
        Assert.Equal(profile.Options.CodeBlockFontSizePt, settings.CodeBlockFontSizePt);
        Assert.Equal(profile.Options.LineSpacingPt, settings.LineSpacingPt);
        Assert.Equal(profile.Options.FirstLineIndentChars, settings.FirstLineIndentChars);
        Assert.Equal(profile.Options.BeforeSpacingPt, settings.BeforeSpacingPt);
        Assert.Equal(profile.Options.AfterSpacingPt, settings.AfterSpacingPt);
        Assert.Equal(profile.Options.PageMarginTopCm, settings.PageMarginTopCm);
        Assert.Equal(profile.Options.PageMarginBottomCm, settings.PageMarginBottomCm);
        Assert.Equal(profile.Options.PageMarginLeftCm, settings.PageMarginLeftCm);
        Assert.Equal(profile.Options.PageMarginRightCm, settings.PageMarginRightCm);
    }

    [Fact]
    public void ExtractProfile_RoundTrips_SettingsToOptions()
    {
        var settings = new FormatDocPreviewSettings
        {
            TitleFont = "黑体",
            HeadingFont = "黑体",
            SubheadingFont = "楷体",
            BodyFont = "宋体",
            CodeBlockFont = "Consolas",
            TitleFontSizePt = 16,
            HeadingFontSizePt = 14,
            SubheadingFontSizePt = 13,
            BodyFontSizePt = 12,
            CodeBlockFontSizePt = 9,
            LineSpacingPt = 21,
            FirstLineIndentChars = 2,
            BeforeSpacingPt = 6,
            AfterSpacingPt = 6,
            PageMarginTopCm = 2.54,
            PageMarginBottomCm = 2.54,
            PageMarginLeftCm = 3.17,
            PageMarginRightCm = 3.17
        };

        var profile = FormattingProfileService.ExtractProfile(settings);
        Assert.Equal(FormattingProfile.Custom, profile.Name);
        Assert.False(profile.IsBuiltIn);
        Assert.Equal("黑体", profile.Options.TitleFont);
        Assert.Equal("宋体", profile.Options.BodyFont);
        Assert.Equal(12.0, profile.Options.BodyFontSizePt);
        Assert.Equal(21.0, profile.Options.LineSpacingPt);
        Assert.Equal(3.17, profile.Options.PageMarginLeftCm);
    }

    // === 合并完整性：Pipeline 与 FormatDoc 属性并存 ===

    [Fact]
    public void UnifiedOptions_ExposesBothStyleSets()
    {
        var opts = new DocxFormattingOptions();
        // Pipeline 侧：多级标题 + 页码 + 网格 + 字间距
        Assert.Equal("黑体", opts.Heading1Font);
        Assert.Equal(14.0, opts.PageNumberFontSizePt);
        Assert.Equal("alternate", opts.PageNumberAlignment);
        Assert.Equal(0.0, opts.CharSpacingPt);
        // FormatDoc 侧：Markdown 标题 + 代码块
        Assert.Equal("黑体", opts.HeadingFont);
        Assert.Equal("楷体_GB2312", opts.SubheadingFont);
        Assert.Equal("Consolas", opts.CodeBlockFont);
        Assert.Equal(10.5, opts.CodeBlockFontSizePt);
    }
}
