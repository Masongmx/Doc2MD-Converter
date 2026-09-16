using System.IO;
using System.Reflection;
using Doc2MD.Models;
using Doc2MD.ViewModels;
using Xunit;

namespace Doc2MD.App.Tests;

public class FileScanServiceTests
{
    private readonly FileScanService _scanner;
    private static readonly MethodInfo ShouldIgnorePathMethod =
        typeof(FileScanService).GetMethod("ShouldIgnorePath", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("ShouldIgnorePath not found");

    public FileScanServiceTests()
    {
        var config = new AppConfig();
        _scanner = new FileScanService(config);
    }

    private bool CallShouldIgnorePath(string path)
    {
        return (bool)ShouldIgnorePathMethod.Invoke(_scanner, [path])!;
    }

    [Theory]
    [InlineData("~$report.docx")]
    [InlineData("~$finance.xlsx")]
    [InlineData("~$presentation.pptx")]
    [InlineData("Thumbs.db")]
    [InlineData(".DS_Store")]
    [InlineData("desktop.ini")]
    public void ShouldIgnorePath_TemporaryAndJunkFiles_ReturnsTrue(string fileName)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.True(CallShouldIgnorePath(tempPath));
    }

    [Theory]
    [InlineData("document.docx")]
    [InlineData("data.xlsx")]
    [InlineData("slides.pptx")]
    [InlineData("article.md")]
    [InlineData("guide.pdf")]
    public void ShouldIgnorePath_NormalFiles_ReturnsFalse(string fileName)
    {
        var normalPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.False(CallShouldIgnorePath(normalPath));
    }

    [Theory]
    [InlineData("doc.pdf", AppMode.ToMarkdown, true)]
    [InlineData("doc.docx", AppMode.ToMarkdown, true)]
    [InlineData("doc.xlsx", AppMode.ToMarkdown, true)]
    [InlineData("doc.pptx", AppMode.ToMarkdown, true)]
    [InlineData("doc.md", AppMode.ToMarkdown, false)]
    [InlineData("doc.md", AppMode.MarkdownToDocx, true)]
    [InlineData("doc.markdown", AppMode.MarkdownToDocx, true)]
    [InlineData("doc.pdf", AppMode.MarkdownToDocx, false)]
    [InlineData("doc.docx", AppMode.FormatDoc, true)]
    [InlineData("doc.doc", AppMode.FormatDoc, true)]
    [InlineData("doc.pdf", AppMode.FormatDoc, false)]
    public void IsSupportedForMode_CorrectlyIdentifiesExtensions(string filePath, AppMode mode, bool expected)
    {
        Assert.Equal(expected, FileScanService.IsSupportedForMode(filePath, mode));
    }
}
