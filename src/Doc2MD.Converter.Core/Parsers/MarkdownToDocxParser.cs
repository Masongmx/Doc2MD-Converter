using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Doc2MD.Constants;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Parsers;

/// <summary>
/// 将常用 Markdown 结构生成符合中文党政机关公文常用版式的 DOCX。
/// 支持标题、正文、列表、引用、代码块、表格及粗体/斜体/删除线。
/// 符合 GB/T 9704-2012 党政机关公文格式标准。
/// 支持 MarkdownToDocxPreviewSettings 配置驱动，所有设置项均有 fallback 到 Gb9704Constants。
/// </summary>
public sealed class MarkdownToDocxParser : IDocumentParser
{
    // --- 单位换算常量 ---
    private const double CmToTwips = 567.0;
    private const double PtToHalfPoints = 2.0;
    private const double PtToTwips = 20.0;

    public FileType SupportedType => FileType.Markdown;
    public ConversionTarget Target => ConversionTarget.OfficialDocx;

    /// <summary>
    /// 预览设置（由调用方在 Parse 前设置），null 则全部使用 Gb9704Constants 默认值
    /// </summary>
    public MarkdownToDocxPreviewSettings? PreviewSettings { get; set; }

    public bool CanParse(string filePath) => Path.GetExtension(filePath).Equals(".md", StringComparison.OrdinalIgnoreCase)
        || Path.GetExtension(filePath).Equals(".markdown", StringComparison.OrdinalIgnoreCase);

