using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Doc2MD.Pipeline.Models;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// 纯执行层：从 DocxTemplate clone DOCX 模板，插入 SemanticDocument 内容，应用 StyleApplier，保存。
/// 禁止：判断内容类型、修改模板结构、决定样式。
/// 所有决策由 DocxTemplate + StyleApplier 提供，本类只负责执行。
/// </summary>
public class DocxRenderer
{
    /// <summary>模板样式版本号</summary>
    private const string TemplateVersion = "v2";

    /// <summary>
    /// 渲染 SemanticDocument 到 DOCX 文件。
    /// </summary>
    public void Render(SemanticDocument document, DocxTemplate template, string outputPath)
    {
        var opts = template.Options;

        // 1. 获取/创建 .dotx 模板
        var templatePath = GetOrCreateTemplate(template.Id, Path.GetDirectoryName(outputPath), opts);

        // 2. 基于模板创建 DOCX
        CreateDocxFromTemplate(templatePath, outputPath, document, opts);

        // 3. 生成格式检查报告
        var formatReport = CheckDocxFormat(outputPath, template.Id);
        var reportPath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? ".",
            Path.GetFileNameWithoutExtension(outputPath) + ".format_check_report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(formatReport, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }), Encoding.UTF8);
    }

    /// <summary>仅渲染 DOCX（不含格式检查报告），供需要自定义报告路径的场景调用</summary>
    public void RenderWithoutReport(SemanticDocument document, DocxTemplate template, string outputPath)
    {
        var opts = template.Options;
        var templatePath = GetOrCreateTemplate(template.Id, Path.GetDirectoryName(outputPath), opts);
        CreateDocxFromTemplate(templatePath, outputPath, document, opts);
    }

    // ========== 模板文件管理 ==========

    private static string GetOrCreateTemplate(string templateId, string? outputDir, DocxFormattingOptions opts)
    {
        var templateDir = Path.Combine(outputDir ?? ".", "templates");
        Directory.CreateDirectory(templateDir);
        var templatePath = Path.Combine(templateDir, $"{templateId}.{TemplateVersion}.dotx");

        if (!File.Exists(templatePath))
            CreateTemplateFile(templatePath, opts);

        return templatePath;
    }

    private static void CreateTemplateFile(string templatePath, DocxFormattingOptions opts)
    {
        using var doc = WordprocessingDocument.Create(templatePath, WordprocessingDocumentType.Template);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        // 页面设置：A4，按排版方案设置边距
        var sectPr = new SectionProperties();
        var pageSize = new PageSize { Width = 11906, Height = 16838 };
        var pageMargin = new PageMargin
        {
            Top = (Int32Value)Math.Round(opts.PageMarginTopCm * 567.0),
            Right = (UInt32Value)Math.Round(opts.PageMarginRightCm * 567.0),
            Bottom = (Int32Value)Math.Round(opts.PageMarginBottomCm * 567.0),
            Left = (UInt32Value)Math.Round(opts.PageMarginLeftCm * 567.0)
        };
        var usableHeight = 16838 - (int)Math.Round(opts.PageMarginTopCm * 567.0) - (int)Math.Round(opts.PageMarginBottomCm * 567.0);
        var docGrid = new DocGrid
        {
            Type = DocGridValues.LinesAndChars,
            LinePitch = usableHeight / opts.LinesPerPage
        };
        sectPr.Append(pageSize, pageMargin, docGrid);
        body.Append(sectPr);

        // 添加样式
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = CreateStyles(opts);
        stylesPart.Styles.Save();

        doc.Save();
    }

