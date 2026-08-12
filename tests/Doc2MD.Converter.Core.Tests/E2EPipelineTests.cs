using System.IO;
using System.Text.Json;
using Doc2MD.Pipeline.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// Pipeline 端到端测试：使用 fixtures Markdown 样例验证完整转换链路。
/// 供 smoke-test.ps1 通过 dotnet test --filter "FullyQualifiedName~E2EPipeline" 调用。
/// 覆盖：3 模板转换、DOCX 可重开、格式检查报告、同名文件保护、PreserveFolderStructure。
/// </summary>
public class E2EPipelineTests
{
    private static readonly string FixturesDir = ResolveFixturesDir();

    private static string ResolveFixturesDir()
    {
        // 1. Try output directory (if content was copied by build)
        var outputDir = Path.Combine(AppContext.BaseDirectory, "fixtures");
        if (File.Exists(Path.Combine(outputDir, "sample_report.md")))
            return outputDir;

        // 2. Fall back to source directory (bin/Release/net8.0/ → ../../../../fixtures/)
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var sourceDir = Path.Combine(projectDir, "fixtures");
        return sourceDir;
    }

    // ========================================================================
    // 1. official-report 模板完整 E2E
    // ========================================================================
    [Fact]
    public void E2EPipeline_OfficialReport_FixtureConvert_Reopen_ReportTemplate()
    {
        var fixturePath = Path.Combine(FixturesDir, "sample_report.md");
        Assert.True(File.Exists(fixturePath), $"Fixture not found: {fixturePath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"E2E_official_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "report.docx");
            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(fixturePath, outputPath, "official-report");

            // 1a. 转换成功
            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath), "DOCX 文件应存在");

            // 1b. 使用 Open XML 重新打开 DOCX 验证结构完整
            using (var doc = WordprocessingDocument.Open(outputPath, false))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                Assert.NotNull(body);
                var paragraphs = body!.Elements<Paragraph>()
                    .Where(p => !string.IsNullOrWhiteSpace(p.InnerText)).ToList();
                Assert.True(paragraphs.Count > 0, "DOCX 应包含段落内容");
            }

            // 1c. 格式检查报告存在
            Assert.True(File.Exists(result.FormatCheckReportPath),
                "格式检查报告 .format_check_report.json 应存在");

            // 1d. 报告 template 字段正确
            var reportJson = File.ReadAllText(result.FormatCheckReportPath!);
            var report = JsonSerializer.Deserialize<JsonDocument>(reportJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var templateVal = report!.RootElement.GetProperty("template").GetString();
            Assert.Equal("official-report", templateVal);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ========================================================================
    // 2. meeting-minutes 模板完整 E2E
    // ========================================================================
    [Fact]
    public void E2EPipeline_MeetingMinutes_FixtureConvert_Reopen_ReportTemplate()
    {
        var fixturePath = Path.Combine(FixturesDir, "sample_meeting.md");
        Assert.True(File.Exists(fixturePath), $"Fixture not found: {fixturePath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"E2E_meeting_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "meeting.docx");
            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(fixturePath, outputPath, "meeting-minutes");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.OutputPath));

            // Open XML 重开
            using (var doc = WordprocessingDocument.Open(outputPath, false))
            {
                Assert.NotNull(doc.MainDocumentPart?.Document?.Body);
            }

            // 格式检查报告
            Assert.True(File.Exists(result.FormatCheckReportPath));
            var reportJson = File.ReadAllText(result.FormatCheckReportPath!);
            var report = JsonSerializer.Deserialize<JsonDocument>(reportJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal("meeting-minutes", report!.RootElement.GetProperty("template").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ========================================================================
    // 3. 同名文件保护：已有同名 DOCX 时不覆盖，使用 _1 后缀
    //    复现 MainViewModel.RunPipelineMd2DocxAsync 中的同名处理逻辑
    // ========================================================================
    [Fact]
    public void E2EPipeline_SameNameFile_NotOverwritten()
    {
        var fixturePath = Path.Combine(FixturesDir, "sample_report.md");
        Assert.True(File.Exists(fixturePath));

        var tempDir = Path.Combine(Path.GetTempPath(), $"E2E_samename_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var converter = new MarkdownToDocxConverter();
            var baseName = Path.GetFileNameWithoutExtension(fixturePath);

            // 第一次转换：生成 sample_report.docx
            var firstOutput = Path.Combine(tempDir, baseName + ".docx");
            var result1 = converter.Convert(fixturePath, firstOutput, "official-report");
            Assert.True(result1.Success, result1.ErrorMessage);
            Assert.True(File.Exists(firstOutput));

            // 记录原始文件内容（字节级快照）
            var originalBytes = File.ReadAllBytes(firstOutput);

            // 模拟 MainViewModel 同名处理逻辑（OverwriteExistingFile=false）
            var overwriteExisting = false;
            var outputPath = firstOutput;
            if (File.Exists(outputPath) && !overwriteExisting)
            {
                var counter = 1;
                var dir = Path.GetDirectoryName(outputPath)!;
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fixturePath);
                do
                {
                    outputPath = Path.Combine(dir, $"{nameWithoutExt}_{counter}.docx");
                    counter++;
                } while (File.Exists(outputPath));
            }

            // 第二次转换：写入 _1 后缀的新路径
            var result2 = converter.Convert(fixturePath, outputPath, "official-report");
            Assert.True(result2.Success, result2.ErrorMessage);

            // 验证：原文件未被覆盖（字节相同）
            Assert.Equal(originalBytes, File.ReadAllBytes(firstOutput));

            // 验证：_1 文件存在且与原文件不同路径
            Assert.NotEqual(firstOutput, outputPath);
            Assert.True(File.Exists(outputPath), $"同名保护文件应存在: {outputPath}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ========================================================================
    // 5. PreserveFolderStructure：输入在子目录时，输出保持相同相对结构
    //    复现 MainViewModel.RunPipelineMd2DocxAsync 中的 PreserveFolderStructure 逻辑
    // ========================================================================
    [Fact]
    public void E2EPipeline_PreserveFolderStructure_SubdirectoryOutput()
    {
        var fixturePath = Path.Combine(FixturesDir, "sample_report.md");
        Assert.True(File.Exists(fixturePath));

        var tempDir = Path.Combine(Path.GetTempPath(), $"E2E_preserve_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // 构建输入目录结构：inputRoot/sub/test.md
            var inputRoot = Path.Combine(tempDir, "input");
            var subDir = Path.Combine(inputRoot, "财务部");
            Directory.CreateDirectory(subDir);
            var mdPath = Path.Combine(subDir, "report.md");
            File.Copy(fixturePath, mdPath);

            var outputDir = Path.Combine(tempDir, "output");

            // 模拟 MainViewModel PreserveFolderStructure 逻辑
            var preserveFolderStructure = true;
            var currentOutputDirectory = outputDir;

            if (preserveFolderStructure && !string.IsNullOrWhiteSpace(inputRoot))
            {
                var fileDir = Path.GetDirectoryName(mdPath) ?? string.Empty;
                if (fileDir.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = fileDir[inputRoot.Length..]
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.IsNullOrWhiteSpace(relativePath))
                        currentOutputDirectory = Path.Combine(outputDir, relativePath);
                }
            }

            Directory.CreateDirectory(currentOutputDirectory);

            var outputPath = Path.Combine(currentOutputDirectory,
                Path.GetFileNameWithoutExtension(mdPath) + ".docx");

            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(mdPath, outputPath, "official-report");

            // 验证：输出在 output/财务部/ 子目录中
            Assert.True(result.Success, result.ErrorMessage);
            var expectedPath = Path.Combine(outputDir, "财务部", "report.docx");
            Assert.Equal(expectedPath, result.OutputPath);
            Assert.True(File.Exists(expectedPath),
                $"PreserveFolderStructure 输出应在子目录: {expectedPath}");

            // 验证格式检查报告也在同一子目录
            var expectedReport = Path.Combine(outputDir, "财务部", "report.format_check_report.json");
            Assert.True(File.Exists(expectedReport),
                $"格式检查报告应在子目录: {expectedReport}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
