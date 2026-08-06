using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Doc2MD.Pipeline.Models;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// 独立格式检查服务：基于模板排版选项检查 DOCX 格式合规性。
/// 检查项覆盖 GB/T 9704-2012 核心要求，可独立于 DocxRenderer 调用。
/// </summary>
public class DocxFormatChecker
{
    /// <summary>
    /// 检查 DOCX 文件格式合规性
    /// </summary>
    public FormatCheckReport Check(string docxPath, string templateId)
    {
        var templateService = new TemplateService();
        var template = templateService.GetTemplate(templateId);
        var opts = template.Options;
        var issues = new List<FormatCheckIssue>();

        try
        {
            using var doc = WordprocessingDocument.Open(docxPath, false);
            var mainPart = doc.MainDocumentPart;
            var body = mainPart?.Document?.Body;

            if (body == null)
            {
                issues.Add(new FormatCheckIssue { Code = "F_EMPTY_DOCUMENT", Severity = "high", Message = "文档内容为空" });
                return new FormatCheckReport { Template = templateId, Issues = issues, Passed = false };
            }

            // ---- 1. 页面尺寸 A4 ----
            var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
            var pageSize = sectPr?.Elements<PageSize>().FirstOrDefault();
            if (pageSize == null || pageSize.Width?.Value != 11906 || pageSize.Height?.Value != 16838)
                issues.Add(new FormatCheckIssue { Code = "F_PAGE_SIZE", Severity = "medium", Message = "页面尺寸非 A4（210×297mm）" });

            // ---- 2. 页边距 ----
            var pageMargin = sectPr?.Elements<PageMargin>().FirstOrDefault();
            if (pageMargin != null)
            {
                var topCm = (pageMargin.Top?.Value ?? 0) / 567.0;
                var bottomCm = (pageMargin.Bottom?.Value ?? 0) / 567.0;
                var leftCm = (pageMargin.Left?.Value ?? 0) / 567.0;
                var rightCm = (pageMargin.Right?.Value ?? 0) / 567.0;

                if (Math.Abs(topCm - opts.PageMarginTopCm) > 0.2)
                    issues.Add(new FormatCheckIssue { Code = "F_MARGIN_TOP", Severity = "low", Message = $"上边距 {topCm:F1}cm，预期 {opts.PageMarginTopCm:F1}cm" });
                if (Math.Abs(bottomCm - opts.PageMarginBottomCm) > 0.2)
                    issues.Add(new FormatCheckIssue { Code = "F_MARGIN_BOTTOM", Severity = "low", Message = $"下边距 {bottomCm:F1}cm，预期 {opts.PageMarginBottomCm:F1}cm" });
                if (Math.Abs(leftCm - opts.PageMarginLeftCm) > 0.2)
                    issues.Add(new FormatCheckIssue { Code = "F_MARGIN_LEFT", Severity = "low", Message = $"左边距 {leftCm:F1}cm，预期 {opts.PageMarginLeftCm:F1}cm" });
                if (Math.Abs(rightCm - opts.PageMarginRightCm) > 0.2)
                    issues.Add(new FormatCheckIssue { Code = "F_MARGIN_RIGHT", Severity = "low", Message = $"右边距 {rightCm:F1}cm，预期 {opts.PageMarginRightCm:F1}cm" });
            }
            else
            {
                issues.Add(new FormatCheckIssue { Code = "F_MARGIN_MISSING", Severity = "medium", Message = "未找到页边距设置" });
            }

            // ---- 3. 标题字体检查 ----
            var firstPara = body.Elements<Paragraph>().FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.InnerText));
            if (firstPara != null)
            {
                var titleRun = firstPara.Elements<Run>().FirstOrDefault();
                if (titleRun?.RunProperties?.RunFonts?.EastAsia?.Value != null)
                {
                    var titleFont = titleRun.RunProperties.RunFonts.EastAsia.Value;
                    if (titleFont != opts.TitleFont && titleFont != "方正小标宋简体" && titleFont != "宋体")
                        issues.Add(new FormatCheckIssue { Code = "F_TITLE_FONT", Severity = "medium", Message = $"标题字体「{titleFont}」，预期「{opts.TitleFont}」" });
                }
                else
                {
                    issues.Add(new FormatCheckIssue { Code = "F_TITLE_FONT_MISSING", Severity = "low", Message = "标题未设置中文字体" });
                }

                var titleSz = titleRun?.RunProperties?.FontSize;
                if (titleSz != null && int.TryParse(titleSz.Val?.Value, out var szVal))
                {
                    var titlePt = szVal / 2.0;
                    if (Math.Abs(titlePt - opts.TitleFontSizePt) > 1.0)
                        issues.Add(new FormatCheckIssue { Code = "F_TITLE_SIZE", Severity = "low", Message = $"标题字号 {titlePt:F0}pt，预期 {opts.TitleFontSizePt:F0}pt" });
                }
            }

            // ---- 4. 正文字体与字号检查 ----
            var bodyParagraphs = body.Elements<Paragraph>()
                .Where(p => !string.IsNullOrWhiteSpace(p.InnerText))
                .Skip(1) // 跳过标题
                .ToList();

