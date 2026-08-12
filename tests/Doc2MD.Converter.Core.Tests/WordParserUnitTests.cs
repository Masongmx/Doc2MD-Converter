using System.Reflection;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// WordParser 私有纯逻辑方法反射单元测试。
/// 覆盖 ExtractHeadingLevel / IsOrderedNumberFormat / GetListPrefix / FormatCellText。
/// </summary>
public class WordParserUnitTests
{
    private static readonly MethodInfo ExtractHeadingLevelMethod =
        typeof(Parsers.WordParser).GetMethod("ExtractHeadingLevel", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("ExtractHeadingLevel not found");

    private static readonly MethodInfo IsOrderedNumberFormatMethod =
        typeof(Parsers.WordParser).GetMethod("IsOrderedNumberFormat", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("IsOrderedNumberFormat not found");

    private static readonly MethodInfo GetListPrefixMethod =
        typeof(Parsers.WordParser).GetMethod("GetListPrefix", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("GetListPrefix not found");

    private static readonly MethodInfo FormatCellTextMethod =
        typeof(Parsers.WordParser).GetMethod("FormatCellText", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("FormatCellText not found");

    private static int CallExtractHeadingLevel(string style)
    {
        var parser = new Parsers.WordParser();
        return (int)ExtractHeadingLevelMethod.Invoke(parser, [style])!;
    }

    private static bool CallIsOrderedNumberFormat(string format)
        => (bool)IsOrderedNumberFormatMethod.Invoke(null, [format])!;

    private static string CallGetListPrefix(Paragraph para)
    {
        var parser = new Parsers.WordParser();
        return (string)GetListPrefixMethod.Invoke(parser, [para])!;
    }

    private static string CallFormatCellText(TableCell cell)
    {
        var parser = new Parsers.WordParser();
        return (string)FormatCellTextMethod.Invoke(parser, [cell])!;
    }

    // === ExtractHeadingLevel ===

    [Theory]
    [InlineData("Heading1", 1)]
    [InlineData("Heading2", 2)]
    [InlineData("Heading3", 3)]
    [InlineData("Heading6", 6)]
    public void ExtractHeadingLevel_ValidStyle_ReturnsLevel(string style, int expected)
    {
        Assert.Equal(expected, CallExtractHeadingLevel(style));
    }

    [Theory]
    [InlineData("Heading")]   // 无数字 -> 1
    [InlineData("Title")]     // 无数字 -> 1
    [InlineData("Heading7")]  // 超范围 -> 1
    [InlineData("")]          // 空串 -> 1
    public void ExtractHeadingLevel_InvalidOrOutOfRange_ReturnsDefaultOne(string style)
    {
        Assert.Equal(1, CallExtractHeadingLevel(style));
    }

    // === IsOrderedNumberFormat ===

    [Theory]
    [InlineData("decimal")]
    [InlineData("upperRoman")]
    [InlineData("lowerLetter")]
    [InlineData("chineseCounting")]
    public void IsOrderedNumberFormat_OrderedFormat_ReturnsTrue(string format)
    {
        Assert.True(CallIsOrderedNumberFormat(format));
    }

    [Theory]
    [InlineData("bullet")]
    [InlineData("none")]
    [InlineData("")]
    public void IsOrderedNumberFormat_NonOrdered_ReturnsFalse(string format)
    {
        Assert.False(CallIsOrderedNumberFormat(format));
    }

    // === GetListPrefix ===

    [Fact]
    public void GetListPrefix_NoNumbering_ReturnsBulletDash()
    {
        var para = new Paragraph();
        Assert.Equal("- ", CallGetListPrefix(para));
    }

    [Fact]
    public void GetListPrefix_NullNumberingId_ReturnsBulletDash()
    {
        // NumberingProperties 存在但 NumberingId 为空
        var para = new Paragraph(
            new ParagraphProperties(
                new NumberingProperties()));
        Assert.Equal("- ", CallGetListPrefix(para));
    }

    [Fact]
    public void GetListPrefix_ZeroNumberingId_ReturnsBulletDash()
    {
        var para = new Paragraph(
            new ParagraphProperties(
                new NumberingProperties(
                    new NumberingId { Val = 0 })));
        Assert.Equal("- ", CallGetListPrefix(para));
    }

    // === FormatCellText ===

    [Fact]
    public void FormatCellText_EmptyCell_ReturnsEmptyString()
    {
        var cell = new TableCell();
        Assert.Equal(string.Empty, CallFormatCellText(cell));
    }

    [Fact]
    public void FormatCellText_PlainTextCell_EscapesPipe()
    {
        var cell = new TableCell(
            new Paragraph(new Run(new Text("a|b"))));
        Assert.Equal("a\\|b", CallFormatCellText(cell));
    }

    [Fact]
    public void FormatCellText_MultipleParagraphs_JoinedWithSpace()
    {
        var cell = new TableCell(
            new Paragraph(new Run(new Text("第一行"))),
            new Paragraph(new Run(new Text("第二行"))));
        Assert.Equal("第一行 第二行", CallFormatCellText(cell));
    }
}
