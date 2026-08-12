using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Doc2MD.Constants;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// DOCX文档一键排版服务
/// 符合 GB/T 9704-2012 党政机关公文格式标准
/// 支持 FormatDocPreviewSettings 配置驱动，所有设置项均有 fallback 到 Gb9704Constants
/// </summary>
public class DocxFormatter
{
    // --- 单位换算常量 ---
    private const double CmToTwips = 567.0;   // 1cm ≈ 567 twips
    private const double PtToHalfPoints = 2.0; // 1pt = 2 half-points
    private const double PtToTwips = 20.0;     // 1pt = 20 twips

    // --- 已解析的配置值（构造后不可变） ---
    private readonly string _bodyFont;
    private readonly string _titleFont;
    private readonly string _headingFont;
    private readonly string _subheadingFont;
    private readonly string _codeBlockFont;
    private readonly int _bodyFontSizeHp;    // 半磅
    private readonly int _titleFontSizeHp;   // 半磅
    private readonly int _headingFontSizeHp; // 半磅
    private readonly int _subheadingFontSizeHp; // 半磅
    private readonly int _codeBlockFontSizeHp; // 半磅
    private readonly string _lineSpacing;     // twips 字符串
    private readonly string _firstLineIndent; // twips 字符串
    private readonly int _pageMarginTop;      // twips
    private readonly int _pageMarginBottom;   // twips
    private readonly int _pageMarginLeft;     // twips
    private readonly int _pageMarginRight;    // twips
    private readonly string _beforeSpacing;   // twips 字符串
    private readonly string _afterSpacing;    // twips 字符串
    private readonly string _templatePath;   // 模板文件路径
    private readonly bool _headerFooterEnabled;
    private readonly string _headerText;
    private readonly string _footerText;

    public DocxFormatter(FormatDocPreviewSettings? settings = null)
    {
        var s = settings;
        _bodyFont = ResolveBodyFont(s);
        _titleFont = ResolveString(() => s?.TitleFont, Gb9704Constants.TitleFont);
        _headingFont = ResolveString(() => s?.HeadingFont, Gb9704Constants.HeadingFont);
        _subheadingFont = ResolveString(() => s?.SubheadingFont, Gb9704Constants.SubheadingFont);
        _codeBlockFont = ResolveString(() => s?.CodeBlockFont, "Consolas");
        _bodyFontSizeHp = ResolveDoubleToHp(() => s?.BodyFontSizePt, Gb9704Constants.BodyFontSize);
        _titleFontSizeHp = ResolveDoubleToHp(() => s?.TitleFontSizePt, Gb9704Constants.TitleFontSize);
        _headingFontSizeHp = ResolveDoubleToHp(() => s?.HeadingFontSizePt, Gb9704Constants.HeadingFontSize);
        _subheadingFontSizeHp = ResolveDoubleToHp(() => s?.SubheadingFontSizePt, Gb9704Constants.SubheadingFontSize);
        _codeBlockFontSizeHp = ResolveDoubleToHp(() => s?.CodeBlockFontSizePt, 21); // 10.5pt
        _lineSpacing = ResolveDoubleToTwipsStr(() => s?.LineSpacingPt, Gb9704Constants.LineSpacing);
        _firstLineIndent = ResolveIndentTwips(() => s?.FirstLineIndentChars, Gb9704Constants.FirstLineIndent);
        _pageMarginTop = ResolveDoubleToTwips(() => s?.PageMarginTopCm, 2098);
        _pageMarginBottom = ResolveDoubleToTwips(() => s?.PageMarginBottomCm, 1984);
        _pageMarginLeft = ResolveDoubleToTwips(() => s?.PageMarginLeftCm, 1587);
        _pageMarginRight = ResolveDoubleToTwips(() => s?.PageMarginRightCm, 1474);
        _beforeSpacing = ResolveDoubleToTwipsStr(() => s?.BeforeSpacingPt, Gb9704Constants.BeforeSpacing);
        _afterSpacing = ResolveDoubleToTwipsStr(() => s?.AfterSpacingPt, Gb9704Constants.AfterSpacing);
        _templatePath = !string.IsNullOrWhiteSpace(s?.TemplatePath) ? s!.TemplatePath : string.Empty;
        _headerFooterEnabled = s?.HeaderFooterEnabled ?? true;
        _headerText = s?.HeaderText ?? string.Empty;
        _footerText = s?.FooterText ?? string.Empty;
    }

