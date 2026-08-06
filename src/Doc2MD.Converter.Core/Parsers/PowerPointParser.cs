using System.Diagnostics;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Parsers;

public class PowerPointParser : IDocumentParser
{
    public FileType SupportedType => FileType.PowerPoint;
    public ConversionTarget Target => ConversionTarget.Markdown;

    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".pptx" || ext == ".ppt";
    }

    public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            result.SourceFilePath = filePath;
            result.SourceType = "PowerPoint";

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".ppt")
            {
                var legacy = LegacyOfficeConverter.Convert(filePath, ".pptx", cancellationToken);
                if (!legacy.IsSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = $"{legacy.ErrorMessage}（.ppt 格式目前需要 LibreOffice 支持，暂无纯 .NET 兜底方案。请安装 LibreOffice 或将其放入 tools\\LibreOffice 目录。）";
                    return result;
                }
                try { return Parse(legacy.ConvertedPath!, outputDirectory, cancellationToken); }
                finally { LegacyOfficeConverter.Cleanup(legacy); }
            }

            using var doc = PresentationDocument.Open(filePath, false);
            var presentationPart = doc.PresentationPart;
            
            if (presentationPart == null)
            {
                result.Success = false;
                result.ErrorMessage = "演示文稿内容为空";
                return result;
            }

            var sb = new StringBuilder();
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            sb.AppendLine($"# {fileName}");
            sb.AppendLine();

            var slideIdList = presentationPart.Presentation?.SlideIdList;
            if (slideIdList == null)
            {
                result.Success = false;
                result.ErrorMessage = "演示文稿幻灯片列表为空";
                return result;
            }
            int slideNumber = 1;
            bool hasImages = false;
            int imgIndex = 0;

            foreach (var slideId in slideIdList.Elements<SlideId>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relationshipId = slideId.RelationshipId?.Value;
                if (string.IsNullOrEmpty(relationshipId)) continue;

                var slidePart = (SlidePart?)presentationPart.GetPartById(relationshipId);
                if (slidePart == null) continue;

                sb.AppendLine($"<!-- SLIDE_START: {slideNumber} -->");
                sb.AppendLine($"## 第 {slideNumber} 页");
                sb.AppendLine();

                // 提取本页幻灯片中的图片（实际字节提取）
                if (slidePart.ImageParts.Any())
                {
                    hasImages = true;
                    foreach (var imagePart in slidePart.ImageParts)
                    {
                        try
                        {
                            imgIndex++;
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
                                AltText = $"幻灯片 {slideNumber} 图片 {imgIndex}",
                                Location = $"幻灯片 {slideNumber}"
                            });

                            // 在幻灯片文本后插入图片引用
                            sb.AppendLine($"![幻灯片 {slideNumber} 图片](assets/{imgFileName})");
                        }
                        catch
                        {
                            // 单个图片提取失败不阻断流程
                        }
                    }
                }

                // 提取幻灯片正文
                if (slidePart.Slide != null)
                {
                    ExtractSlideText(slidePart.Slide, sb);
                }

                // 提取演讲者备注
                ExtractNotes(slidePart, sb);

                sb.AppendLine($"<!-- SLIDE_END: {slideNumber} -->");
                sb.AppendLine();
                slideNumber++;
            }

            result.SlideCount = slideNumber - 1;

            // 图片存在但提取全部失败时警告
            if (hasImages && result.ImageExports.Count == 0)
            {
                result.Warnings.Add(ConversionWarning.Create(
                    "W_IMG_LOST", "PPT 演示文稿包含图片，但提取失败"));
                sb.AppendLine("<!-- IMAGE_PLACEHOLDER: PPT 演示文稿包含嵌入式图片，提取失败 -->");
            }

            // 检测图表和嵌入对象
            bool hasChart = false, hasEmbedded = false;
            foreach (var slideId2 in slideIdList.Elements<SlideId>())
            {
                var relId2 = slideId2.RelationshipId?.Value;
                if (string.IsNullOrEmpty(relId2)) continue;
                var sp = (SlidePart?)presentationPart.GetPartById(relId2);
                if (sp == null) continue;

                foreach (var part in sp.Parts)
                {
                    if (part.OpenXmlPart is ChartPart) hasChart = true;
                    if (part.OpenXmlPart is EmbeddedObjectPart || part.OpenXmlPart is EmbeddedPackagePart) hasEmbedded = true;
                }

                // 也检查 ChartParts 集合（某些版本的 OpenXml 中图表直接挂在 SlidePart 下）
                if (sp.ChartParts.Any()) hasChart = true;
            }

            if (hasChart)
                result.Warnings.Add(ConversionWarning.Create(
                    "W_CHART_LOST", "PPT 演示文稿包含图表，暂不支持提取"));
            if (hasEmbedded)
                result.Warnings.Add(ConversionWarning.Create(
                    "W_EMBEDDED_OBJECT_LOST", "PPT 演示文稿包含嵌入对象，暂不支持提取"));

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

    private void ExtractSlideText(Slide slide, StringBuilder sb)
    {
        foreach (var shape in slide.Descendants<Shape>())
        {
            ExtractShapeText(shape, sb);
        }
    }

    private void ExtractShapeText(Shape shape, StringBuilder sb)
    {
        var textBody = shape.TextBody;
        if (textBody == null) return;

        foreach (var element in textBody.ChildElements)
        {
            if (element is DocumentFormat.OpenXml.Drawing.Paragraph para)
            {
                var text = GetParagraphText(para);
                if (!string.IsNullOrEmpty(text))
                {
                    sb.AppendLine(text.Trim());
                }
            }
        }
    }

    /// <summary>
    /// 提取演讲者备注
    /// </summary>
    private void ExtractNotes(SlidePart slidePart, StringBuilder sb)
    {
        var notesSlidePart = slidePart.NotesSlidePart;
        if (notesSlidePart == null) return;

        var notesText = new StringBuilder();
        var notesSlide = notesSlidePart.NotesSlide;

        if (notesSlide == null) return;

        foreach (var shape in notesSlide.Descendants<Shape>())
        {
            var textBody = shape.TextBody;
            if (textBody == null) continue;

            foreach (var element in textBody.ChildElements)
            {
                if (element is DocumentFormat.OpenXml.Drawing.Paragraph para)
                {
                    var text = GetParagraphText(para);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        notesText.AppendLine(text.Trim());
                    }
                }
            }
        }

        if (notesText.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("> **备注**");
            foreach (var line in notesText.ToString().Split('\n'))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    sb.AppendLine($"> {trimmed}");
                }
            }
        }
    }

    private string GetParagraphText(DocumentFormat.OpenXml.Drawing.Paragraph para)
    {
        var sb = new StringBuilder();
        foreach (var run in para.Elements<DocumentFormat.OpenXml.Drawing.Run>())
        {
            if (run.Text != null)
            {
                var text = run.Text.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text);
                }
            }
        }
        return sb.ToString();
    }
}