    private static Styles CreateStyles(DocxFormattingOptions opts)
    {
        var styles = new Styles();
        var lineSpacingTwips = ((int)Math.Round(opts.LineSpacingPt * 20.0)).ToString();
        var bodyIndentTwips = opts.CalcIndentTwips(opts.FirstLineIndentChars, opts.BodyFontSizePt).ToString();
        var bodyFontSizeHalfPt = ((int)Math.Round(opts.BodyFontSizePt * 2.0)).ToString();

        // 默认段落样式
        var defaultParaStyle = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        defaultParaStyle.Append(new Name { Val = "Normal" });
        var defaultRPR = new StyleRunProperties();
        defaultRPR.Append(CreateRunFontsElement(opts.BodyFont));
        defaultRPR.Append(new FontSize { Val = bodyFontSizeHalfPt });
        defaultRPR.Append(new FontSizeComplexScript { Val = bodyFontSizeHalfPt });
        if (opts.CharSpacingPt > 0)
            defaultRPR.Append(new Spacing { Val = (int)Math.Round(opts.CharSpacingPt * 2.0) });
        var defaultParaProps = new StyleParagraphProperties();
        defaultParaProps.Append(new SpacingBetweenLines { Line = lineSpacingTwips, LineRule = LineSpacingRuleValues.Exact });
        defaultParaProps.Append(new Indentation { FirstLine = bodyIndentTwips });
        defaultParaStyle.Append(defaultParaProps);
        defaultParaStyle.Append(defaultRPR);
        styles.Append(defaultParaStyle);

        // Heading1 样式 — 文件标题：居中，不缩进
        styles.Append(CreateHeadingStyle("Heading1", "heading 1",
            opts.TitleFont, opts.TitleFontSizePt, opts.TitleBold,
            JustificationValues.Center, 0, "0", "160", lineSpacingTwips));

        // Heading2 样式 — 一级标题
        var h2Indent = opts.CalcIndentTwips(opts.Heading1IndentChars, opts.Heading1FontSizePt).ToString();
        styles.Append(CreateHeadingStyle("Heading2", "heading 2",
            opts.Heading1Font, opts.Heading1FontSizePt, opts.Heading1Bold,
            JustificationValues.Left, opts.Heading1IndentChars, "160", "80", lineSpacingTwips,
            h2Indent));

        // Heading3 样式 — 二级标题
        var h3Indent = opts.CalcIndentTwips(opts.Heading2IndentChars, opts.Heading2FontSizePt).ToString();
        styles.Append(CreateHeadingStyle("Heading3", "heading 3",
            opts.Heading2Font, opts.Heading2FontSizePt, opts.Heading2Bold,
            JustificationValues.Left, opts.Heading2IndentChars, "120", "60", lineSpacingTwips,
            h3Indent));

        // Heading4 样式 — 三级标题
        var h4Indent = opts.CalcIndentTwips(opts.Heading3IndentChars, opts.Heading3FontSizePt).ToString();
        styles.Append(CreateHeadingStyle("Heading4", "heading 4",
            opts.Heading3Font, opts.Heading3FontSizePt, opts.Heading3Bold,
            JustificationValues.Left, opts.Heading3IndentChars, "80", "40", lineSpacingTwips,
            h4Indent));

        return styles;
    }

    private static Style CreateHeadingStyle(
        string styleId, string name,
        string font, double fontSizePt, bool bold,
        JustificationValues alignment, double indentChars,
        string beforeSpacing, string afterSpacing, string lineSpacing,
        string? firstLineIndent = null)
    {
        var style = new Style { Type = StyleValues.Paragraph, StyleId = styleId };
        style.Append(new Name { Val = name });
        style.Append(new BasedOn { Val = "Normal" });
        style.Append(new NextParagraphStyle { Val = "Normal" });

        var paraProps = new StyleParagraphProperties();
        paraProps.Append(new Justification { Val = alignment });
        paraProps.Append(new SpacingBetweenLines { Before = beforeSpacing, After = afterSpacing, Line = lineSpacing, LineRule = LineSpacingRuleValues.Exact });

        if (alignment == JustificationValues.Center)
        {
            paraProps.Append(new Indentation { FirstLine = "0" });
        }
        else if (firstLineIndent != null)
        {
            paraProps.Append(new Indentation { FirstLine = firstLineIndent });
        }

        paraProps.Append(new KeepNext());
        if (styleId == "Heading1" || styleId == "Heading2")
            paraProps.Append(new KeepLines());

        var rPr = new StyleRunProperties();
        rPr.Append(CreateRunFontsElement(font));
        var halfPt = ((int)Math.Round(fontSizePt * 2.0)).ToString();
        rPr.Append(new FontSize { Val = halfPt });
        rPr.Append(new FontSizeComplexScript { Val = halfPt });
        if (bold)
        {
            rPr.Append(new Bold());
            rPr.Append(new BoldComplexScript());
        }

        style.Append(paraProps);
        style.Append(rPr);
        return style;
    }