    // === Fallback 解析辅助 ===

    private static string ResolveString(Func<string?> getter, string fallback)
        => !string.IsNullOrWhiteSpace(getter()) ? getter()! : fallback;

    private static int ResolveDoubleToHp(Func<double?> getter, int fallbackHp)
    {
        var v = getter();
        return (v.HasValue && v.Value > 0) ? (int)(v.Value * PtToHalfPoints) : fallbackHp;
    }

    private static string ResolveDoubleToTwipsStr(Func<double?> getter, string fallbackTwips)
    {
        var v = getter();
        return (v.HasValue && v.Value > 0) ? ((int)(v.Value * PtToTwips)).ToString() : fallbackTwips;
    }

    private static int ResolveDoubleToTwips(Func<double?> getter, int fallbackTwips)
    {
        var v = getter();
        return (v.HasValue && v.Value > 0) ? (int)(v.Value * CmToTwips) : fallbackTwips;
    }

    private static string ResolveIndentTwips(Func<double?> getter, int fallbackTwips)
    {
        var v = getter();
        if (!v.HasValue || v.Value <= 0) return fallbackTwips.ToString();
        // 首行缩进：字符数 × 字号(pt) × 35 twip/char（与 GB/T 9704 计算方式一致）
        return ((int)(v.Value * 280)).ToString();
    }

    /// <summary>
    /// 正文字体解析：优先 BodyFont → 旧 FontFamily 兼容 → Gb9704Constants.BodyFont
    /// </summary>
    private static string ResolveBodyFont(FormatDocPreviewSettings? s)
    {
        // 1. 优先读取结构化字段 BodyFont
        if (!string.IsNullOrWhiteSpace(s?.BodyFont)) return s!.BodyFont;
        // 2. 兼容旧配置：读取 FontFamily
        if (!string.IsNullOrWhiteSpace(s?.FontFamily)) return s!.FontFamily;
        // 3. 最终 fallback 到国标
        return Gb9704Constants.BodyFont;
    }

    // === 核心排版方法 ===

    public ConversionResult Format(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = "文件不存在";
                return result;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                result.Success = false;
                result.ErrorMessage = "未选择输出目录。";
                return result;
            }

            Directory.CreateDirectory(outputDirectory);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var sourcePath = filePath;
            LegacyConversionResult? legacy = null;
            if (extension == ".doc")
            {
                legacy = LegacyOfficeConverter.Convert(filePath, ".docx", cancellationToken);
                if (!legacy.IsSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = legacy.ErrorMessage;
                    return result;
                }

                sourcePath = legacy.ConvertedPath!;
                extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            }

