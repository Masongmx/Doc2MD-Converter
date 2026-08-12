using System.Reflection;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// ExcelParser 私有纯逻辑方法反射单元测试。
/// 覆盖 GetColumnIndex / EscapeMdCell / EscapeCsvField / GenerateCsv。
/// </summary>
public class ExcelParserUnitTests
{
    private static readonly MethodInfo GetColumnIndexMethod =
        typeof(Parsers.ExcelParser).GetMethod("GetColumnIndex", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("GetColumnIndex not found");

    private static readonly MethodInfo EscapeMdCellMethod =
        typeof(Parsers.ExcelParser).GetMethod("EscapeMdCell", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("EscapeMdCell not found");

    private static readonly MethodInfo EscapeCsvFieldMethod =
        typeof(Parsers.ExcelParser).GetMethod("EscapeCsvField", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("EscapeCsvField not found");

    private static readonly MethodInfo GenerateCsvMethod =
        typeof(Parsers.ExcelParser).GetMethod("GenerateCsv", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("GenerateCsv not found");

    private static int CallGetColumnIndex(string cellReference)
    {
        var parser = new Parsers.ExcelParser();
        return (int)GetColumnIndexMethod.Invoke(parser, [cellReference])!;
    }

    private static string CallEscapeMdCell(string value)
    {
        var parser = new Parsers.ExcelParser();
        return (string)EscapeMdCellMethod.Invoke(parser, [value])!;
    }

    private static string CallEscapeCsvField(string field)
        => (string)EscapeCsvFieldMethod.Invoke(null, [field])!;

    private static string CallGenerateCsv(Dictionary<(int, int), string> cellMap, int maxRow, int maxCol)
        => (string)GenerateCsvMethod.Invoke(null, [cellMap, maxRow, maxCol])!;

    // === GetColumnIndex ===

    [Theory]
    [InlineData("A1", 0)]
    [InlineData("Z9", 25)]
    [InlineData("AA1", 26)]
    [InlineData("AB7", 27)]
    [InlineData("B12", 1)]
    public void GetColumnIndex_VariousReferences_ReturnsZeroBasedIndex(string reference, int expected)
    {
        Assert.Equal(expected, CallGetColumnIndex(reference));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1")]
    public void GetColumnIndex_NoLetterPrefix_ReturnsZero(string reference)
    {
        Assert.Equal(0, CallGetColumnIndex(reference));
    }

    // === EscapeMdCell ===

    [Theory]
    [InlineData("", "")]
    [InlineData("a|b", "a\\|b")]
    [InlineData("a\nb", "a b")]
    [InlineData("a\rb", "ab")]
    [InlineData("普通文本", "普通文本")]
    public void EscapeMdCell_EscapesMarkdownTableSpecials(string input, string expected)
    {
        Assert.Equal(expected, CallEscapeMdCell(input));
    }

    // === EscapeCsvField ===

    [Theory]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    public void EscapeCsvField_SimpleField_Unchanged(string input, string expected)
    {
        Assert.Equal(expected, CallEscapeCsvField(input));
    }

    [Theory]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    [InlineData("a\nb", "\"a\nb\"")]
    public void EscapeCsvField_SpecialChars_QuotedAndEscaped(string input, string expected)
    {
        Assert.Equal(expected, CallEscapeCsvField(input));
    }

    // === GenerateCsv ===

    [Fact]
    public void GenerateCsv_SingleCell_ProducesHeaderOnly()
    {
        var map = new Dictionary<(int, int), string> { [(0, 0)] = "值" };
        var csv = CallGenerateCsv(map, 0, 0);
        // AppendLine 使用环境换行符；规范化后应等于 "值\n"
        Assert.Equal("值\n", csv.Replace("\r\n", "\n"));
    }

    [Fact]
    public void GenerateCsv_MissingCells_FilledWithEmpty()
    {
        var map = new Dictionary<(int, int), string> { [(1, 1)] = "X" };
        var csv = CallGenerateCsv(map, 1, 1);
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal(",", lines[0]);       // (0,0) 空,(0,1) 空
        Assert.Equal(",X", lines[1]);      // (1,0) 空,(1,1) X
    }

    [Fact]
    public void GenerateCsv_CommaAndQuote_Escaped()
    {
        var map = new Dictionary<(int, int), string> { [(0, 0)] = "a,b", [(0, 1)] = "c\"d" };
        var csv = CallGenerateCsv(map, 0, 1);
        var line = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.Equal("\"a,b\",\"c\"\"d\"", line);
    }
}