    // ========== DOCX 文档创建 ==========

    private static void CreateDocxFromTemplate(string templatePath, string outputPath, SemanticDocument document, DocxFormattingOptions opts)
    {
        File.Copy(templatePath, outputPath, overwrite: true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        doc.ChangeDocumentType(WordprocessingDocumentType.Document);

        var mainPart = doc.MainDocumentPart ?? throw new InvalidOperationException("模板文档缺少 MainDocumentPart");
        var body = mainPart.Document?.Body ?? throw new InvalidOperationException("模板文档缺少 Body");

        body.RemoveAllChildren<Paragraph>();

        var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
        sectPr?.Remove();

        EnsureNumberingDefinitions(mainPart);

        // 渲染语义块
        foreach (var block in document.Blocks)
        {
            switch (block)
            {
                case HeadingBlock h:
                    AddHeading(body, h, opts);
                    break;
                case ParagraphBlock p:
                    AddParagraph(body, p, opts);
                    break;
                case TableBlock t:
                    AddTable(body, t, opts);
                    break;
                case ListBlock l:
                    AddList(body, l, opts);
                    break;
                case QuoteBlock q:
                    AddBlockquote(body, q, opts);
                    break;
                case HorizontalRuleBlock:
                    AddHorizontalRule(body);
                    break;
            }
        }

        sectPr ??= new SectionProperties();
        if (sectPr.Elements<PageMargin>().FirstOrDefault() == null)
        {
            sectPr.Append(new PageMargin
            {
                Top = (Int32Value)Math.Round(opts.PageMarginTopCm * 567.0),
                Right = (UInt32Value)Math.Round(opts.PageMarginRightCm * 567.0),
                Bottom = (Int32Value)Math.Round(opts.PageMarginBottomCm * 567.0),
                Left = (UInt32Value)Math.Round(opts.PageMarginLeftCm * 567.0)
            });
        }
        body.Append(sectPr);

        doc.Save();
    }

    // ========== 渲染各语义块（纯执行，样式由 StyleApplier 提供） ==========

    private static void AddHeading(Body body, HeadingBlock block, DocxFormattingOptions opts)
    {
        var styleId = StyleApplier.GetHeadingStyleId(block.Level);
        var (font, fontSizePt, bold) = StyleApplier.GetHeadingFormat(block.Level, opts);
        var alignment = StyleApplier.GetHeadingAlignment(block.Level);

        var para = new Paragraph();
        var paraProps = new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = styleId } };

        if (alignment == "center")
        {
            paraProps.Append(new Justification { Val = JustificationValues.Center });
            paraProps.Append(new Indentation { FirstLine = "0" });
        }
        else
        {
            paraProps.Append(new Justification { Val = JustificationValues.Left });
            var (indentChars, indFontSizePt) = StyleApplier.GetHeadingIndent(block.Level, opts);
            var indentTwips = opts.CalcIndentTwips(indentChars, indFontSizePt);
            paraProps.Append(new Indentation { FirstLine = indentTwips.ToString() });
        }

        para.Append(paraProps);

        // 应用标题字体/字号/加粗（确保内联属性覆盖样式继承）
        foreach (var run in block.Runs)
        {
            var docRun = CreateRun(run);
            ApplyHeadingRunFormat(docRun, font, fontSizePt, bold, opts);
            para.Append(docRun);
        }

