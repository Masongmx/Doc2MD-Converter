using System.IO;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 排版方案管理服务：方案应用、保存、加载
/// </summary>
public static class FormattingProfileService
{
    /// <summary>
    /// 将排版方案应用到 MarkdownToDocxPreviewSettings
    /// </summary>
    public static void ApplyTo(FormattingProfile profile, MarkdownToDocxPreviewSettings settings)
    {
        var opt = profile.Options;
        settings.TitleFont = opt.TitleFont;
        settings.HeadingFont = opt.HeadingFont;
        settings.SubheadingFont = opt.SubheadingFont;
        settings.BodyFont = opt.BodyFont;
        settings.CodeBlockFont = opt.CodeBlockFont;
        settings.TitleFontSizePt = opt.TitleFontSizePt;
        settings.HeadingFontSizePt = opt.HeadingFontSizePt;
        settings.SubheadingFontSizePt = opt.SubheadingFontSizePt;
        settings.BodyFontSizePt = opt.BodyFontSizePt;
        settings.CodeBlockFontSizePt = opt.CodeBlockFontSizePt;
        settings.LineSpacingPt = opt.LineSpacingPt;
        settings.FirstLineIndentChars = opt.FirstLineIndentChars;
        settings.BeforeSpacingPt = opt.BeforeSpacingPt;
        settings.AfterSpacingPt = opt.AfterSpacingPt;
        settings.PageMarginTopCm = opt.PageMarginTopCm;
        settings.PageMarginBottomCm = opt.PageMarginBottomCm;
        settings.PageMarginLeftCm = opt.PageMarginLeftCm;
        settings.PageMarginRightCm = opt.PageMarginRightCm;
    }

    /// <summary>
    /// 将排版方案应用到 FormatDocPreviewSettings
    /// </summary>
    public static void ApplyTo(FormattingProfile profile, FormatDocPreviewSettings settings)
    {
        var opt = profile.Options;
        settings.TitleFont = opt.TitleFont;
        settings.HeadingFont = opt.HeadingFont;
        settings.SubheadingFont = opt.SubheadingFont;
        settings.BodyFont = opt.BodyFont;
        settings.CodeBlockFont = opt.CodeBlockFont;
        settings.TitleFontSizePt = opt.TitleFontSizePt;
        settings.HeadingFontSizePt = opt.HeadingFontSizePt;
        settings.SubheadingFontSizePt = opt.SubheadingFontSizePt;
        settings.BodyFontSizePt = opt.BodyFontSizePt;
        settings.CodeBlockFontSizePt = opt.CodeBlockFontSizePt;
        settings.LineSpacingPt = opt.LineSpacingPt;
        settings.FirstLineIndentChars = opt.FirstLineIndentChars;
        settings.BeforeSpacingPt = opt.BeforeSpacingPt;
        settings.AfterSpacingPt = opt.AfterSpacingPt;
        settings.PageMarginTopCm = opt.PageMarginTopCm;
        settings.PageMarginBottomCm = opt.PageMarginBottomCm;
        settings.PageMarginLeftCm = opt.PageMarginLeftCm;
        settings.PageMarginRightCm = opt.PageMarginRightCm;
    }

    /// <summary>
    /// 从 MarkdownToDocxPreviewSettings 导出为 FormattingProfile
    /// </summary>
    public static FormattingProfile ExtractProfile(MarkdownToDocxPreviewSettings settings)
    {
        return new FormattingProfile
        {
            Name = FormattingProfile.Custom,
            IsBuiltIn = false,
            Options = new DocxFormattingOptions
            {
                TitleFont = settings.TitleFont ?? "",
                HeadingFont = settings.HeadingFont ?? "",
                SubheadingFont = settings.SubheadingFont ?? "",
                BodyFont = settings.BodyFont ?? "",
                CodeBlockFont = settings.CodeBlockFont ?? "Consolas",
                TitleFontSizePt = settings.TitleFontSizePt,
                HeadingFontSizePt = settings.HeadingFontSizePt,
                SubheadingFontSizePt = settings.SubheadingFontSizePt,
                BodyFontSizePt = settings.BodyFontSizePt,
                CodeBlockFontSizePt = settings.CodeBlockFontSizePt,
                LineSpacingPt = settings.LineSpacingPt,
                FirstLineIndentChars = settings.FirstLineIndentChars,
                BeforeSpacingPt = settings.BeforeSpacingPt,
                AfterSpacingPt = settings.AfterSpacingPt,
                PageMarginTopCm = settings.PageMarginTopCm,
                PageMarginBottomCm = settings.PageMarginBottomCm,
                PageMarginLeftCm = settings.PageMarginLeftCm,
                PageMarginRightCm = settings.PageMarginRightCm
            }
        };
    }

    /// <summary>
    /// 从 FormatDocPreviewSettings 导出为 FormattingProfile
    /// </summary>
    public static FormattingProfile ExtractProfile(FormatDocPreviewSettings settings)
    {
        return new FormattingProfile
        {
            Name = FormattingProfile.Custom,
            IsBuiltIn = false,
            Options = new DocxFormattingOptions
            {
                TitleFont = settings.TitleFont ?? "",
                HeadingFont = settings.HeadingFont ?? "",
                SubheadingFont = settings.SubheadingFont ?? "",
                BodyFont = settings.BodyFont ?? "",
                CodeBlockFont = settings.CodeBlockFont ?? "Consolas",
                TitleFontSizePt = settings.TitleFontSizePt,
                HeadingFontSizePt = settings.HeadingFontSizePt,
                SubheadingFontSizePt = settings.SubheadingFontSizePt,
                BodyFontSizePt = settings.BodyFontSizePt,
                CodeBlockFontSizePt = settings.CodeBlockFontSizePt,
                LineSpacingPt = settings.LineSpacingPt,
                FirstLineIndentChars = settings.FirstLineIndentChars,
                BeforeSpacingPt = settings.BeforeSpacingPt,
                AfterSpacingPt = settings.AfterSpacingPt,
                PageMarginTopCm = settings.PageMarginTopCm,
                PageMarginBottomCm = settings.PageMarginBottomCm,
                PageMarginLeftCm = settings.PageMarginLeftCm,
                PageMarginRightCm = settings.PageMarginRightCm
            }
        };
    }

    /// <summary>
    /// 保存排版方案到 JSON 文件
    /// </summary>
    public static bool SaveProfileToFile(FormattingProfile profile, string filePath)
    {
        try
        {
            profile.SaveToFile(filePath);
            LoggingService.Info($"排版方案已保存: {profile.Name} -> {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.Error($"保存排版方案失败: {filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 从 JSON 文件加载排版方案
    /// </summary>
    public static FormattingProfile? LoadProfileFromFile(string filePath)
    {
        var profile = FormattingProfile.LoadFromFile(filePath);
        if (profile != null)
        {
            LoggingService.Info($"排版方案已加载: {profile.Name} <- {filePath}");
        }
        return profile;
    }
}
