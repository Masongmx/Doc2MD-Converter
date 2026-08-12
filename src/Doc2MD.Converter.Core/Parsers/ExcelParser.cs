using System.Diagnostics;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Parsers;

public class ExcelParser : IDocumentParser
{
    /// <summary>P0: 大表预览行数阈值</summary>
    private const int MaxPreviewRows = 50;

    public FileType SupportedType => FileType.Excel;
    public ConversionTarget Target => ConversionTarget.Markdown;

    public bool CanParse(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".xlsx" || ext == ".xls";
    }

    public ConversionResult Parse(string filePath, string outputDirectory, CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            result.Metadata.SourceFilePath = filePath;
            result.Metadata.SourceType = "Excel";

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".xls")
            {
                // 路径1：LibreOffice 优先（最高保真度，保留公式结果和样式）
                var legacy = LegacyOfficeConverter.Convert(filePath, ".xlsx", cancellationToken);
                if (legacy.IsSuccess)
                {
                    try { return Parse(legacy.ConvertedPath!, outputDirectory, cancellationToken); }
                    finally { LegacyOfficeConverter.Cleanup(legacy); }
                }

                // 路径2：Excel COM 自动化兜底（需要 Microsoft Excel）
                LoggingService.Info($"[ExcelParser] LibreOffice 不可用，切换 Excel COM 兜底: {filePath}");
                var comResult = OfficeComFallbackService.ConvertXlsToXlsx(filePath, cancellationToken);
                if (comResult.IsSuccess)
                {
                    try
                    {
                        var parsedResult = Parse(comResult.ConvertedPath!, outputDirectory, cancellationToken);
                        if (parsedResult.Success)
                        {
                            parsedResult.Quality.Warnings.Add(ConversionWarning.Create(
                                "W_LEGACY_FALLBACK",
                                ".xls 文件通过 Excel COM 自动化转换（LibreOffice 不可用），公式结果和样式信息可能略有差异",
                                "全文"));
                        }
                        return parsedResult;
                    }
                    finally { OfficeComFallbackService.Cleanup(comResult); }
                }

                // 两条路径都失败
                result.Success = false;
                result.ErrorMessage = $"LibreOffice 转换失败: {legacy.ErrorMessage}；Excel COM 兜底也失败: {comResult.ErrorMessage}";
                return result;
            }

            using var doc = SpreadsheetDocument.Open(filePath, false);
            var workbookPart = doc.WorkbookPart;
            
            if (workbookPart == null)
            {
                result.Success = false;
                result.ErrorMessage = "工作簿内容为空";
                return result;
            }

            var sb = new StringBuilder();
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            sb.AppendLine($"# {fileName}");
            sb.AppendLine();

            // 预加载样式信息（用于日期检测）
            var stylesPart = workbookPart.WorkbookStylesPart;
            CellFormats? cellFormats = stylesPart?.Stylesheet?.CellFormats;

            var sheetsContainer = workbookPart.Workbook?.Sheets;
            if (sheetsContainer == null)
            {
                result.Success = false;
                result.ErrorMessage = "工作簿工作表列表为空";
                return result;
            }

            var sheets = sheetsContainer.Elements<Sheet>().ToList();
            result.Metadata.SheetCount = sheets.Count;

            bool isFirstSheet = true;

            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // P0: 输出 SHEET_START 标记
                sb.AppendLine($"<!-- SHEET_START: {sheet.Name?.Value ?? "工作表"} -->");

                if (!isFirstSheet)
                {
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }

                var sheetName = sheet.Name?.Value ?? "工作表";
                sb.AppendLine($"## {sheetName}");
                sb.AppendLine();

