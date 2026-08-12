using System.Reflection;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// PdfParser 私有纯逻辑方法反射单元测试。
/// 覆盖 GetSafeFileName / DetermineImageMimeType。
/// </summary>
public class PdfParserUnitTests
{
    private static readonly MethodInfo GetSafeFileNameMethod =
        typeof(Parsers.PdfParser).GetMethod("GetSafeFileName", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("GetSafeFileName not found");

    private static readonly MethodInfo DetermineImageMimeTypeMethod =
        typeof(Parsers.PdfParser).GetMethod("DetermineImageMimeType", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DetermineImageMimeType not found");

    private static string CallGetSafeFileName(string fileName)
        => (string)GetSafeFileNameMethod.Invoke(null, [fileName])!;

    private static string CallDetermineImageMimeType(byte[] data)
        => (string)DetermineImageMimeTypeMethod.Invoke(null, [data])!;

    // === GetSafeFileName ===

    [Fact]
    public void GetSafeFileName_NoInvalidChars_Unchanged()
    {
        Assert.Equal("report_2026.pdf", CallGetSafeFileName("report_2026.pdf"));
    }

    [Fact]
    public void GetSafeFileName_InvalidChars_ReplacedWithUnderscore()
    {
        var invalid = Path.GetInvalidFileNameChars().First();
        var input = $"a{invalid}b{invalid}c";
        var result = CallGetSafeFileName(input);
        Assert.Equal("a_b_c", result);
        Assert.DoesNotContain(invalid, result);
    }

    [Fact]
    public void GetSafeFileName_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CallGetSafeFileName(string.Empty));
    }

    // === DetermineImageMimeType ===

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, "image/gif")]
    [InlineData(new byte[] { 0x42, 0x4D, 0x00, 0x00 }, "image/bmp")]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00 }, "image/tiff")]
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, "image/tiff")]
    public void DetermineImageMimeType_MagicBytes_DetectsCorrectType(byte[] data, string expected)
    {
        Assert.Equal(expected, CallDetermineImageMimeType(data));
    }

    [Fact]
    public void DetermineImageMimeType_TooShort_DefaultsToPng()
    {
        Assert.Equal("image/png", CallDetermineImageMimeType([0xFF]));
    }

    [Fact]
    public void DetermineImageMimeType_UnknownHeader_DefaultsToPng()
    {
        Assert.Equal("image/png", CallDetermineImageMimeType([0x00, 0x01, 0x02, 0x03]));
    }
}
