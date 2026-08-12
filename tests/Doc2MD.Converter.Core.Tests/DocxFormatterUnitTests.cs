using System.Reflection;
using Doc2MD.Models;
using Doc2MD.Services;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// DocxFormatter 私有纯逻辑方法反射单元测试。
/// 覆盖 ResolveBodyFont / ResolveDoubleToHp / DetectHeadingLevel。
/// </summary>
public class DocxFormatterUnitTests
{
    private static readonly MethodInfo ResolveBodyFontMethod =
        typeof(DocxFormatter).GetMethod("ResolveBodyFont", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("ResolveBodyFont not found");

    private static readonly MethodInfo ResolveDoubleToHpMethod =
        typeof(DocxFormatter).GetMethod("ResolveDoubleToHp", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("ResolveDoubleToHp not found");

    private static readonly MethodInfo DetectHeadingLevelMethod =
        typeof(DocxFormatter).GetMethod("DetectHeadingLevel", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("DetectHeadingLevel not found");

    private static string CallResolveBodyFont(FormatDocPreviewSettings? settings)
        => (string)ResolveBodyFontMethod.Invoke(null, [settings])!;

    private static int CallResolveDoubleToHp(double? value, int fallback)
        => (int)ResolveDoubleToHpMethod.Invoke(null, [new Func<double?>(() => value), fallback])!;

    private static int CallDetectHeadingLevel(Paragraph paragraph, string text)
    {
        var formatter = new DocxFormatter();
        return (int)DetectHeadingLevelMethod.Invoke(formatter, [paragraph, text])!;
    }

    // === ResolveBodyFont ===

    [Fact]
    public void ResolveBodyFont_PrefersStructuredField()
    {
        var settings = new FormatDocPreviewSettings { BodyFont = "仿宋", FontFamily = "楷体" };
        Assert.Equal("仿宋", CallResolveBodyFont(settings));
    }

    [Fact]
    public void ResolveBodyFont_FallsBackToLegacyFontFamily()
    {
        var settings = new FormatDocPreviewSettings { BodyFont = "", FontFamily = "楷体" };
        Assert.Equal("楷体", CallResolveBodyFont(settings));
    }

    [Fact]
    public void ResolveBodyFont_EmptySettings_FallsBackToNationalStandard()
    {
        var result = CallResolveBodyFont(null);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    // === ResolveDoubleToHp ===

    [Fact]
    public void ResolveDoubleToHp_ValidValue_ConvertsToHalfPoints()
    {
        // 16pt * 2 = 32 半磅
        Assert.Equal(32, CallResolveDoubleToHp(16.0, 20));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(null)]
    [InlineData(-5.0)]
    public void ResolveDoubleToHp_InvalidValue_UsesFallback(double? value)
    {
        Assert.Equal(20, CallResolveDoubleToHp(value, 20));
    }

    // === DetectHeadingLevel ===

    [Theory]
    [InlineData("Heading1", "", 1)]
    [InlineData("标题1", "", 1)]
    [InlineData("Heading2", "", 2)]
    [InlineData("标题2", "", 2)]
    [InlineData("Heading3", "", 3)]
    [InlineData("标题3", "", 3)]
    public void DetectHeadingLevel_StyleId_DeterminesLevel(string styleId, string text, int expected)
    {
        var para = new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = styleId }));
        Assert.Equal(expected, CallDetectHeadingLevel(para, text));
    }

    [Theory]
    [InlineData("一、总则", 1)]
    [InlineData("（一）基本要求", 2)]
    [InlineData("1.1 背景", 3)]
    [InlineData("1、第一条", 3)]
    [InlineData("第一条", 0)]          // 中文序数词不匹配数字正则
    [InlineData("普通正文段落", 0)]
    [InlineData("", 0)]
    public void DetectHeadingLevel_TextHeuristic_WhenNoStyle(string text, int expected)
    {
        var para = new Paragraph();
        Assert.Equal(expected, CallDetectHeadingLevel(para, text));
    }
}