                var relationshipId = sheet.Id?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId)) continue;

                var worksheetPart = (WorksheetPart?)workbookPart.GetPartById(relationshipId);
                if (worksheetPart == null) continue;

                var worksheet = worksheetPart.Worksheet;
                if (worksheet == null) continue;

                var sheetData = worksheet.GetFirstChild<SheetData>();
                if (sheetData == null) continue;

                // 检测合并单元格
                var mergeCells = worksheet.GetFirstChild<MergeCells>();
                if (mergeCells != null && mergeCells.Elements<MergeCell>().Any())
                {
                    result.Quality.Warnings.Add(ConversionWarning.Create(
                        "W_MERGED_CELLS",
                        $"工作表「{sheetName}」包含合并单元格，仅保留首个单元格的值", sheetName));
                }

                // 检测公式
                var formulaCells = sheetData.Descendants<Cell>()
                    .Count(c => c.CellFormula != null);
                if (formulaCells > 0)
                {
                    result.Quality.Warnings.Add(ConversionWarning.Create(
                        "W_FORMULA_LOST",
                        $"工作表「{sheetName}」包含 {formulaCells} 个公式单元格，仅提取计算结果", sheetName));
                }

                // 检测隐藏行
                var hiddenRows = sheetData.Elements<Row>()
                    .Count(r => r.Hidden?.Value == true);
                if (hiddenRows > 0)
                {
                    result.Quality.Warnings.Add(ConversionWarning.Create(
                        "W_HIDDEN_ROW",
                        $"工作表「{sheetName}」包含 {hiddenRows} 个隐藏行，未提取", sheetName));
                }

                // 检测图表
                if (worksheetPart.DrawingsPart != null
                    && worksheetPart.DrawingsPart.Parts.Any(p => p.OpenXmlPart is ChartPart))
                {
                    result.Quality.Warnings.Add(ConversionWarning.Create(
                        "W_CHART_LOST",
                        $"工作表「{sheetName}」包含图表，暂不支持提取", sheetName));
                }

                var table = ParseSheetData(sheetData, workbookPart, cellFormats, sheetName, result.Quality.Warnings, result.TableExports);
                sb.Append(table);

                sb.AppendLine($"<!-- SHEET_END -->");

                isFirstSheet = false;
            }

            result.Metadata.SourceFileName = Path.GetFileName(filePath);
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

    private string ParseSheetData(SheetData sheetData, WorkbookPart workbookPart, 
        CellFormats? cellFormats, string sheetName, List<ConversionWarning> warnings,
        List<TableExport> tableExports)
    {
        var sb = new StringBuilder();
        var rows = sheetData.Elements<Row>().ToList();

        if (rows.Count == 0) return string.Empty;

        int maxCol = 0;
        int maxRow = -1;
        var cellMap = new Dictionary<(int row, int col), string>();

        foreach (var row in rows)
        {
            int rowIndex = (int)(row.RowIndex?.Value ?? (uint)(maxRow + 2));
            if (rowIndex - 1 > maxRow) maxRow = rowIndex - 1;

            foreach (var cell in row.Elements<Cell>())
            {
                var refValue = cell.CellReference?.Value ?? "";
                int colIndex = GetColumnIndex(refValue);
                if (colIndex > maxCol) maxCol = colIndex;

                var cellValue = GetCellValue(cell, workbookPart, cellFormats);
                cellMap[(rowIndex - 1, colIndex)] = cellValue;
            }
        }

        if (maxRow < 0) return string.Empty;

        // P0: 大表截断逻辑
        bool truncated = maxRow + 1 > MaxPreviewRows;
        int displayRows = truncated ? MaxPreviewRows : maxRow + 1;

        for (int r = 0; r < displayRows; r++)
        {
            var rowValues = new List<string>();
            for (int c = 0; c <= maxCol; c++)
            {
                if (cellMap.TryGetValue((r, c), out var value))
                {
                    rowValues.Add(value);
                }
                else
                {
                    rowValues.Add("");
                }
            }

            if (rowValues.All(v => string.IsNullOrEmpty(v))) continue;

            sb.Append("| " + string.Join(" | ", rowValues.Select(v => EscapeMdCell(v))) + " |");
            
            if (r == 0)
            {
                sb.AppendLine();
                sb.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", maxCol + 1)) + " |");
            }
            else
            {
                sb.AppendLine();
            }
        }

        // 截断提示 + CSV 导出
        if (truncated)
        {
            sb.AppendLine();
            sb.AppendLine($"<!-- TABLE_TRUNCATED: 工作表「{sheetName}」共有 {maxRow + 1} 行，仅展示前 {MaxPreviewRows} 行预览，完整数据见 tables/ 目录 -->");
            sb.AppendLine($"> *表格已截断：共 {maxRow + 1} 行，仅展示前 {MaxPreviewRows} 行*");
            warnings.Add(ConversionWarning.Create(
                "W_TABLE_TRUNCATED",
                $"工作表「{sheetName}」共 {maxRow + 1} 行，仅展示前 {MaxPreviewRows} 行", sheetName));

            // 生成完整 CSV 导出
            var csv = GenerateCsv(cellMap, maxRow, maxCol);
            var safeName = string.Join("_", sheetName.Split(Path.GetInvalidFileNameChars()));
            tableExports.Add(new TableExport
            {
                FileName = $"{safeName}.csv",
                CsvContent = csv
            });
        }

        return sb.ToString();
    }

    private string EscapeMdCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }

    /// <summary>
    /// 将完整表格数据生成为 CSV 格式
    /// </summary>
    private static string GenerateCsv(Dictionary<(int row, int col), string> cellMap, int maxRow, int maxCol)
    {
        var sb = new StringBuilder();
        for (int r = 0; r <= maxRow; r++)
        {
            var rowValues = new List<string>();
            for (int c = 0; c <= maxCol; c++)
            {
                cellMap.TryGetValue((r, c), out var value);
                rowValues.Add(EscapeCsvField(value ?? ""));
            }
            sb.AppendLine(string.Join(",", rowValues));
        }
        return sb.ToString();
    }

    /// <summary>
    /// CSV 字段转义：含逗号、引号、换行时用双引号包裹
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private string GetCellValue(Cell cell, WorkbookPart workbookPart, 
        CellFormats? cellFormats)
    {
        // 优先处理 InlineString：某些单元格使用 InlineString 而非 SharedString 或数值
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            var inlineString = cell.InlineString;
            if (inlineString != null)
            {
                // InlineString 可能包含多个 Text 节点（富文本格式），合并所有文本
                var sb = new StringBuilder();
                foreach (var text in inlineString.Elements<Text>())
                {
                    sb.Append(text.Text);
                }
                return sb.ToString();
            }
            return "";
        }

        var cellValue = cell.CellValue;
        if (cellValue == null) return "";

        var value = cellValue.Text ?? "";

        // 日期格式检测：Excel 日期存储为数字，需要通过样式判断
        if (cell.DataType?.Value != CellValues.SharedString &&
            cell.DataType?.Value != CellValues.Boolean &&
            cell.DataType?.Value != CellValues.Error)
        {
            var isDate = IsCellDateFormat(cell, cellFormats);
            if (isDate && double.TryParse(value, out var oleDate))
            {
                try
                {
                    // OLE Automation Date 转 DateTime
                    var date = DateTime.FromOADate(oleDate);
                    return date.ToString("yyyy-MM-dd");
                }
                catch
                {
                    // 转换失败则返回原始值
                }
            }
        }

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            var sharedStrings = workbookPart.SharedStringTablePart;
            if (sharedStrings != null)
            {
                var index = int.TryParse(value, out var i) ? i : 0;
                var sharedStringTable = sharedStrings.SharedStringTable;
                if (sharedStringTable == null) return "";

                var sharedString = sharedStringTable.ElementAtOrDefault(index);
                return sharedString?.InnerText ?? "";
            }
        }
        else if (cell.DataType?.Value == CellValues.Boolean)
        {
            return value == "1" ? "TRUE" : "FALSE";
        }
        else if (cell.DataType?.Value == CellValues.Error)
        {
            return $"#{value}";
        }

        return value;
    }

    /// <summary>
    /// 检测单元格是否为日期格式
    /// </summary>
    private bool IsCellDateFormat(Cell cell, CellFormats? cellFormats)
    {
        if (cellFormats == null) return false;

        var styleIndex = cell.StyleIndex?.Value;
        if (styleIndex == null) return false;

        var cellFormat = cellFormats.ElementAtOrDefault((int)styleIndex) as CellFormat;
        if (cellFormat == null) return false;

        var numFmtId = cellFormat.NumberFormatId?.Value;
        if (numFmtId == null) return false;

        // Excel 内置日期格式的 NumberFormatId 范围：
        // 14-22: 标准日期时间格式
        // 27-36: 东亚日期格式
        // 45-47: 时间格式
        // 50-58: 东亚日期时间格式
        if ((numFmtId >= 14 && numFmtId <= 22) ||
            (numFmtId >= 27 && numFmtId <= 36) ||
            (numFmtId >= 45 && numFmtId <= 47) ||
            (numFmtId >= 50 && numFmtId <= 58))
        {
            return true;
        }

        // 自定义格式 (>=164)：检查格式字符串是否包含日期占位符
        if (numFmtId >= 164)
        {
            var stylesheet = cellFormats.Parent as DocumentFormat.OpenXml.Spreadsheet.Stylesheet;
            var numberingFormats = stylesheet?.NumberingFormats;
            if (numberingFormats != null)
            {
                foreach (var nf in numberingFormats.OfType<NumberingFormat>())
                {
                    if (nf.NumberFormatId?.Value == numFmtId)
                    {
                        var fmtCode = nf.FormatCode?.Value ?? "";
                        return IsDateFormatString(fmtCode);
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断自定义格式字符串是否为日期格式
    /// 修复 v2.0：精确区分 mm（月份）和 mm（分钟）——仅当不含 h/s（时分秒）时才将 mm 视为月份
    /// </summary>
    private bool IsDateFormatString(string formatCode)
    {
        if (string.IsNullOrEmpty(formatCode)) return false;
        var lower = formatCode.ToLowerInvariant();

        // 检查是否包含时间占位符（h/小时、s/秒）——含时间时 mm 是分钟不是月份
        bool hasTimePart = lower.Contains('h') || lower.Contains('s') || lower.Contains("时") || lower.Contains("秒");

        // 日期部分检测：y（年）、d（日）一定是日期；m 在纯日期上下文中是月份，在时间上下文中是分钟
        bool hasYear = lower.Contains('y') || lower.Contains("年");
        bool hasDay = lower.Contains('d') || lower.Contains("日");
        bool hasMonth = lower.Contains('m') || lower.Contains("月");

        // 如果有时间部分，mm 是分钟——不视为日期格式（除非同时有 y 或 d）
        if (hasTimePart && !hasYear && !hasDay)
            return false;

        return hasYear || hasDay || (hasMonth && !hasTimePart);
    }

    private int GetColumnIndex(string cellReference)
    {
        var columnPart = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        if (string.IsNullOrEmpty(columnPart)) return 0;
        
        int index = 0;
        foreach (char c in columnPart.ToUpperInvariant())
        {
            index = index * 26 + (c - 'A' + 1);
        }
        
        return index - 1;
    }
}