    public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var markdown = ReadMarkdown(filePath);
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(filePath) + ".docx");
            WriteDocument(markdown, outputPath, cancellationToken);
            result.Success = true;
            result.OutputPath = outputPath;
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

    // === 配置解析（带 fallback） ===

    private string BodyFont => ResolveStr(() => PreviewSettings?.BodyFont, Gb9704Constants.BodyFont);
    private string TitleFont => ResolveStr(() => PreviewSettings?.TitleFont, Gb9704Constants.TitleFont);
    private string HeadingFont => ResolveStr(() => PreviewSettings?.HeadingFont, Gb9704Constants.HeadingFont);
    private string SubheadingFont => ResolveStr(() => PreviewSettings?.SubheadingFont, Gb9704Constants.SubheadingFont);
    private string CodeBlockFont => ResolveStr(() => PreviewSettings?.CodeBlockFont, "Consolas");

    private int BodyFontSizeHp => ResolveD2Hp(() => PreviewSettings?.BodyFontSizePt, Gb9704Constants.BodyFontSize);
    private int TitleFontSizeHp => ResolveD2Hp(() => PreviewSettings?.TitleFontSizePt, Gb9704Constants.TitleFontSize);
    private int HeadingFontSizeHp => ResolveD2Hp(() => PreviewSettings?.HeadingFontSizePt, Gb9704Constants.HeadingFontSize);
    private int SubheadingFontSizeHp => ResolveD2Hp(() => PreviewSettings?.SubheadingFontSizePt, Gb9704Constants.SubheadingFontSize);
    private int CodeBlockFontSizeHp => ResolveD2Hp(() => PreviewSettings?.CodeBlockFontSizePt, 21); // 10.5pt

    private string LineSpacing => ResolveD2TwipsStr(() => PreviewSettings?.LineSpacingPt, Gb9704Constants.LineSpacing);
    private int FirstLineIndent => ResolveIndent(() => PreviewSettings?.FirstLineIndentChars, Gb9704Constants.FirstLineIndent);
    private string BeforeSpacing => ResolveD2TwipsStr(() => PreviewSettings?.BeforeSpacingPt, Gb9704Constants.BeforeSpacing);
    private string AfterSpacing => ResolveD2TwipsStr(() => PreviewSettings?.AfterSpacingPt, Gb9704Constants.AfterSpacing);

    private int PageMarginTop => ResolveD2TwipsCm(() => PreviewSettings?.PageMarginTopCm, 2098);
    private int PageMarginBottom => ResolveD2TwipsCm(() => PreviewSettings?.PageMarginBottomCm, 1984);
    private int PageMarginLeft => ResolveD2TwipsCm(() => PreviewSettings?.PageMarginLeftCm, 1587);
    private int PageMarginRight => ResolveD2TwipsCm(() => PreviewSettings?.PageMarginRightCm, 1474);

    private static string ResolveStr(Func<string?> getter, string fallback)
        => !string.IsNullOrWhiteSpace(getter()) ? getter()! : fallback;

    private static int ResolveD2Hp(Func<double?> getter, int fallback)
    { var v = getter(); return (v.HasValue && v.Value > 0) ? (int)(v.Value * PtToHalfPoints) : fallback; }

    private static string ResolveD2TwipsStr(Func<double?> getter, string fallback)
    { var v = getter(); return (v.HasValue && v.Value > 0) ? ((int)(v.Value * PtToTwips)).ToString() : fallback; }

    private static int ResolveD2TwipsCm(Func<double?> getter, int fallback)
    { var v = getter(); return (v.HasValue && v.Value > 0) ? (int)(v.Value * CmToTwips) : fallback; }

    private static int ResolveIndent(Func<double?> getter, int fallback)
    { var v = getter(); return (v.HasValue && v.Value > 0) ? (int)(v.Value * 280) : fallback; }

    // === 标题编号 ===

    private sealed class HeadingCounter
    {
        public int Level1;
        public int Level2;
        public int Level3;
        public int Level4;

        public void ResetLowerLevels(int level)
        {
            switch (level)
            {
                case 1: Level1 = 0; Level2 = 0; Level3 = 0; Level4 = 0; break;
                case 2: Level2 = 0; Level3 = 0; Level4 = 0; break;
                case 3: Level3 = 0; Level4 = 0; break;
                case 4: Level4 = 0; break;
            }
        }
    }

    private static string GetChineseNumber(int number)
    {
        var chnNums = new[] { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        if (number <= 10) return chnNums[number - 1];
        if (number < 20) return "十" + (number == 10 ? "" : chnNums[number - 11]);
        if (number < 100)
        {
            var tens = number / 10;
            var ones = number % 10;
            return chnNums[tens - 1] + "十" + (ones == 0 ? "" : chnNums[ones - 1]);
        }
        return number.ToString();
    }

    private static string GetNumberedHeading(HeadingCounter counter, string text, int level)
    {
        counter.ResetLowerLevels(level);

        return level switch
        {
            1 => text,
            2 => GetChineseNumber(++counter.Level1) + "、" + text,
            3 => "（" + GetChineseNumber(++counter.Level2) + "）" + text,
            4 => (++counter.Level3).ToString() + ". " + text,
            5 => "(" + (++counter.Level4) + ") " + text,
            _ => text
        };
    }

    /// <summary>
    /// 识别中文编号标题级别（与 DocxFormatter.DetectHeadingLevel 对齐）。
    /// "一、xxx" → 1，"（一）xxx" → 2，"1.xxx" 或 "1、xxx" → 3，否则返回 0。
    /// </summary>
    private static int DetectChineseHeadingLevel(string line)
    {
        var normalized = line.Trim();
        if (string.IsNullOrEmpty(normalized)) return 0;

        if (Regex.IsMatch(normalized, @"^[一二三四五六七八九十百]+、")) return 1;
        if (Regex.IsMatch(normalized, @"^[（(][一二三四五六七八九十百]+[）)]")) return 2;
        if (Regex.IsMatch(normalized, @"^\d+[.、]")) return 3;

        return 0;
    }

    private static string ReadMarkdown(string path)
    {
        return TextFileReader.ReadAllText(path);
    }

    private void WriteDocument(string markdown, string outputPath, CancellationToken cancellationToken)
    {
        var counter = new HeadingCounter();
        var templatePath = ResolveStr(() => PreviewSettings?.TemplatePath, string.Empty);
        var useTemplate = !string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath);

        WordprocessingDocument document;
        MainDocumentPart mainPart;
        Body body;

        if (useTemplate)
        {
            // 克隆模板：复制模板文件到输出路径，打开后清空正文
            File.Copy(templatePath!, outputPath, overwrite: true);
            document = WordprocessingDocument.Open(outputPath, true);
            mainPart = document.MainDocumentPart!;
            body = mainPart.Document!.Body!;

            // 清空模板中的正文内容（保留 SectionProperties）
            var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
            body.RemoveAllChildren();
            if (sectPr != null) body.Append(sectPr);
        }
        else
        {
            document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
            mainPart = document.AddMainDocumentPart();
            body = new Body();
            mainPart.Document = new Document(body);
        }

        // 预处理：过滤不应写入 DOCX 的技术标记
        var cleaned = StripDocxPollutants(markdown);

        var lines = cleaned.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var inCodeBlock = false;
        var code = new StringBuilder();
        var headingBookmarks = new List<(string Id, string Text, int Level)>();

        for (var index = 0; index < lines.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    body.Append(CreateCodeParagraph(code.ToString().TrimEnd()));
                    code.Clear();
                }
                inCodeBlock = !inCodeBlock;
                continue;
            }
            if (inCodeBlock)
            {
                code.AppendLine(line);
                continue;
            }
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (TryReadTable(lines, ref index, out var rows))
            {
                body.Append(CreateTable(rows));
                continue;
            }

            var heading = Regex.Match(line, @"^(#{1,6})\s+(.+?)\s*$");
            if (heading.Success)
            {
                var level = heading.Groups[1].Value.Length;
                var numberedText = GetNumberedHeading(counter, heading.Groups[2].Value, level);
                var para = CreateHeading(numberedText, level);
                body.Append(para);
                // 收集标题信息用于目录
                headingBookmarks.Add(($"_Toc{headingBookmarks.Count + 1}", numberedText, level));
                continue;
            }

            // 中文编号标题自动识别（与 DocxFormatter.DetectHeadingLevel 一致）
            var cnHeadingLevel = DetectChineseHeadingLevel(line);
            if (cnHeadingLevel > 0)
            {
                var para = CreateHeading(line.Trim(), cnHeadingLevel);
                body.Append(para);
                headingBookmarks.Add(($"_Toc{headingBookmarks.Count + 1}", line.Trim(), cnHeadingLevel));
                continue;
            }

            var ordered = Regex.Match(line, @"^\s*(\d+)[.、]\s+(.+)$");
            if (ordered.Success)
            {
                body.Append(CreateBodyParagraph($"{ordered.Groups[1].Value}. {ordered.Groups[2].Value}"));
                continue;
            }

            var unordered = Regex.Match(line, @"^\s*[-*+]\s+(.+)$");
            if (unordered.Success)
            {
                body.Append(CreateBodyParagraph($"• {unordered.Groups[1].Value}"));
                continue;
            }

            if (line.StartsWith(">", StringComparison.Ordinal))
            {
                body.Append(CreateQuoteParagraph(line.TrimStart('>', ' ')));
                continue;
            }

            body.Append(CreateBodyParagraph(line));
        }

        if (inCodeBlock && code.Length > 0)
            body.Append(CreateCodeParagraph(code.ToString().TrimEnd()));

        // 生成目录（在正文前插入）
        var generateToc = PreviewSettings?.GenerateToc == true;
        if (generateToc && headingBookmarks.Count > 0)
        {
            InsertTableOfContents(body, mainPart);
        }

        // 页面设置使用配置值（模板模式下保留模板的 SectionProperties，仅覆盖边距）
        if (useTemplate)
        {
            ApplyPageSetupToExistingSection(body);
        }
        else
        {
            body.Append(new SectionProperties(
                new PageSize { Width = 11906U, Height = 16838U },
                new PageMargin { Top = PageMarginTop, Bottom = PageMarginBottom, Left = (UInt32Value)(uint)PageMarginLeft, Right = (UInt32Value)(uint)PageMarginRight, Header = 851U, Footer = 851U, Gutter = 0U },
                new Columns { Space = "425" },
                new DocGrid { Type = DocGridValues.Lines, LinePitch = 312 }));
        }

        // 添加页眉页脚
        ApplyHeaderFooter(mainPart);

        mainPart.Document.Save();
        document.Dispose();
    }

    /// <summary>
    /// 在文档正文最前面插入目录域代码
    /// </summary>
    private static void InsertTableOfContents(Body body, MainDocumentPart mainPart)
    {
        // 确保有样式定义
        var stylesPart = mainPart.StyleDefinitionsPart;
        if (stylesPart == null)
        {
            stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles();
        }

        // 确保有 TOC 标题样式
        var tocHeading = stylesPart.Styles?.Elements<Style>().FirstOrDefault(s => s.StyleId == "TOCHeading");
        if (tocHeading == null)
        {
            stylesPart.Styles!.Append(new Style
            {
                StyleId = "TOCHeading",
                Type = StyleValues.Paragraph,
                StyleRunProperties = new StyleRunProperties
                {
                    RunFonts = new RunFonts { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体" },
                    FontSize = new FontSize { Val = "36" },
                    Bold = new Bold()
                }
            });
        }

        // 创建目录段落
        var tocPara = new Paragraph();
        var tocProps = new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { After = "200" });
        tocProps.ParagraphStyleId = new ParagraphStyleId { Val = "TOCHeading" };
        tocPara.ParagraphProperties = tocProps;
        tocPara.Append(new Run(new Text("目  录") { Space = SpaceProcessingModeValues.Preserve }));

        // TOC 域代码
        var tocFieldPara = new Paragraph();
        var tocFieldRun = new Run();
        tocFieldRun.Append(new FieldChar { FieldCharType = FieldCharValues.Begin });
        tocFieldPara.Append(tocFieldRun);

        var tocInstrRun = new Run();
        tocInstrRun.Append(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
        tocFieldPara.Append(tocInstrRun);

        var tocEndRun = new Run();
        tocEndRun.Append(new FieldChar { FieldCharType = FieldCharValues.End });
        tocFieldPara.Append(tocEndRun);

        // 在 body 最前面插入（SectionProperties 之前）
        var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
        if (sectPr != null)
        {
            sectPr.InsertBeforeSelf(tocFieldPara);
            sectPr.InsertBeforeSelf(tocPara);
        }
        else
        {
            body.InsertBefore(tocFieldPara, body.FirstChild);
            body.InsertBefore(tocPara, body.FirstChild);
        }
    }

    /// <summary>
    /// 模板模式下，更新现有 SectionProperties 的页边距和页面尺寸
    /// </summary>
    private void ApplyPageSetupToExistingSection(Body body)
    {
        var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
        if (sectPr == null) return;

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

        var pageMargin = sectPr.Elements<PageMargin>().FirstOrDefault();
        if (pageMargin == null)
        {
            sectPr.PrependChild(new PageMargin
            {
                Top = PageMarginTop, Bottom = PageMarginBottom,
                Left = (UInt32Value)(uint)PageMarginLeft, Right = (UInt32Value)(uint)PageMarginRight,
                Header = 851U, Footer = 851U, Gutter = 0U
            });
        }
        else
        {
            pageMargin.Top = PageMarginTop;
            pageMargin.Bottom = PageMarginBottom;
            pageMargin.Left = (UInt32Value)(uint)PageMarginLeft;
            pageMargin.Right = (UInt32Value)(uint)PageMarginRight;
        }
    }

    /// <summary>
    /// 根据配置添加页眉页脚到文档
    /// </summary>
    private void ApplyHeaderFooter(MainDocumentPart mainPart)
    {
        var headerText = ResolveStr(() => PreviewSettings?.HeaderText, string.Empty);
        var footerText = ResolveStr(() => PreviewSettings?.FooterText, string.Empty);

        if (string.IsNullOrWhiteSpace(headerText) && string.IsNullOrWhiteSpace(footerText)) return;

        // 添加页眉
        if (!string.IsNullOrWhiteSpace(headerText))
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
                        new RunFonts { Ascii = BodyFont, HighAnsi = BodyFont, EastAsia = BodyFont },
                        new FontSize { Val = "18" }),
                    new Text(headerText) { Space = SpaceProcessingModeValues.Preserve }));
            header.Append(headerPara);
            headerPart.Header = header;

            // 关联到 SectionProperties
            var sectPr = mainPart.Document?.Body?.Elements<SectionProperties>().FirstOrDefault();
            if (sectPr != null)
            {
                sectPr.RemoveAllChildren<HeaderReference>();
                sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) });
            }
        }

        // 添加页脚
        if (!string.IsNullOrWhiteSpace(footerText))
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
                        new RunFonts { Ascii = BodyFont, HighAnsi = BodyFont, EastAsia = BodyFont },
                        new FontSize { Val = "18" }),
                    new Text(footerText) { Space = SpaceProcessingModeValues.Preserve }));
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

    private Paragraph CreateHeading(string text, int level)
    {
        // 根据级别选择配置化字体/字号，全部从 PreviewSettings 读取
        var (font, sizeHp, alignment, beforeSpacing) = level switch
        {
            1 => (TitleFont, TitleFontSizeHp, JustificationValues.Center, "240"),
            2 => (HeadingFont, HeadingFontSizeHp, JustificationValues.Left, "160"),
            3 => (SubheadingFont, SubheadingFontSizeHp, JustificationValues.Left, "120"),
            4 => (BodyFont, BodyFontSizeHp, JustificationValues.Left, "80"),
            _ => (BodyFont, BodyFontSizeHp, JustificationValues.Left, "80")
        };
        var paragraph = CreateParagraphWithSpacing(text, sizeHp, font, alignment, 0, 0, beforeSpacing, false);
        paragraph.ParagraphProperties!.KeepNext = new KeepNext();
        paragraph.ParagraphProperties.KeepLines = new KeepLines();
        if (level <= 3)
            paragraph.ParagraphProperties.Shading = new Shading { Val = ShadingPatternValues.Clear, Fill = "auto" };
        return paragraph;
    }

    /// <summary>
    /// 创建正文段落 - 符合党政机关公文格式（首行缩进、固定行间距）
    /// </summary>
    private Paragraph CreateBodyParagraph(string text)
    {
        var paragraph = CreateParagraphWithSpacing(text, BodyFontSizeHp, BodyFont, JustificationValues.Both,
            FirstLineIndent, 0, BeforeSpacing, false);
        return paragraph;
    }

    /// <summary>
    /// 创建引用段落
    /// </summary>
    private Paragraph CreateQuoteParagraph(string text)
    {
        var paragraph = CreateParagraphWithSpacing(text, SubheadingFontSizeHp, SubheadingFont,
            JustificationValues.Left, 0, 0, "120", false);
        paragraph.ParagraphProperties!.Indentation = new Indentation { Left = "567" };
        paragraph.ParagraphProperties.Shading = new Shading { Val = ShadingPatternValues.Clear, Fill = "auto" };
        return paragraph;
    }

    /// <summary>
    /// 创建代码块段落
    /// </summary>
    private Paragraph CreateCodeParagraph(string text)
    {
        var paragraph = CreateParagraphWithSpacing(text, CodeBlockFontSizeHp, CodeBlockFont,
            JustificationValues.Left, 0, 0, "240", false);
        paragraph.ParagraphProperties!.Shading = new Shading { Val = ShadingPatternValues.Clear, Fill = "F5F5F5" };
        paragraph.ParagraphProperties.Indentation = new Indentation { Left = "567" };
        return paragraph;
    }

    private Paragraph CreateParagraphWithSpacing(string text, int size, string font,
        JustificationValues alignment, int firstLine, int after, string before, bool useAutoSpacing)
    {
        var spacingLineRule = useAutoSpacing ? LineSpacingRuleValues.Auto : LineSpacingRuleValues.Exact;
        var spacingLine = useAutoSpacing ? Gb9704Constants.AutoLineSpacing : LineSpacing;
        var properties = new ParagraphProperties(
            new Justification { Val = alignment },
            new SpacingBetweenLines
            {
                Line = spacingLine,
                LineRule = spacingLineRule,
                Before = before,
                After = after.ToString()
            },
            new Indentation { FirstLine = firstLine.ToString() });
        var paragraph = new Paragraph(properties);
        AppendInlineRuns(paragraph, text, size, font);
        return paragraph;
    }

    private void AppendInlineRuns(Paragraph paragraph, string text, int size, string font)
    {
        // 先用正则匹配已知行内格式，再对未匹配的残留片段调用 CleanInlineFormatting 清理
        var pattern = @"(\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|~~(.+?)~~|\*(.+?)\*|`(.+?)`)";
        var position = 0;
        foreach (Match match in Regex.Matches(text, pattern))
        {
            if (match.Index > position) paragraph.Append(CreateRun(CleanInlineFormatting(text[position..match.Index]), size, font));
            var boldItalic = match.Groups[2].Success;
            var bold = boldItalic || match.Groups[3].Success;
            var strike = match.Groups[4].Success;
            var italic = boldItalic || match.Groups[5].Success;
            var code = match.Groups[6].Success;
            var value = boldItalic ? match.Groups[2].Value : match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Success ? match.Groups[4].Value : match.Groups[5].Success ? match.Groups[5].Value : match.Groups[6].Value;
            paragraph.Append(CreateRun(value, code ? CodeBlockFontSizeHp : size, code ? CodeBlockFont : font, bold, italic, strike));
            position = match.Index + match.Length;
        }
        if (position < text.Length) paragraph.Append(CreateRun(CleanInlineFormatting(text[position..]), size, font));
    }

    private static Run CreateRun(string text, int size, string font, bool bold = false, bool italic = false, bool strike = false)
    {
        var props = new RunProperties(
            new RunFonts { Ascii = font, HighAnsi = font, EastAsia = font, ComplexScript = font },
            new FontSize { Val = size.ToString() },
            new FontSizeComplexScript { Val = size.ToString() });
        if (bold) props.Append(new Bold());
        if (italic) props.Append(new Italic());
        if (strike) props.Append(new Strike());
        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static bool TryReadTable(string[] lines, ref int index, out List<List<string>> rows)
    {
        rows = new();
        if (!lines[index].TrimStart().StartsWith("|", StringComparison.Ordinal) || index + 1 >= lines.Length) return false;
        var separator = lines[index + 1].Trim();
        if (!Regex.IsMatch(separator, @"^\|?\s*:?-{3,}")) return false;
        rows.Add(SplitTableRow(lines[index]));
        index += 2;
        while (index < lines.Length && lines[index].TrimStart().StartsWith("|", StringComparison.Ordinal))
        {
            rows.Add(SplitTableRow(lines[index]));
            index++;
        }
        index--;
        return rows.Count > 0;
    }

    private static List<string> SplitTableRow(string row) => row.Trim().Trim('|').Split('|').Select(c => c.Trim().Replace("\\|", "|")).ToList();

    /// <summary>
    /// 过滤不应写入 DOCX 正文的技术标记：
    /// 1. YAML frontmatter（--- 到 ---）——仅当闭合标记存在且中间内容像 YAML 时才跳过，
    ///    避免误删以横线分隔符（---）开头的正文。
    /// 2. HTML 注释块（&lt;!-- ... --&gt;）——含 AI_AGENT_NOTICE、block_id、source marker 等
    /// 3. AI_AGENT_NOTICE 独立块（以防不是 HTML 注释格式）
    /// </summary>
    private static string StripDocxPollutants(string markdown)
    {
        var sb = new StringBuilder(markdown.Length);
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var i = 0;

        // 1. 跳过文档开头的 YAML frontmatter（--- 到闭合 ---）
        //    防误删：必须验证 (a) 在合理范围内找到闭合 ---，且 (b) 中间内容像 YAML（含 key: 行）。
        //    否则视为正文中的横线分隔符，不跳过。
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            var closeIdx = -1;
            var yamlLikeCount = 0;
            var maxScan = Math.Min(lines.Length, 50); // frontmatter 通常不超过 50 行
            for (var j = 1; j < maxScan; j++)
            {
                if (lines[j].Trim() == "---" || lines[j].Trim() == "...")
                {
                    closeIdx = j;
                    break;
                }
                // 统计看起来像 YAML key: value 的行
                var t = lines[j].Trim();
                if (!string.IsNullOrEmpty(t) && !t.StartsWith("#") && t.Contains(':'))
                    yamlLikeCount++;
            }
            // 只有同时满足"找到闭合标记"且"中间有至少 1 行像 YAML"才当作 frontmatter
            if (closeIdx > 0 && yamlLikeCount >= 1)
            {
                i = closeIdx + 1;
            }
            // 否则 i 保持 0，不跳过任何内容（当作普通横线）
        }

        // 2. 逐行处理，跳过 HTML 注释和 AI_AGENT_NOTICE
        while (i < lines.Length)
        {
            var line = lines[i];

            // 跳过 HTML 注释：单行 <!-- ... -->
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
            {
                // 如果注释在同行闭合
                if (trimmed.Contains("-->"))
                {
                    i++;
                    continue;
                }
                // 跨行 HTML 注释：跳到 -->
                i++;
                while (i < lines.Length)
                {
                    if (lines[i].Contains("-->")) { i++; break; }
                    i++;
                }
                continue;
            }

            // 跳过 AI_AGENT_NOTICE 标记行（以防万一不是 HTML 注释格式）
            if (trimmed.StartsWith("AI_AGENT_NOTICE", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            sb.AppendLine(lines[i]);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// 清理 Markdown 内联格式标记，只保留内容文本。
    /// **bold** → bold, *italic* → italic, `code` → code, ~~delete~~ → delete
    /// 仅处理未被 AppendInlineRuns 正则匹配到的残留标记。
    /// </summary>
    private static string CleanInlineFormatting(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // 移除粗斜体标记
        text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "$1");
        // 移除粗体标记
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        // 移除斜体标记
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        // 移除行内代码标记
        text = Regex.Replace(text, @"`(.+?)`", "$1");
        // 移除删除线标记
        text = Regex.Replace(text, @"~~(.+?)~~", "$1");
        return text;
    }

    private Table CreateTable(List<List<string>> rows)
    {
        var columns = rows.Max(r => r.Count);
        var table = new Table(new TableProperties(
            new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4U }, new BottomBorder { Val = BorderValues.Single, Size = 4U },
                new LeftBorder { Val = BorderValues.Single, Size = 4U }, new RightBorder { Val = BorderValues.Single, Size = 4U },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U })));
        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            for (var col = 0; col < columns; col++)
            {
                var cell = new TableCell(
                    new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
                    CreateParagraphWithSpacing(col < row.Count ? row[col] : "", 24, BodyFont, JustificationValues.Left, 0, 0, BeforeSpacing, true));
                tableRow.Append(cell);
            }
            table.Append(tableRow);
        }
        return table;
    }
}