            if (extension != ".docx")
            {
                result.Success = false;
                result.ErrorMessage = "仅支持 .docx 或 .doc 文档。";
                return result;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(filePath) + "_已排版.docx");
                File.Copy(sourcePath, outputPath, overwrite: true);
                FormatDocument(outputPath, cancellationToken);
                result.Success = true;
                result.OutputPath = outputPath;
            }
            finally
            {
                if (legacy is not null) LegacyOfficeConverter.Cleanup(legacy);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private void FormatDocument(string filePath, CancellationToken cancellationToken)
    {
        using var document = WordprocessingDocument.Open(filePath, true);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body == null) return;

        // 如果有模板，先从模板注入样式
        if (!string.IsNullOrWhiteSpace(_templatePath) && File.Exists(_templatePath))
        {
            InjectTemplateStyles(document.MainDocumentPart!, _templatePath);
        }

        var paragraphs = body.Elements<Paragraph>().ToList();
        var firstContentParagraph = paragraphs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(GetParagraphText(p)));

        foreach (var para in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FormatParagraph(para, para == firstContentParagraph);
        }

        // 确保文档有正确的页面设置
        EnsureDocumentSettings(body, document);

        // 添加页眉页脚
        if (_headerFooterEnabled)
        {
            ApplyHeaderFooter(document.MainDocumentPart!);
        }

        document.Save();
    }

    /// <summary>
    /// 从模板文件中复制样式定义到当前文档
    /// </summary>
    private static void InjectTemplateStyles(MainDocumentPart targetPart, string templatePath)
    {
        try
        {
            using var templateDoc = WordprocessingDocument.Open(templatePath, false);
            var templateMainPart = templateDoc.MainDocumentPart;
            if (templateMainPart?.StyleDefinitionsPart?.Styles == null) return;

            // 确保 target 有 StyleDefinitionsPart
            var targetStylesPart = targetPart.StyleDefinitionsPart;
            if (targetStylesPart == null)
            {
                targetStylesPart = targetPart.AddNewPart<StyleDefinitionsPart>();
                targetStylesPart.Styles = new Styles();
            }

            // 从模板合并样式（不覆盖已有同名样式）
            var existingStyleIds = new HashSet<string>(
                targetStylesPart.Styles?.Elements<Style>().Select(s => s.StyleId?.Value ?? "") ?? []);

            foreach (var style in templateMainPart.StyleDefinitionsPart.Styles.Elements<Style>())
            {
                if (!existingStyleIds.Contains(style.StyleId?.Value ?? ""))
                {
                    targetStylesPart.Styles!.Append(style.CloneNode(true));
                }
            }

            targetStylesPart.Styles!.Save();
        }
        catch (Exception ex)
        {
            // 模板样式注入失败不应阻断排版流程，但需记录日志便于排查
            LoggingService.Warning($"模板样式注入失败，已跳过: {ex.Message}");
        }
    }

    /// <summary>
    /// 为文档添加页眉页脚
    /// </summary>
    private void ApplyHeaderFooter(MainDocumentPart mainPart)
    {
        var hasHeader = !string.IsNullOrWhiteSpace(_headerText);
        var hasFooter = !string.IsNullOrWhiteSpace(_footerText);

        if (!hasHeader && !hasFooter) return;

        // 添加页眉
        if (hasHeader)
        {
            var headerPart = mainPart.HeaderParts.Any()
                ? mainPart.HeaderParts.First()
                : mainPart.AddNewPart<HeaderPart>();

            var header = headerPart.Header ?? new Header();
            header.RemoveAllChildren();
            var headerPara = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { After = "0", Before = "0", Line = "240", LineRule = LineSpacingRuleValues.Exact }),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = _bodyFont, HighAnsi = _bodyFont, EastAsia = _bodyFont },
                        new FontSize { Val = "18" }),
                    new Text(_headerText) { Space = SpaceProcessingModeValues.Preserve }));
            header.Append(headerPara);
            headerPart.Header = header;

            var sectPr = mainPart.Document?.Body?.Elements<SectionProperties>().FirstOrDefault();
            if (sectPr != null)
            {
                sectPr.RemoveAllChildren<HeaderReference>();
                sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) });
            }
        }

        // 添加页脚
        if (hasFooter)
        {
            var footerPart = mainPart.FooterParts.Any()
                ? mainPart.FooterParts.First()
                : mainPart.AddNewPart<FooterPart>();

            var footer = footerPart.Footer ?? new Footer();
            footer.RemoveAllChildren();
            var footerPara = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { After = "0", Before = "0", Line = "240", LineRule = LineSpacingRuleValues.Exact }),
                new Run(
                    new RunProperties(
                        new RunFonts { Ascii = _bodyFont, HighAnsi = _bodyFont, EastAsia = _bodyFont },
                        new FontSize { Val = "18" }),
                    new Text(_footerText) { Space = SpaceProcessingModeValues.Preserve }));
            footer.Append(footerPara);
            footerPart.Footer = footer;

            var sectPr = mainPart.Document?.Body?.Elements<SectionProperties>().FirstOrDefault();
            if (sectPr != null)
            {
                sectPr.RemoveAllChildren<FooterReference>();
                sectPr.PrependChild(new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) });
            }
        }
    }

    private void FormatParagraph(Paragraph paragraph, bool isDocumentTitle)
    {
        var text = GetParagraphText(paragraph);
        if (string.IsNullOrWhiteSpace(text)) return;

        if (isDocumentTitle && IsCentered(paragraph))
        {
            FormatTitle(paragraph);
            return;
        }

        var headingLevel = DetectHeadingLevel(paragraph, text);

        if (headingLevel > 0)
        {
            FormatHeading(paragraph, text, headingLevel);
            return;
        }

        // 正文段落：设置字体、行间距、首行缩进
        FormatBodyParagraph(paragraph);
    }

    private string GetParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();
        foreach (var run in paragraph.Elements<Run>())
        {
            foreach (var text in run.Elements<Text>())
            {
                sb.Append(text.Text);
            }
        }
        return sb.ToString();
    }

    private int DetectHeadingLevel(Paragraph paragraph, string text)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (!string.IsNullOrEmpty(styleId))
        {
            if (styleId.Contains("Heading1", StringComparison.OrdinalIgnoreCase) ||
                styleId.Contains("标题1", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (styleId.Contains("Heading2", StringComparison.OrdinalIgnoreCase) ||
                styleId.Contains("标题2", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (styleId.Contains("Heading3", StringComparison.OrdinalIgnoreCase) ||
                styleId.Contains("标题3", StringComparison.OrdinalIgnoreCase))
                return 3;
        }

        var normalized = text.Trim();
        if (Regex.IsMatch(normalized, @"^[一二三四五六七八九十百]+、")) return 1;
        if (Regex.IsMatch(normalized, @"^[（(][一二三四五六七八九十百]+[）)]")) return 2;
        if (Regex.IsMatch(normalized, @"^\d+[.、]")) return 3;

        return 0;
    }

    private static bool IsCentered(Paragraph paragraph) =>
        paragraph.ParagraphProperties?.Justification?.Val?.Value == JustificationValues.Center;

    private void FormatTitle(Paragraph paragraph)
    {
        UpdateParagraphProperties(paragraph, JustificationValues.Center, "0", _beforeSpacing, _afterSpacing, false);
        paragraph.ParagraphProperties!.KeepNext = new KeepNext();
        paragraph.ParagraphProperties.KeepLines = new KeepLines();
        foreach (var run in paragraph.Elements<Run>())
            UpdateRunProperties(run, _titleFontSizeHp, _titleFont, bold: false);
    }

    private void FormatHeading(Paragraph paragraph, string text, int level)
    {
        var numberedText = text.Trim();

        // 根据级别选择字体和字号
        var (font, sizeHp, alignment, beforeSpacing) = ResolveHeadingFormat(level);

        UpdateParagraphProperties(paragraph, alignment, "0", beforeSpacing, _afterSpacing, false);
        paragraph.ParagraphProperties!.KeepNext = new KeepNext();
        paragraph.ParagraphProperties.KeepLines = new KeepLines();

        // 更新字体样式
        foreach (var run in paragraph.Elements<Run>())
        {
            UpdateRunProperties(run, sizeHp, font, bold: level is 1 or 2);
        }

        // 更新文本内容
        UpdateRunTexts(paragraph, numberedText);
    }

    private void FormatBodyParagraph(Paragraph paragraph)
    {
        // 设置首行缩进
        UpdateParagraphProperties(paragraph, JustificationValues.Both, _firstLineIndent, _beforeSpacing, _afterSpacing, false);

        // 设置字体
        foreach (var run in paragraph.Elements<Run>())
        {
            UpdateRunProperties(run, _bodyFontSizeHp, _bodyFont);
        }
    }

    /// <summary>
    /// 根据标题级别解析格式（全部从配置字段读取）
    /// </summary>
    private (string Font, int SizeHp, JustificationValues Alignment, string BeforeSpacing) ResolveHeadingFormat(int level)
    {
        return level switch
        {
            1 => (_titleFont, _titleFontSizeHp, JustificationValues.Center, "240"),
            2 => (_headingFont, _headingFontSizeHp, JustificationValues.Left, "160"),
            3 => (_subheadingFont, _subheadingFontSizeHp, JustificationValues.Left, "120"),
            4 => (_bodyFont, _bodyFontSizeHp, JustificationValues.Left, "80"),
            _ => (_bodyFont, _bodyFontSizeHp, JustificationValues.Left, "80")
        };
    }

    private void UpdateParagraphProperties(Paragraph paragraph, JustificationValues alignment,
        string firstLine, string before, string after, bool useAutoSpacing)
    {
        var props = paragraph.ParagraphProperties;
        if (props == null)
        {
            props = new ParagraphProperties();
            paragraph.InsertAt(props, 0);
        }

        // 移除旧的属性
        props.RemoveAllChildren<Justification>();
        props.RemoveAllChildren<SpacingBetweenLines>();
        props.RemoveAllChildren<Indentation>();

        // 添加新属性
        var spacingRule = useAutoSpacing ? LineSpacingRuleValues.Auto : LineSpacingRuleValues.Exact;
        var spacingLine = useAutoSpacing ? Gb9704Constants.AutoLineSpacing : _lineSpacing;

        props.Append(new Justification { Val = alignment });
        props.Append(new SpacingBetweenLines
        {
            Line = spacingLine,
            LineRule = spacingRule,
            Before = before,
            After = after
        });
        props.Append(new Indentation { FirstLine = firstLine });
    }

    private void UpdateRunProperties(Run run, int size, string font, bool bold = false, bool italic = false)
    {
        var props = run.RunProperties;
        if (props == null)
        {
            props = new RunProperties();
            run.InsertAt(props, 0);
        }

        // 移除旧的字体和大小设置
        props.RemoveAllChildren<RunFonts>();
        props.RemoveAllChildren<FontSize>();
        props.RemoveAllChildren<FontSizeComplexScript>();
        props.RemoveAllChildren<Bold>();
        props.RemoveAllChildren<Italic>();

        // 添加新设置
        props.Append(new RunFonts { Ascii = font, HighAnsi = font, EastAsia = font, ComplexScript = font });
        props.Append(new FontSize { Val = size.ToString() });
        props.Append(new FontSizeComplexScript { Val = size.ToString() });

        if (bold) props.Append(new Bold());
        if (italic) props.Append(new Italic());
    }

    private void UpdateRunTexts(Paragraph paragraph, string newText)
    {
        // 清除所有现有run的内容
        var runs = paragraph.Elements<Run>().ToList();

        if (runs.Count == 0)
        {
            // 如果没有run，创建一个
            var newRun = new Run(
                new RunProperties(
                    new RunFonts { Ascii = _bodyFont, HighAnsi = _bodyFont, EastAsia = _bodyFont },
                    new FontSize { Val = _bodyFontSizeHp.ToString() }
                ),
                new Text(newText) { Space = SpaceProcessingModeValues.Preserve }
            );
            paragraph.Append(newRun);
        }
        else
        {
            // 清空第一个run并设置新文本
            var firstRun = runs[0];
            var props = firstRun.RunProperties?.CloneNode(true) as RunProperties
                ?? new RunProperties(
                    new RunFonts { Ascii = _bodyFont, HighAnsi = _bodyFont, EastAsia = _bodyFont },
                    new FontSize { Val = _bodyFontSizeHp.ToString() }
                );

            firstRun.RemoveAllChildren<Text>();
            firstRun.Append(new Text(newText) { Space = SpaceProcessingModeValues.Preserve });

            // 移除其他run
            for (int i = 1; i < runs.Count; i++)
            {
                runs[i].Remove();
            }
        }
    }

    private void EnsureDocumentSettings(Body body, WordprocessingDocument document)
    {
        // 确保有节属性
        var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
        if (sectPr == null)
        {
            sectPr = new SectionProperties();
            body.Append(sectPr);
        }

        // 设置A4纸张
        var pageSize = sectPr.Elements<PageSize>().FirstOrDefault();
        if (pageSize == null)
        {
            sectPr.PrependChild(new PageSize { Width = 11906U, Height = 16838U });
        }
        else
        {
            pageSize.Width = 11906U;
            pageSize.Height = 16838U;
        }

        // 设置页边距（使用配置值）
        var pageMargin = sectPr.Elements<PageMargin>().FirstOrDefault();
        if (pageMargin == null)
        {
            sectPr.PrependChild(new PageMargin
            {
                Top = _pageMarginTop,
                Bottom = _pageMarginBottom,
                Left = (UInt32Value)(uint)_pageMarginLeft,
                Right = (UInt32Value)(uint)_pageMarginRight,
                Header = 851U,
                Footer = 851U,
                Gutter = 0U
            });
        }
        else
        {
            pageMargin.Top = _pageMarginTop;
            pageMargin.Bottom = _pageMarginBottom;
            pageMargin.Left = (UInt32Value)(uint)_pageMarginLeft;
            pageMargin.Right = (UInt32Value)(uint)_pageMarginRight;
        }

        // 设置文档网格
        var docGrid = sectPr.Elements<DocGrid>().FirstOrDefault();
        if (docGrid == null)
        {
            sectPr.Append(new DocGrid { Type = DocGridValues.Lines, LinePitch = 312 });
        }
    }
}
