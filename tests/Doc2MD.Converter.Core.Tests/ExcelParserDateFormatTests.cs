using System.Reflection;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// U3: Excel 日期格式修复测试
/// 通过反射测试 ExcelParser.IsDateFormatString 私有方法
/// </summary>
public class ExcelParserDateFormatTests
{
    private static readonly MethodInfo IsDateFormatStringMethod =
        typeof(Parsers.ExcelParser).GetMethod("IsDateFormatString",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("IsDateFormatString not found");

    private static bool CallIsDateFormatString(string formatCode)
    {
        var parser = new Parsers.ExcelParser();
        return (bool)IsDateFormatStringMethod.Invoke(parser, [formatCode])!;
    }

    // === 纯日期格式 -> true ===

    [Theory]
    [InlineData("yyyy-mm-dd")]
    [InlineData("yyyy/mm/dd")]
    [InlineData("yy-m-d")]
    [InlineData("yyyy\u5e74m\u6708d\u65e5")]
    [InlineData("yyyy\u5e74mm\u6708dd\u65e5")]
    [InlineData("mm/dd/yyyy")]
    [InlineData("dd-mm-yyyy")]
    public void PureDateFormat_ReturnsTrue(string format)
    {
        Assert.True(CallIsDateFormatString(format));
    }

    // === 纯时间格式 -> false (mm is minutes not months) ===

    [Theory]
    [InlineData("hh:mm:ss")]
    [InlineData("h:mm")]
    [InlineData("hh:mm")]
    [InlineData("mm:ss")]
    [InlineData("h:mm:ss")]
    [InlineData("hh\u65f6mm\u5206ss\u79d2")]
    public void PureTimeFormat_ReturnsFalse(string format)
    {
        Assert.False(CallIsDateFormatString(format));
    }

    // === 混合日期时间格式 -> true ===

    [Theory]
    [InlineData("yyyy-mm-dd hh:mm:ss")]
    [InlineData("yyyy/m/d h:mm")]
    [InlineData("dd-mm-yyyy hh:mm")]
    public void DateTimeFormat_ReturnsTrue(string format)
    {
        Assert.True(CallIsDateFormatString(format));
    }

    // === 边界情况 ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrNull_ReturnsFalse(string format)
    {
        Assert.False(CallIsDateFormatString(format));
    }

    [Theory]
    [InlineData("General")]
    [InlineData("0.00")]
    [InlineData("#,##0")]
    [InlineData("0%")]
    [InlineData("@")]
    public void NonDateFormat_ReturnsFalse(string format)
    {
        Assert.False(CallIsDateFormatString(format));
    }

    [Theory]
    [InlineData("yyyy")]
    [InlineData("dd")]
    [InlineData("yyyy\u5e74")]
    [InlineData("d\u65e5")]
    public void YearOrDayOnly_ReturnsTrue(string format)
    {
        Assert.True(CallIsDateFormatString(format));
    }

    [Fact]
    public void MonthOnly_ReturnsTrue()
    {
        Assert.True(CallIsDateFormatString("mm"));
    }

    [Theory]
    [InlineData("hh")]
    [InlineData("ss")]
    public void HourOrSecondOnly_ReturnsFalse(string format)
    {
        Assert.False(CallIsDateFormatString(format));
    }
}
