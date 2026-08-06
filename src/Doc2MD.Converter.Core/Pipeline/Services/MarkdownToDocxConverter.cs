using Doc2MD.Pipeline.Models;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// Markdown → DOCX 转换器（Phase 1 重构版）
/// 内部使用：SemanticDocumentConverter → DocxTemplate → DocxRenderer 统一管道。
/// 外部 API 保持兼容：Convert(markdownPath, outputPath, template)。
/// 所有排版决策由 DocxTemplate.Options 决定，无 switch-based 逻辑。
/// </summary>
public class MarkdownToDocxConverter
{
    private readonly TemplateService _templateService = new();
    private readonly DocxRenderer _renderer = new();

    /// <summary>支持的模板 ID 集合（由 TemplateService 驱动）</summary>
    public HashSet<string> SupportedTemplates => _templateService.GetAllTemplates()
        .Select(t => t.Id).ToHashSet();

    /// <summary>
    /// 将 Markdown 转换为 DOCX 文件
    /// </summary>
    public Md2DocxResult Convert(string markdownPath, string outputPath, string template = "official-report")
    {
        var result = new Md2DocxResult();

        try
        {
            if (!File.Exists(markdownPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Markdown 文件不存在: {markdownPath}";
                return result;
            }

            // 通过 TemplateService 获取 DocxTemplate（含兼容映射）
            var docxTemplate = _templateService.GetTemplate(template);

            // 1. Markdown → SemanticDocument（语义解析，不含样式逻辑）
            var markdown = File.ReadAllText(markdownPath);
            var semanticDoc = SemanticDocumentConverter.Convert(markdown);

            // 2. DocxTemplate → StyleApplier → DocxRenderer → DOCX（纯执行）
            _renderer.Render(semanticDoc, docxTemplate, outputPath);

            // 3. 生成格式检查报告（DocxRenderer 内部已处理）
            var reportPath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? ".",
                Path.GetFileNameWithoutExtension(outputPath) + ".format_check_report.json");

            // 读取格式检查结果
            var formatReport = ReadFormatCheckReport(reportPath);

            result.Success = true;
            result.OutputPath = outputPath;
            result.FormatCheckReportPath = File.Exists(reportPath) ? reportPath : null;
            result.FormatIssues = formatReport?.Issues ?? [];
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>读取格式检查报告文件</summary>
    private static FormatCheckReport? ReadFormatCheckReport(string reportPath)
    {
        if (!File.Exists(reportPath)) return null;
        try
        {
            var json = File.ReadAllText(reportPath);
            return System.Text.Json.JsonSerializer.Deserialize<FormatCheckReport>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower });
        }
        catch { return null; }
    }
}

// === 结果模型类（保持兼容，迁移自旧版 MarkdownToDocxConverter） ===

/// <summary>md2docx 转换结果</summary>
public class Md2DocxResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputPath { get; set; }
    public string? FormatCheckReportPath { get; set; }
    public List<FormatCheckIssue> FormatIssues { get; set; } = [];
}

/// <summary>格式检查问题</summary>
public class FormatCheckIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public string Message { get; set; } = string.Empty;
}

/// <summary>格式检查报告</summary>
public class FormatCheckReport
{
    public string Template { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; }
    public bool Passed { get; set; }
    public List<FormatCheckIssue> Issues { get; set; } = [];
}