        body.Append(para);
    }

    private static void AddParagraph(Body body, ParagraphBlock block, DocxFormattingOptions opts)
    {
        if (string.IsNullOrWhiteSpace(block.Content)) return;

        var para = new Paragraph();
        var indentTwips = opts.CalcIndentTwips(opts.FirstLineIndentChars, opts.BodyFontSizePt);
        var lineTwips = (int)Math.Round(opts.LineSpacingPt * 20.0);
        para.Append(new ParagraphProperties
        {
            Indentation = new Indentation { FirstLine = indentTwips.ToString() },
            SpacingBetweenLines = new SpacingBetweenLines { Line = lineTwips.ToString(), LineRule = LineSpacingRuleValues.Exact }
        });

        foreach (var run in block.Runs)
        {
            var docRun = CreateRun(run);
            if (!run.Code) // 行内代码保留等宽字体
                ApplyBodyRunFormat(docRun, opts);
            para.Append(docRun);
        }

        body.Append(para);
    }

    private static void AddTable(Body body, TableBlock block, DocxFormattingOptions opts)
    {
        if (block.Rows.Count == 0) return;

        var table = new Table();
        var tblProps = new TableProperties();
        var tblBorders = new TableBorders
        {
            TopBorder = new TopBorder { Val = BorderValues.Single, Size = 4 },
            BottomBorder = new BottomBorder { Val = BorderValues.Single, Size = 4 },
            LeftBorder = new LeftBorder { Val = BorderValues.Single, Size = 4 },
            RightBorder = new RightBorder { Val = BorderValues.Single, Size = 4 },
            InsideHorizontalBorder = new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            InsideVerticalBorder = new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
        };
        tblProps.Append(tblBorders);
        tblProps.Append(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });
        var cellMargin = new TableCellMargin
        {
            TopMargin = new TopMargin { Width = "50", Type = TableWidthUnitValues.Dxa },
            BottomMargin = new BottomMargin { Width = "50", Type = TableWidthUnitValues.Dxa },
            LeftMargin = new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
            RightMargin = new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }
        };
        tblProps.Append(cellMargin);
        table.Append(tblProps);

        for (int rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
        {
            var row = block.Rows[rowIndex];
            var isHeaderRow = rowIndex == 0;
            var tr = new TableRow();

            foreach (var cell in row)
            {
                var tc = new TableCell();
                var tcPara = new Paragraph();
                tcPara.Append(new ParagraphProperties
                {
                    Justification = new Justification { Val = JustificationValues.Center },
                    SpacingBetweenLines = new SpacingBetweenLines { Line = "400", LineRule = LineSpacingRuleValues.Exact }
                });

                if (isHeaderRow)
                {
                    foreach (var inlineRun in cell.Runs)
                    {
                        inlineRun.Bold = true;
                        var docRun = CreateRun(inlineRun);
                        var rPr = docRun.RunProperties ?? new RunProperties();
                        rPr.RunFonts = CreateRunFontsElement(opts.Heading1Font);
                        rPr.FontSize = new FontSize { Val = "21" };
                        rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = "21" };
                        rPr.Bold = new Bold();
                        rPr.BoldComplexScript = new BoldComplexScript();
                        docRun.RunProperties = rPr;
                        tcPara.Append(docRun);
                    }
                }
                else
                {
                    foreach (var inlineRun in cell.Runs)
                    {
                        var docRun = CreateRun(inlineRun);
                        if (!inlineRun.Code)
                        {
                            var rPr = docRun.RunProperties ?? new RunProperties();
                            rPr.RunFonts = CreateRunFontsElement(opts.BodyFont);
                            rPr.FontSize = new FontSize { Val = "21" };
                            rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = "21" };
                            docRun.RunProperties = rPr;
                        }
                        tcPara.Append(docRun);
                    }
                }

                tc.Append(tcPara);
                tr.Append(tc);
            }
            table.Append(tr);
        }

        body.Append(table);
        body.Append(new Paragraph());
    }

    private static void AddList(Body body, ListBlock block, DocxFormattingOptions opts)
    {
        var lineTwips = (int)Math.Round(opts.LineSpacingPt * 20.0);

        foreach (var item in block.Items)
        {
            var para = new Paragraph();
            var paraProps = new ParagraphProperties
            {
                Indentation = new Indentation { Left = "420", FirstLine = "0" },
                SpacingBetweenLines = new SpacingBetweenLines { Line = lineTwips.ToString(), LineRule = LineSpacingRuleValues.Exact }
            };

            if (block.IsOrdered)
            {
                paraProps.NumberingProperties = new NumberingProperties
                {
                    NumberingId = new NumberingId { Val = 1 },
                    NumberingLevelReference = new NumberingLevelReference { Val = 0 }
                };
            }

            para.Append(paraProps);

            if (!block.IsOrdered)
                para.Append(new Run(new Text("● ") { Space = SpaceProcessingModeValues.Preserve }));

            foreach (var run in item.Runs)
                para.Append(CreateRun(run));

            body.Append(para);
        }
    }

    private static void AddBlockquote(Body body, QuoteBlock block, DocxFormattingOptions opts)
    {
        var para = new Paragraph();
        var lineTwips = (int)Math.Round(opts.LineSpacingPt * 20.0);
        para.Append(new ParagraphProperties
        {
            Indentation = new Indentation { Left = "420", FirstLine = "0" },
            SpacingBetweenLines = new SpacingBetweenLines { Line = lineTwips.ToString(), LineRule = LineSpacingRuleValues.Exact }
        });

        foreach (var run in block.Runs)
        {
            var docRun = CreateRun(run);
            var rPr = docRun.RunProperties ?? new RunProperties();
            rPr.RunFonts = new RunFonts { Ascii = "KaiTi", HighAnsi = "KaiTi", EastAsia = "楷体" };
            rPr.Italic = new Italic();
            rPr.FontSize = new FontSize { Val = "28" };
            docRun.RunProperties = rPr;
            para.Append(docRun);
        }

        body.Append(para);
    }

    private static void AddHorizontalRule(Body body)
    {
        var para = new Paragraph();
        var paraProps = new ParagraphProperties();
        paraProps.Append(new ParagraphBorders
        {
            BottomBorder = new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1 }
        });
        para.Append(paraProps);
        body.Append(para);
    }

    // ========== Run 创建与格式应用 ==========

    private static Run CreateRun(InlineRun inlineRun)
    {
        var run = new Run();
        var text = new Text(inlineRun.Text) { Space = SpaceProcessingModeValues.Preserve };

        if (inlineRun.Bold || inlineRun.Italic || inlineRun.Strikethrough || inlineRun.Code)
        {
            var rPr = new RunProperties();
            if (inlineRun.Bold) { rPr.Append(new Bold()); rPr.Append(new BoldComplexScript()); }
            if (inlineRun.Italic) { rPr.Append(new Italic()); rPr.Append(new ItalicComplexScript()); }
            if (inlineRun.Strikethrough) { rPr.Append(new Strike()); }
            if (inlineRun.Code)
            {
                rPr.Append(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", EastAsia = "微软雅黑", ComplexScript = "Consolas" });
                rPr.Append(new FontSize { Val = "28" });
                rPr.Append(new Shading { Fill = "F0F0F0" });
            }
            run.Append(rPr);
        }

        run.Append(text);
        return run;
    }

    private static void ApplyHeadingRunFormat(Run docRun, string font, double fontSizePt, bool bold, DocxFormattingOptions opts)
    {
        var rPr = docRun.RunProperties;
        if (rPr == null) { rPr = new RunProperties(); docRun.InsertAt(rPr, 0); }
        rPr.RunFonts = CreateRunFontsElement(font);
        var halfPt = ((int)Math.Round(fontSizePt * 2.0)).ToString();
        rPr.FontSize = new FontSize { Val = halfPt };
        rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = halfPt };
        if (bold) { rPr.Bold = new Bold(); rPr.BoldComplexScript = new BoldComplexScript(); }
        if (opts.CharSpacingPt > 0)
            rPr.Spacing = new Spacing { Val = (int)Math.Round(opts.CharSpacingPt * 2.0) };
    }

    private static void ApplyBodyRunFormat(Run docRun, DocxFormattingOptions opts)
    {
        var rPr = docRun.RunProperties;
        if (rPr == null) { rPr = new RunProperties(); docRun.InsertAt(rPr, 0); }
        rPr.RunFonts = CreateRunFontsElement(opts.BodyFont);
        var halfPt = ((int)Math.Round(opts.BodyFontSizePt * 2.0)).ToString();
        rPr.FontSize = new FontSize { Val = halfPt };
        rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = halfPt };
        if (opts.CharSpacingPt > 0)
            rPr.Spacing = new Spacing { Val = (int)Math.Round(opts.CharSpacingPt * 2.0) };
    }

    private static RunFonts CreateRunFontsElement(string fontName) => new()
    {
        Ascii = fontName,
        HighAnsi = fontName,
        EastAsia = fontName,
        ComplexScript = fontName
    };

    // ========== 编号定义 ==========

    private static void EnsureNumberingDefinitions(MainDocumentPart mainPart)
    {
        var numberingPart = mainPart.NumberingDefinitionsPart;
        if (numberingPart != null) return;

        numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering();
        var abstractNum = new AbstractNum { AbstractNumberId = 0 };
        var lvl = new Level { LevelIndex = 0 };
        lvl.Append(new StartNumberingValue { Val = 1 });
        lvl.Append(new NumberingFormat { Val = NumberFormatValues.Decimal });
        lvl.Append(new LevelText { Val = "%1." });
        lvl.Append(new LevelJustification { Val = LevelJustificationValues.Left });
        var lvlRunProps = new PreviousRunProperties();
        lvlRunProps.Append(new RunFonts { Ascii = "SimHei", HighAnsi = "SimHei", EastAsia = "黑体" });
        lvlRunProps.Append(new FontSize { Val = "32" });
        lvl.Append(lvlRunProps);
        var lvlParaProps = new PreviousParagraphProperties();
        lvlParaProps.Append(new Indentation { Left = "420", Hanging = "420" });
        lvl.Append(lvlParaProps);
        abstractNum.Append(lvl);
        numbering.Append(abstractNum);
        var num = new NumberingInstance { NumberID = 1 };
        num.Append(new AbstractNumId { Val = 0 });
        numbering.Append(num);
        numberingPart.Numbering = numbering;
        numberingPart.Numbering.Save();
    }

    // ========== 格式检查 ==========

    /// <summary>
    /// 基于模板排版选项检查 DOCX 格式合规性。检查项覆盖 GB/T 9704-2012 核心要求。
    /// </summary>
    private static FormatCheckReport CheckDocxFormat(string docxPath, string templateId)
    {
        // 获取模板对应的排版选项
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

            // ---- 5. 行距检查 ----
            var nonStandardLineSpacing = 0;
            foreach (var para in bodyParagraphs.Take(20))
            {
                var spacing = para.ParagraphProperties?.SpacingBetweenLines;
                if (spacing?.Line?.Value != null)
                {
                    var lineVal = int.Parse(spacing.Line.Value);
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

            // ---- 7. 表格宽度检查 ----
            foreach (var table in body.Elements<Table>())
            {
                var maxCols = table.Elements<TableRow>().Max(r => r.Elements<TableCell>().Count());
                if (maxCols > 8)
                    issues.Add(new FormatCheckIssue { Code = "F_TABLE_TOO_WIDE", Severity = "medium", Message = $"表格列数过多（{maxCols} 列），可能超出页面宽度" });
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
}
