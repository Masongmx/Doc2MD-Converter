using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Parsers;

public class WordParser : IDocumentParser
{
    /// <summary>缓存的 MainDocumentPart，供方法链中访问超链接关系</summary>
    private MainDocumentPart? _mainPart;

    /// <summary>有序列表编号计数器：key=(numId, ilvl)，value=当前序号</summary>
    private readonly Dictionary<(int numId, int ilvl), int> _orderedListCounters = new();

    /// <summary>编号格式缓存：key=numId，value=(ilvl → isOrdered)</summary>
    private readonly Dictionary<int, Dictionary<int, bool>> _numberingFormatCache = new();
    public FileType SupportedType => FileType.Word;
    public ConversionTarget Target => ConversionTarget.Markdown;

    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".docx" || ext == ".doc";
    }

    public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            result.SourceFilePath = filePath;
            result.SourceType = "Word";

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".doc")
            {
                // 路径1：LibreOffice 优先（最高保真度，保留完整格式和表格）
                var legacy = LegacyOfficeConverter.Convert(filePath, ".docx", cancellationToken);
                if (legacy.IsSuccess)
                {
                    try { return Parse(legacy.ConvertedPath!, outputDirectory, cancellationToken); }
                    finally { LegacyOfficeConverter.Cleanup(legacy); }
                }

                // 路径2：Word COM 自动化兜底（需要 Microsoft Word）
                LoggingService.Info($"[WordParser] LibreOffice 不可用，切换 Word COM 兜底: {filePath}");
                var comResult = OfficeComFallbackService.ConvertDocToDocx(filePath, cancellationToken);
                if (comResult.IsSuccess)
                {
                    try
                    {
                        var parsedResult = Parse(comResult.ConvertedPath!, outputDirectory, cancellationToken);
                        if (parsedResult.Success)
                        {
                            parsedResult.Warnings.Add(ConversionWarning.Create(
                                "W_LEGACY_FALLBACK",
                                ".doc 文件通过 Word COM 自动化转换（LibreOffice 不可用），格式保真度可能略有差异",
                                "全文"));
                        }
                        return parsedResult;
                    }
                    finally { OfficeComFallbackService.Cleanup(comResult); }
                }

                // 两条路径都失败
                result.Success = false;
                result.ErrorMessage = $"LibreOffice 转换失败: {legacy.ErrorMessage}；Word COM 兜底也失败: {comResult.ErrorMessage}";
                return result;
            }

            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            
            if (body == null)
            {
                result.Success = false;
                result.ErrorMessage = "文档内容为空";
                return result;
            }

            var mainPart = doc.MainDocumentPart!;
            _mainPart = mainPart;

            // 预加载编号格式信息（用于有序列表检测）
            LoadNumberingFormats(mainPart.NumberingDefinitionsPart);

            // 检测超链接——v2.0 起保留 URL，不再丢失
            // URL 保留在 BuildFormattedText 中处理，此处不发警告

            // 提取图片到 ImageExports（实际字节提取）
            bool hasImageParts = mainPart.ImageParts.Any();
            if (hasImageParts)
            {
                int imgIndex = 0;
                foreach (var imagePart in mainPart.ImageParts)
                {
                    imgIndex++;
                    try
                    {
                        using var stream = imagePart.GetStream();
                        var data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);

                        var mimeType = imagePart.ContentType ?? "image/png";
                        var imgExt = mimeType switch
                        {
                            "image/jpeg" => ".jpg",
                            "image/gif" => ".gif",
                            "image/bmp" => ".bmp",
                            "image/tiff" => ".tiff",
                            "image/svg+xml" => ".svg",
                            _ => ".png"
                        };
                        var imgFileName = $"image_{imgIndex:D3}{imgExt}";

                        result.ImageExports.Add(new ImageExport
                        {
                            FileName = imgFileName,
                            Data = data,
                            MimeType = mimeType,
                            AltText = $"图片 {imgIndex}",
                            Location = null
                        });
                    }
                    catch
                    {
                        // 单个图片提取失败不阻断整体流程
                    }
                }
            }

            // 检测脚注/尾注
            if (mainPart.FootnotesPart != null || mainPart.EndnotesPart != null)
            {
                result.Warnings.Add(ConversionWarning.Create(
                    "W_FOOTNOTE_LOST", "Word 文档包含脚注/尾注，暂不支持提取"));
            }

            // 检测批注
            if (mainPart.WordprocessingCommentsPart != null)
            {
                result.Warnings.Add(ConversionWarning.Create(
                    "W_COMMENT_LOST", "Word 文档包含批注，暂不支持提取"));
            }

            // 检测修订标记
            var revisions = body.Descendants<OpenXmlElement>()
                .Where(e => e is ParagraphMarkRunPropertiesChange
                         || e is RunPropertiesChange
                         || e is ParagraphPropertiesChange
                         || e is SectionPropertiesChange
                         || e is TablePropertiesChange
                         || e is TableRowPropertiesChange
                         || e is TableCellPropertiesChange)
                .ToList();
            if (revisions.Count > 0)
            {
                result.Warnings.Add(ConversionWarning.Create(
                    "W_REVISION_LOST",
                    $"Word 文档包含 {revisions.Count} 处修订标记，暂不支持提取修订信息"));
            }

            // 检测嵌入对象（OLE / 包）
            if (mainPart.Parts.Any(p => p.OpenXmlPart is EmbeddedObjectPart || p.OpenXmlPart is EmbeddedPackagePart))
            {
                result.Warnings.Add(ConversionWarning.Create(
                    "W_EMBEDDED_OBJECT_LOST",
                    "Word 文档包含嵌入对象（OLE/包），暂不支持提取"));
            }

            // 检测有序列表——v2.0 起支持有序列表编号提取
            // 编号提取在 GetListPrefix 中处理，此处不发 W_ORDERED_LIST_FLAT 警告

            var sb = new StringBuilder();
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            sb.AppendLine($"# {fileName}");
            sb.AppendLine();

            foreach (var element in body.Elements())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parsed = ParseElement(element);
                if (!string.IsNullOrEmpty(parsed))
                {
                    sb.AppendLine(parsed);
                }
            }

            // 追加图片引用
            if (result.ImageExports.Count > 0)
            {
                sb.AppendLine();
                foreach (var img in result.ImageExports)
                {
                    sb.AppendLine($"![{img.AltText}](assets/{img.FileName})");
                }
            }
            else if (hasImageParts)
            {
                // 图片存在但提取全部失败
                result.Warnings.Add(ConversionWarning.Create(
                    "W_IMG_LOST", "Word 文档包含嵌入式图片，但提取失败"));
                sb.AppendLine();
                sb.AppendLine("<!-- IMAGE_PLACEHOLDER: Word 文档包含嵌入式图片，提取失败 -->");
            }

            result.SourceFileName = Path.GetFileName(filePath);
            result.RawMarkdown = sb.ToString();
            result.Success = true;
            result.OutputPath = Path.Combine(outputDirectory,
                Path.GetFileNameWithoutExtension(filePath) + ".md");
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

    private string ParseElement(OpenXmlElement element)
    {
        if (element is Paragraph para)
        {
            return ParseParagraph(para);
        }
        if (element is Table table)
        {
            return ParseTable(table);
        }
        return string.Empty;
    }

    private string ParseParagraph(Paragraph para)
    {
        // 重置有序列表计数器（当段落不属于列表时）
        var numberingProps = para.ParagraphProperties?.NumberingProperties;
        var numId = numberingProps?.NumberingId?.Val?.Value;
        var ilvl = numberingProps?.NumberingLevelReference?.Val?.Value ?? 0;

        // 如果段落没有编号属性，重置所有计数器（退出列表）
        if (numId == null || numId == 0)
        {
            _orderedListCounters.Clear();
        }

        // 先获取纯文本用于标题/列表检测
        var plainText = para.InnerText.Trim();
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        
        // 标题检测
        if (style != null)
        {
            if (style.StartsWith("Heading") || style.StartsWith("标题"))
            {
                var level = ExtractHeadingLevel(style);
                return new string('#', level) + " " + plainText;
            }
            if (style.Contains("List") || style.Contains("列表"))
            {
                var prefix = GetListPrefix(para);
                return $"{prefix}{BuildFormattedText(para)}";
            }
        }

        // 编号属性检测
        if (para.ParagraphProperties?.NumberingProperties != null)
        {
            var prefix = GetListPrefix(para);
            return $"{prefix}{BuildFormattedText(para)}";
        }

        // 普通正文，保留行内格式
        return BuildFormattedText(para);
    }

    /// <summary>
    /// 构建带行内格式（加粗/斜体）的文本。
    /// 遍历 paragraph.ChildElements 以覆盖 Hyperlink 内的 Run。
    /// v2.0: 超链接保留 URL，输出 [text](url) 格式。
    /// </summary>
    private string BuildFormattedText(Paragraph para)
    {
        var sb = new StringBuilder();

        foreach (var child in para.ChildElements)
        {
            if (child is Run run)
            {
                AppendFormattedRun(sb, run);
            }
            else if (child is Hyperlink hyperlink)
            {
                // 提取 Hyperlink 内部所有 Run 的文本
                var textBuilder = new StringBuilder();
                foreach (var hlRun in hyperlink.Elements<Run>())
                {
                    var runText = hlRun.InnerText;
                    if (!string.IsNullOrEmpty(runText))
                        textBuilder.Append(runText);
                }
                var linkText = textBuilder.ToString();

                // 解析 URL
                var url = ResolveHyperlinkUrl(hyperlink);

                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(linkText))
                {
                    // 转义 URL 中的括号
                    url = url.Replace(")", "%29").Replace("(", "%28");
                    sb.Append($"[{linkText}]({url})");
                }
                else
                {
                    // URL 解析失败，退化为纯文本
                    foreach (var hlRun in hyperlink.Elements<Run>())
                    {
                        AppendFormattedRun(sb, hlRun);
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析 Hyperlink 元素的 URL（外部链接或内部书签锚点）
    /// </summary>
    private string? ResolveHyperlinkUrl(Hyperlink hyperlink)
    {
        if (_mainPart == null) return null;

        // 外部链接：通过 r:id 查找 HyperlinkRelationship
        var rid = hyperlink.Id?.Value;
        if (!string.IsNullOrEmpty(rid))
        {
            var rel = _mainPart.HyperlinkRelationships
                .FirstOrDefault(r => r.Id == rid);
            return rel?.Uri?.AbsoluteUri;
        }

        // 内部链接：通过 anchor 属性
        if (hyperlink.Anchor?.HasValue == true && !string.IsNullOrEmpty(hyperlink.Anchor.Value))
        {
            return $"#{hyperlink.Anchor.Value}";
        }

        return null;
    }

    /// <summary>
    /// 将单个 Run 的格式化文本追加到 StringBuilder
    /// </summary>
    private static void AppendFormattedRun(StringBuilder sb, Run run)
    {
        // 跳过脚注引用标记
        if (run.GetFirstChild<FootnoteReference>() != null || run.GetFirstChild<EndnoteReference>() != null)
            return;

        // 跳过批注引用标记
        if (run.GetFirstChild<CommentReference>() != null)
            return;

        var runText = run.InnerText;
        if (string.IsNullOrEmpty(runText)) return;

        var props = run.RunProperties;
        bool isBold = props?.Bold != null && props.Bold.Val?.Value != false;
        bool isBoldCs = props?.BoldComplexScript != null && props.BoldComplexScript.Val?.Value != false;
        bool isItalic = props?.Italic != null && props.Italic.Val?.Value != false;
        bool isItalicCs = props?.ItalicComplexScript != null && props.ItalicComplexScript.Val?.Value != false;

        isBold = isBold || isBoldCs;
        isItalic = isItalic || isItalicCs;

        bool isStrike = props?.Strike != null && props.Strike.Val?.Value != false;

        if (isBold && isItalic)
            sb.Append($"***{runText}***");
        else if (isBold)
            sb.Append($"**{runText}**");
        else if (isItalic)
            sb.Append($"*{runText}*");
        else if (isStrike)
            sb.Append($"~~{runText}~~");
        else
            sb.Append(runText);
    }

    /// <summary>
    /// 获取列表前缀（支持有序和无序列表）
    /// v2.0: 通过 NumberingDefinitionsPart 判断编号格式，有序列表输出序号
    /// </summary>
    private string GetListPrefix(Paragraph para)
    {
        var numProps = para.ParagraphProperties?.NumberingProperties;
        if (numProps == null) return "- ";

        var numId = numProps.NumberingId?.Val?.Value;
        if (numId == null || numId == 0) return "- ";

        var ilvl = numProps.NumberingLevelReference?.Val?.Value ?? 0;

        // 查找该 numId + ilvl 是否为有序列表
        bool isOrdered = false;
        if (_numberingFormatCache.TryGetValue(numId.Value, out var levelMap))
        {
            levelMap.TryGetValue(ilvl, out isOrdered);
        }

        if (!isOrdered)
            return "- ";

        // 有序列表：递增计数器
        var key = (numId.Value, ilvl);
        if (!_orderedListCounters.ContainsKey(key))
            _orderedListCounters[key] = 1;

        var currentNum = _orderedListCounters[key];
        _orderedListCounters[key]++;

        // 重置下级编号（嵌套列表场景：重新进入上级时下级重置）
        foreach (var k in _orderedListCounters.Keys.ToList())
        {
            if (k.numId == numId.Value && k.ilvl > ilvl)
                _orderedListCounters[k] = 1;
        }

        return $"{currentNum}. ";
    }

    /// <summary>
    /// 预加载编号格式信息：判断每个 numbering instance 的每一级是否为有序编号
    /// </summary>
    private void LoadNumberingFormats(NumberingDefinitionsPart? numberingPart)
    {
        if (numberingPart == null) return;

        var numbering = numberingPart.Numbering;
        if (numbering == null) return;

        // 构建 abstractNumId → (ilvl → numFmt) 映射
        var abstractFormats = new Dictionary<int, Dictionary<int, string>>();
        foreach (var absNum in numbering.Elements<AbstractNum>())
        {
            var absId = absNum.AbstractNumberId?.Value ?? 0;
            var levels = new Dictionary<int, string>();
            foreach (var level in absNum.Elements<Level>())
            {
                var ilvl = level.LevelIndex?.Value ?? 0;
                var numFmt = level.NumberingFormat?.Val?.Value ?? NumberFormatValues.Bullet;
                levels[ilvl] = numFmt.ToString()!;
            }
            abstractFormats[absId] = levels;
        }

        // 构建 numId → (ilvl → isOrdered) 映射
        foreach (var numInstance in numbering.Elements<NumberingInstance>())
        {
            var numId = numInstance.NumberID?.Value ?? 0;
            var absNumId = numInstance.AbstractNumId?.Val?.Value ?? 0;

            if (!abstractFormats.TryGetValue(absNumId, out var levels)) continue;

            var levelMap = new Dictionary<int, bool>();
            foreach (var (ilvl, fmtStr) in levels)
            {
                // 有序格式：decimal, decimalEnclosed, decimalZero, upperRoman, lowerRoman,
                //           upperLetter, lowerLetter, chicago, ordinal, cardinal, etc.
                bool isOrdered = IsOrderedNumberFormat(fmtStr);
                levelMap[ilvl] = isOrdered;
            }
            _numberingFormatCache[numId] = levelMap;
        }
    }

    /// <summary>
    /// 判断编号格式是否为有序（非 bullet/none）
    /// </summary>
    private static bool IsOrderedNumberFormat(string format)
    {
        if (string.IsNullOrEmpty(format)) return false;
        var lower = format.ToLowerInvariant();
        return lower != "bullet" && lower != "none";
    }

    private int ExtractHeadingLevel(string style)
    {
        var match = Regex.Match(style, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var level) && level is >= 1 and <= 6)
            return level;
        return 1;
    }

    private string ParseTable(Table table)
    {
        var sb = new StringBuilder();
        var rows = table.Elements<TableRow>().ToList();
        
        if (rows.Count == 0) return string.Empty;

        var cellCount = rows[0].Elements<TableCell>().Count();
        
        for (int i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Elements<TableCell>()
                .Select(c => FormatCellText(c).Trim())
                .ToList();
            
            while (cells.Count < cellCount)
                cells.Add("");
            
            sb.Append("| " + string.Join(" | ", cells) + " |");
            
            if (i == 0)
            {
                sb.AppendLine();
                sb.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", cellCount)) + " |");
            }
            else
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化单元格文本，保留行内格式
    /// </summary>
    private string FormatCellText(TableCell cell)
    {
        var sb = new StringBuilder();
        foreach (var para in cell.Elements<Paragraph>())
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(BuildFormattedText(para));
        }
        // 转义管道符
        return sb.ToString().Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }
}
