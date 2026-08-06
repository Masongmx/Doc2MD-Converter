using System.Diagnostics;
using System.IO;
using System.Text;
using Doc2MD.Models;
using Doc2MD.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Doc2MD.Parsers;

/// <summary>
/// PDF 文档解析器
/// </summary>
public class PdfParser : IDocumentParser
{
    private readonly PdfLineClassifier _lineClassifier;
    private readonly PdfTableDetector _tableDetector;
    private readonly PdfTextMerger _textMerger;

    public FileType SupportedType => FileType.PDF;
    public ConversionTarget Target => ConversionTarget.Markdown;

    /// <summary>
    /// 是否启用 OCR（当 PDF 为扫描件时），默认 true。
    /// 由调用方在 Parse 前设置，null 则默认启用。
    /// </summary>
    public bool? EnableOcr { get; set; }

    public PdfParser()
    {
        _lineClassifier = new PdfLineClassifier();
        _tableDetector = new PdfTableDetector();
        _textMerger = new PdfTextMerger(_lineClassifier);
    }

    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".pdf";
    }

    public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            result.SourceFilePath = filePath;
            result.SourceType = "PDF";

            var sb = new StringBuilder();
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            sb.AppendLine($"# {fileName}");
            sb.AppendLine();

            using var document = PdfDocument.Open(filePath);

            // 收集所有页的行信息
            var allLineInfos = new List<PdfLineClassifier.LineInfo>();
            var pageBreakIndices = new HashSet<int>();
            var totalWordCount = 0;
            int pageCount = 0;
            bool hasImages = false;
            int imgIndex = 0;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                pageCount++;
                pageBreakIndices.Add(allLineInfos.Count);

                // 输出 PAGE_START 标记
                allLineInfos.Add(new PdfLineClassifier.LineInfo
                {
                    Type = PdfLineClassifier.LineType.Empty,
                    IsPageBreak = true
                });

                // 提取页内图片（实际字节提取）
                try
                {
                    var imageObjects = page.GetImages().ToList();
                    if (imageObjects.Count > 0)
                    {
                        hasImages = true;

                        foreach (var img in imageObjects)
                        {
                            try
                            {
                                imgIndex++;
                                var rawBytes = img.RawBytes;
                                byte[]? imageData = null;
                                if (rawBytes != null && rawBytes.Length > 0)
                                {
                                    imageData = rawBytes.ToArray();
                                }

                                if (imageData == null || imageData.Length == 0) continue;

                                var mimeType = DetermineImageMimeType(imageData);
                                var imgExt = mimeType switch
                                {
                                    "image/jpeg" => ".jpg",
                                    "image/gif" => ".gif",
                                    "image/bmp" => ".bmp",
                                    "image/tiff" => ".tiff",
                                    _ => ".png"
                                };
                                var imgFileName = $"image_{imgIndex:D3}{imgExt}";

                                result.ImageExports.Add(new ImageExport
                                {
                                    FileName = imgFileName,
                                    Data = imageData,
                                    MimeType = mimeType,
                                    AltText = $"第 {pageCount} 页 图片 {imgIndex}",
                                    Location = $"第 {pageCount} 页"
                                });
                            }
                            catch
                            {
                                // 单个图片提取失败不阻断流程
                            }
                        }
                    }
                }
                catch
                {
                    // PdfPig 版本可能不支持 GetImages，跳过
                }

                var words = page.GetWords().ToList();
                totalWordCount += words.Count;
                if (words.Count == 0)
                {
                    continue;
                }

                var rawLines = GroupIntoLines(words);
                var lineInfos = _lineClassifier.BuildLineInfos(rawLines);

                // 表格检测
                var paths = page.Paths.ToList();
                _tableDetector.DetectTables(words, paths, lineInfos);

                allLineInfos.AddRange(lineInfos);
            }

            result.PageCount = pageCount;

            // 图片存在但提取全部失败时警告
            if (hasImages && result.ImageExports.Count == 0)
            {
                result.Warnings.Add(ConversionWarning.Create(
                    "W_IMG_LOST", "PDF 包含图片，但提取失败", $"全文共 {pageCount} 页"));
            }

            // PdfPig 无可提取文字时，按扫描件交由本地 OCR 引擎处理
            // 受 EnableOcr 配置控制（默认启用）
            var ocrEnabled = EnableOcr ?? true;
            if (totalWordCount == 0 && ocrEnabled)
            {
                var ocr = OfflineOcrService.ExtractPdfText(filePath, cancellationToken);
                if (!ocr.IsSuccess)
                {
                    result.Warnings.Add(ConversionWarning.Create(
                        "W_OCR_FAILED", $"OCR 提取失败: {ocr.ErrorMessage}", "全文"));
                    result.Success = false;
                    result.ErrorMessage = ocr.ErrorMessage;
                    return result;
                }
                result.OcrUsed = true;
                result.Warnings.Add(ConversionWarning.Create(
                    "W_OCR_LOW_CONFIDENCE",
                    "PDF 为扫描件，已通过 OCR 提取文本，质量可能低于原生文本", "全文"));

                sb.AppendLine("<!-- OCR_TEXT_START -->");
                sb.AppendLine(ocr.Text);
                sb.AppendLine("<!-- OCR_TEXT_END -->");

                result.SourceFileName = Path.GetFileName(filePath);
                result.RawMarkdown = sb.ToString();
                result.Success = true;
                result.OutputPath = Path.Combine(outputDirectory, GetSafeFileName(fileName) + ".md");
                return result;
            }

            // 检测双栏 PDF 布局
            DetectMultiColumnLayout(document, result);

            // 分类每行
            _lineClassifier.ClassifyLines(allLineInfos);

            // 合并标题跨行
            _lineClassifier.MergeHeadingLines(allLineInfos);

            // 合并正文行成段落，输出Markdown
            var content = _textMerger.MergeIntoParagraphs(allLineInfos, pageBreakIndices, result.Warnings);

            sb.Append(content);

            // 追加图片引用
            if (result.ImageExports.Count > 0)
            {
                sb.AppendLine();
                foreach (var img in result.ImageExports)
                {
                    sb.AppendLine($"![{img.AltText}](assets/{img.FileName})");
                }
            }
            else if (hasImages)
            {
                sb.AppendLine();
                sb.AppendLine("<!-- IMAGE_PLACEHOLDER: PDF 包含嵌入式图片，提取失败 -->");
                sb.AppendLine();
            }

            result.SourceFileName = Path.GetFileName(filePath);
            result.RawMarkdown = sb.ToString();
            result.Success = true;
            result.OutputPath = Path.Combine(outputDirectory, GetSafeFileName(fileName) + ".md");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;

            // 智能错误分类：区分加密、损坏、格式不兼容
            var msg = ex.Message ?? "";
            if (msg.Contains("encrypt", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("crypt", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = $"PDF 文件已加密或受密码保护，无法解析: {msg}";
            }
            else if (msg.Contains("format", StringComparison.OrdinalIgnoreCase) ||
                     msg.Contains("header", StringComparison.OrdinalIgnoreCase) ||
                     msg.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                     msg.Contains("corrupt", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = $"PDF 文件格式异常或已损坏: {msg}";
            }
            else
            {
                result.ErrorMessage = msg;
            }

            LoggingService.Error($"PDF 解析失败: {filePath}", ex);
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private static string GetSafeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    #region 行分组

    private List<List<Word>> GroupIntoLines(List<Word> words)
    {
        if (words.Count == 0)
            return new List<List<Word>>();

        var fontSizes = words.Select(w => PdfStyles.GetFontSize(w)).Where(f => f > 0).ToList();
        double medianFontSize = fontSizes.Count > 0
            ? fontSizes.OrderBy(f => f).ElementAt(fontSizes.Count / 2)
            : 12;
        double lineThreshold = Math.Max(3, medianFontSize * 0.6);

        var sorted = words.OrderBy(w => -w.BoundingBox.Top)
                         .ThenBy(w => w.BoundingBox.Left)
                         .ToList();

        var lines = new List<List<Word>>();
        var currentLine = new List<Word>();
        double? lineY = null;

        foreach (var word in sorted)
        {
            var wordY = word.BoundingBox.Top;

            if (lineY == null || Math.Abs(wordY - lineY.Value) <= lineThreshold)
            {
                currentLine.Add(word);
                if (lineY == null) lineY = wordY;
            }
            else
            {
                if (currentLine.Count > 0)
                {
                    lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());
                }
                currentLine = new List<Word> { word };
                lineY = wordY;
            }
        }

        if (currentLine.Count > 0)
        {
            lines.Add(currentLine.OrderBy(w => w.BoundingBox.Left).ToList());
        }

        return lines;
    }

    #endregion

    #region 图片与布局检测

    /// <summary>
    /// 根据文件头 magic bytes 判断图片 MIME 类型
    /// </summary>
    private static string DetermineImageMimeType(byte[] data)
    {
        if (data.Length < 4) return "image/png";

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        // GIF: 47 49 46 38
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return "image/gif";
        // BMP: 42 4D
        if (data[0] == 0x42 && data[1] == 0x4D)
            return "image/bmp";
        // TIFF: 49 49 或 4D 4D
        if ((data[0] == 0x49 && data[1] == 0x49) || (data[0] == 0x4D && data[1] == 0x4D))
            return "image/tiff";

        return "image/png";
    }

    /// <summary>
    /// 检测双栏布局：在多页中检测是否存在左右对称的文本列分布
    /// </summary>
    private static void DetectMultiColumnLayout(PdfDocument document, ConversionResult result)
    {
        int multiColumnPages = 0;
        int checkedPages = 0;

        foreach (var page in document.GetPages().Take(10)) // 最多检查前 10 页
        {
            var words = page.GetWords().ToList();
            if (words.Count < 10) continue;
            checkedPages++;

            var pageWidth = page.Width;
            var midX = pageWidth / 2;

            // 统计左半部分和右半部分的文字数量
            int leftCount = words.Count(w => w.BoundingBox.Right < midX);
            int rightCount = words.Count(w => w.BoundingBox.Left >= midX);

            // 如果两侧都有较多文字（各占 >30%），可能是双栏
            double leftRatio = (double)leftCount / words.Count;
            double rightRatio = (double)rightCount / words.Count;

            if (leftRatio > 0.3 && rightRatio > 0.3)
                multiColumnPages++;
        }

        if (checkedPages > 0 && multiColumnPages > checkedPages / 2)
        {
            result.Warnings.Add(ConversionWarning.Create(
                "W_TWO_COLUMN_PDF",
                $"PDF 疑似双栏布局（{multiColumnPages}/{checkedPages} 页），文本提取顺序可能不正确", "全文"));
        }
    }

    #endregion
}
