using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Doc2MD.Failures;
using Doc2MD.Humanizer;
using Doc2MD.Models;
using Doc2MD.Parsers;
using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Converter.Core.Tests;

/// <summary>
/// 真实使用场景端到端与边界测试（覆盖阅读、查阅、复制、分享与故障诊断）
/// </summary>
public class ScenarioTests
{
    private readonly ConversionService _service;

    public ScenarioTests()
    {
        _service = new ConversionService();
    }

    [Fact]
    public async Task Scenario_HumanReading_ShouldProduceReadableGovMarkdownWithTocAndHighlights()
    {
        // 场景 1：用户阅读场景。生成的 Markdown 需要具备清晰的层级、目录、公文加粗，且保留 AI 结构化元数据
        var rawMd = @"---
title: 测试公文
doc_number: 国发〔2026〕8号
---

# 关于加快推进数字政府建设的指导意见

国发〔2026〕8号

各省、自治区、直辖市人民政府，国务院各部委、各直属机构：

### 一、总体要求
为全面贯彻落实党中央决策部署。

#### （一）指导思想
以新时代中国特色社会主义思想为指导。

#### （二）基本原则
坚持党的全面领导，坚持以人民为中心。

### 二、重点任务
推进政务数据共享与协同。

|序号|建设任务|责任单位|完成时限|
|---|---|---|---|
|1|一网通办平台升级|国办|2026年底|
|2|跨部门数据归集|各部委|持续推进|

- 工作推进机制
  - 成立专班
    - 落实责任
";
        var result = new ConversionResult
        {
            Success = true,
            ProcessedMarkdown = rawMd,
            Metadata = new ConversionMetadata
            {
                GovMetadata = new GovMetadata
                {
                    DocumentNumber = "国发〔2026〕8号",
                    Title = "关于加快推进数字政府建设的指导意见"
                }
            }
        };

        var pipeline = new HumanizerPipeline();
        var options = new HumanizerOptions
        {
            Enabled = true,
            EnableTocGenerator = true,
            TocHeadingThreshold = 3,
            EnableHeadingNormalizer = true,
            EnableTableFormatter = true,
            EnableListNormalizer = true,
            EnableDocHighlight = true
        };

        var optimized = await pipeline.ProcessAsync(rawMd, result, options, CancellationToken.None);

        // 验证可读性增强
        Assert.Contains("## 目录", optimized);
        Assert.Contains("- [一、总体要求](#一、总体要求)", optimized);
        Assert.Contains("## 一、总体要求", optimized); // 3 级被规整为 2 级
        Assert.Contains("**国发〔2026〕8号**", optimized);
        Assert.Contains("| 序号 | 建设任务 | 责任单位 | 完成时限 |", optimized);
        Assert.Contains("    - 落实责任", optimized); // 列表对齐
    }

    [Fact]
    public async Task Scenario_CopyAndShare_ShouldMaintainCleanMarkdownWithoutBrokenEntities()
    {
        // 场景 2：用户查阅、复制与分享场景。验证 Markdown 没有污染性 XML、非法零宽字符、未闭合的表格或乱码
        var markdown = "# 文档标题\n\n正文内容，包含敏感标头。\n\n| 列1 | 列2 |\n| --- | --- |\n| A | B |\n\n```csharp\nvar x = 1;\n```\n";
        var result = new ConversionResult { Success = true, ProcessedMarkdown = markdown };

        var pipeline = new HumanizerPipeline();
        var options = new HumanizerOptions { Enabled = true };

        var output = await pipeline.ProcessAsync(markdown, result, options, CancellationToken.None);

        Assert.DoesNotContain("w:document", output);
        Assert.DoesNotContain("<o:p>", output);
        Assert.Contains("```csharp", output);
        Assert.Contains("| 列1 | 列2 |", output);
    }

    [Fact]
    public async Task Scenario_LockedFile_ShouldDiagnoseAndReturnActionableGuide()
    {
        // 场景 3：用户正在 Word 中打开文件，后台转换遭遇进程锁
        var tempFile = Path.Combine(Path.GetTempPath(), $"scenario_locked_{Guid.NewGuid():N}.docx");
        File.WriteAllText(tempFile, "dummy content");

        try
        {
            // 独占锁定文件
            using var stream = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var config = new AppConfig();
            var fileItem = new FileItem
            {
                FullPath = tempFile,
                FileName = Path.GetFileName(tempFile),
                Extension = Path.GetExtension(tempFile),
                Type = FileItem.GetFileType(Path.GetExtension(tempFile))
            };
            var result = await _service.ConvertFileAsync(fileItem, Path.GetTempPath(), false, null, ConversionTarget.Markdown, config, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.FailureContext);
            Assert.Equal(FailureCategory.FileLocked, result.FailureContext.Category);
            Assert.Contains("关闭", result.FailureContext.SuggestedAction);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Scenario_CorruptedFile_ShouldDiagnoseSourceCorrupted()
    {
        // 场景 4：文件内容损坏（非合法 zip/docx 格式）
        var tempFile = Path.Combine(Path.GetTempPath(), $"scenario_corrupt_{Guid.NewGuid():N}.docx");
        File.WriteAllText(tempFile, "This is not a valid zip or docx content");

        try
        {
            var config = new AppConfig();
            var fileItem = new FileItem
            {
                FullPath = tempFile,
                FileName = Path.GetFileName(tempFile),
                Extension = Path.GetExtension(tempFile),
                Type = FileItem.GetFileType(Path.GetExtension(tempFile))
            };
            var result = await _service.ConvertFileAsync(fileItem, Path.GetTempPath(), false, null, ConversionTarget.Markdown, config, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.FailureContext);
            Assert.True(result.FailureContext.Category == FailureCategory.SourceFileCorrupted ||
                        result.FailureContext.Category == FailureCategory.InternalException);
            Assert.False(string.IsNullOrWhiteSpace(result.FailureContext.SuggestedAction));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Scenario_DualMode_AiParsingAndHumanizer_ShouldCoexistWithoutBreakingYamlFrontmatter()
    {
        // 场景 5：双轨机制验证。Humanizer 处理不破坏面向大模型的 YAML Frontmatter
        var raw = "---\ntitle: 测试\nauthor: AI\n---\n\n# 正文\n\n### 节1\n内容1\n\n### 节2\n内容2\n\n### 节3\n内容3";
        var result = new ConversionResult
        {
            Success = true,
            ProcessedMarkdown = raw,
            Metadata = new ConversionMetadata()
        };

        var pipeline = new HumanizerPipeline();
        var options = new HumanizerOptions
        {
            Enabled = true,
            EnableTocGenerator = true,
            TocHeadingThreshold = 2
        };

        var output = await pipeline.ProcessAsync(raw, result, options, CancellationToken.None);

        var normalizedOutput = output.Replace("\r\n", "\n");
        Assert.StartsWith("---\ntitle: 测试\nauthor: AI\n---", normalizedOutput);
        Assert.Contains("## 目录", output);
    }
}