            var nonCompliantBodyRuns = 0;
            var nonCompliantBodySizeRuns = 0;
            foreach (var para in bodyParagraphs.Take(20)) // 抽样前20段
            {
                foreach (var run in para.Elements<Run>())
                {
                    if (string.IsNullOrWhiteSpace(run.InnerText)) continue;
                    var fonts = run.RunProperties?.RunFonts;
                    if (fonts?.EastAsia?.Value != null && fonts.EastAsia.Value != opts.BodyFont
                        && fonts.EastAsia.Value != "仿宋" && fonts.EastAsia.Value != "仿宋_GB2312")
                        nonCompliantBodyRuns++;
                    var sz = run.RunProperties?.FontSize;
                    if (sz != null && int.TryParse(sz.Val?.Value, out var bSz) && Math.Abs(bSz / 2.0 - opts.BodyFontSizePt) > 1.0)
                        nonCompliantBodySizeRuns++;
                }
            }
            if (nonCompliantBodyRuns > 3)
                issues.Add(new FormatCheckIssue { Code = "F_BODY_FONT", Severity = "medium", Message = $"正文存在非{opts.BodyFont}字体段落（{nonCompliantBodyRuns} 处）" });
            if (nonCompliantBodySizeRuns > 3)
                issues.Add(new FormatCheckIssue { Code = "F_BODY_SIZE", Severity = "low", Message = $"正文存在非预期字号段落（{nonCompliantBodySizeRuns} 处），预期 {opts.BodyFontSizePt:F0}pt" });

            // ---- 5. 行距检查（修复：使用 TryParse 替代 int.Parse）----
            var nonStandardLineSpacing = 0;
            foreach (var para in bodyParagraphs.Take(20))
            {
                var spacing = para.ParagraphProperties?.SpacingBetweenLines;
                if (spacing?.Line?.Value != null)
                {
                    if (int.TryParse(spacing.Line.Value, out var lineVal))
                    {
                        if (spacing.LineRule?.Value == LineSpacingRuleValues.Exact)
                        {
                            var linePt = lineVal / 20.0;
                            if (Math.Abs(linePt - opts.LineSpacingPt) > 1.0)
                                nonStandardLineSpacing++;
                        }
                        else if (spacing.LineRule?.Value == LineSpacingRuleValues.AtLeast)
                        {
                            // AtLeast 模式行距通常不精确，标记为低风险
                        }
                    }
                    // 无法解析行距值时静默跳过（不再抛异常）
                }
            }
            if (nonStandardLineSpacing > 3)
                issues.Add(new FormatCheckIssue { Code = "F_LINE_SPACING", Severity = "low", Message = $"存在非标准行距段落（{nonStandardLineSpacing} 处），预期固定 {opts.LineSpacingPt:F0}磅" });

            // ---- 6. 首行缩进检查 ----
            var noIndentParas = 0;
            foreach (var para in bodyParagraphs.Take(20))
            {
                var indent = para.ParagraphProperties?.Indentation;
                if (indent?.FirstLine?.Value == null && indent?.FirstLineChars?.Value == null)
                {
                    // 检查是否为标题样式（标题不需要缩进）
                    var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    if (styleId != "Heading1" && styleId != "Heading2" && styleId != "Heading3")
                        noIndentParas++;
                }
            }
            if (noIndentParas > 5)
                issues.Add(new FormatCheckIssue { Code = "F_FIRST_INDENT", Severity = "low", Message = $"正文未设置首行缩进（{noIndentParas} 段），预期缩进 {opts.FirstLineIndentChars:F0} 字符" });

            // ---- 7. 表格宽度检查（修复：空表安全 + 实际页宽判断）----
            var pageUsableWidthTwips = 11906 - (int)Math.Round(opts.PageMarginLeftCm * 567.0) - (int)Math.Round(opts.PageMarginRightCm * 567.0);
            foreach (var table in body.Elements<Table>())
            {
                var rows = table.Elements<TableRow>().ToList();
                if (rows.Count == 0) continue; // 空表格安全跳过

                var maxCols = rows.Max(r => r.Elements<TableCell>().Count());

                // 基于页面可用宽度估算：假设每列最小 600 twips（约 1cm）
                var minColWidthTwips = 600;
                var estimatedMinTableWidth = maxCols * minColWidthTwips;
                if (estimatedMinTableWidth > pageUsableWidthTwips)
                    issues.Add(new FormatCheckIssue { Code = "F_TABLE_TOO_WIDE", Severity = "medium", Message = $"表格列数过多（{maxCols} 列），可能超出页面可用宽度（{pageUsableWidthTwips / 567.0:F1}cm）" });
            }

            // ---- 8. 文档网格检查 ----
            var docGrid = sectPr?.Elements<DocGrid>().FirstOrDefault();
            if (docGrid?.LinePitch?.Value != null)
            {
                var usableHeight = 16838 - (int)Math.Round(opts.PageMarginTopCm * 567.0) - (int)Math.Round(opts.PageMarginBottomCm * 567.0);
                var expectedLinePitch = usableHeight / opts.LinesPerPage;
                var actualLinePitch = docGrid.LinePitch.Value;
                if (Math.Abs(actualLinePitch - expectedLinePitch) > 20)
                    issues.Add(new FormatCheckIssue { Code = "F_DOC_GRID", Severity = "low", Message = $"文档网格行距 {actualLinePitch}twips，预期 {expectedLinePitch}twips（{opts.LinesPerPage}行/页）" });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new FormatCheckIssue { Code = "F_CHECK_ERROR", Severity = "high", Message = $"格式检查异常: {ex.Message}" });
        }

        return new FormatCheckReport
        {
            Template = templateId,
            CheckedAt = DateTimeOffset.UtcNow,
            Issues = issues,
            Passed = issues.All(i => i.Severity != "high")
        };
    }

    /// <summary>
    /// 检查格式并保存报告到输出目录
    /// </summary>
    public FormatCheckReport CheckAndSaveReport(string docxPath, string templateId)
    {
        var report = Check(docxPath, templateId);

        var reportPath = Path.Combine(
            Path.GetDirectoryName(docxPath) ?? ".",
            Path.GetFileNameWithoutExtension(docxPath) + ".format_check_report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }), System.Text.Encoding.UTF8);

        return report;
    }
}
