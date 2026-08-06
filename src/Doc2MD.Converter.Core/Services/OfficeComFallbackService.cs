using System.IO;
using System.Runtime.InteropServices;

namespace Doc2MD.Services;

/// <summary>
/// Office COM 自动化兜底转换器：在 LibreOffice 不可用时，使用本机 Microsoft Word/Excel
/// 将旧式二进制 .doc/.xls 文件转为 OpenXML 格式，然后复用现有解析器。
/// 采用后期绑定（dynamic），不需要任何 COM 互操作 NuGet 包。
/// </summary>
public static class OfficeComFallbackService
{
    /// <summary>
    /// 使用 Word COM 自动化将 .doc 转换为 .docx。
    /// 需要 Microsoft Word 安装。
    /// </summary>
    public static LegacyConversionResult ConvertDocToDocx(string sourcePath, CancellationToken cancellationToken)
    {
        Type? wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType == null)
            return LegacyConversionResult.Fail("未找到 Microsoft Word，COM 自动化不可用。");

        dynamic? wordApp = null;
        dynamic? document = null;
        var tempDir = Path.Combine(Path.GetTempPath(), "Doc2MD", "ComFallback", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            cancellationToken.ThrowIfCancellationRequested();

            wordApp = Activator.CreateInstance(wordType);
            wordApp!.Visible = false;
            wordApp.DisplayAlerts = 0; // wdAlertsNone

            // 打开文档（只读模式，不添加到最近文件列表）
            var documents = wordApp.Documents;
            document = documents.Open(sourcePath, false, true, false);

            cancellationToken.ThrowIfCancellationRequested();

            var convertedPath = Path.Combine(tempDir,
                Path.GetFileNameWithoutExtension(sourcePath) + ".docx");

            // SaveAs：wdFormatXMLDocument = 12
            document.SaveAs(convertedPath, 12);

            document.Close(false);
            document = null;

            wordApp.Quit();
            wordApp = null;

            if (!File.Exists(convertedPath))
                return LegacyConversionResult.Fail("Word COM 转换完成，但未找到输出文件。");

            LoggingService.Info($"[OfficeComFallback] Word COM 转换成功: {sourcePath} -> {convertedPath}");
            return LegacyConversionResult.Success(convertedPath, tempDir);
        }
        catch (OperationCanceledException)
        {
            return LegacyConversionResult.Fail("已取消 Word COM 转换。");
        }
        catch (Exception ex)
        {
            return LegacyConversionResult.Fail($"Word COM 转换失败: {ex.Message}");
        }
        finally
        {
            // 确保 Word 进程被关闭
            try { document?.Close(false); } catch { }
            try { wordApp?.Quit(); } catch { }

            if (document != null)
            {
                try { Marshal.ReleaseComObject(document); } catch { }
                document = null;
            }
            if (wordApp != null)
            {
                try { Marshal.ReleaseComObject(wordApp); } catch { }
                wordApp = null;
            }
        }
    }

    /// <summary>
    /// 使用 Excel COM 自动化将 .xls 转换为 .xlsx。
    /// 需要 Microsoft Excel 安装。
    /// </summary>
    public static LegacyConversionResult ConvertXlsToXlsx(string sourcePath, CancellationToken cancellationToken)
    {
        Type? excelType = Type.GetTypeFromProgID("Excel.Application");
        if (excelType == null)
            return LegacyConversionResult.Fail("未找到 Microsoft Excel，COM 自动化不可用。");

        dynamic? excelApp = null;
        dynamic? workbook = null;
        var tempDir = Path.Combine(Path.GetTempPath(), "Doc2MD", "ComFallback", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            cancellationToken.ThrowIfCancellationRequested();

            excelApp = Activator.CreateInstance(excelType);
            excelApp!.Visible = false;
            excelApp.DisplayAlerts = false;
            excelApp.ScreenUpdating = false;

            var workbooks = excelApp.Workbooks;
            // Workbooks.Open(Filename, UpdateLinks, ReadOnly)
            workbook = workbooks.Open(sourcePath, 0, true);

            cancellationToken.ThrowIfCancellationRequested();

            var convertedPath = Path.Combine(tempDir,
                Path.GetFileNameWithoutExtension(sourcePath) + ".xlsx");

            // SaveAs：xlOpenXMLWorkbook = 51
            workbook.SaveAs(convertedPath, 51);

            workbook.Close(false);
            workbook = null;

            excelApp.Quit();
            excelApp = null;

            if (!File.Exists(convertedPath))
                return LegacyConversionResult.Fail("Excel COM 转换完成，但未找到输出文件。");

            LoggingService.Info($"[OfficeComFallback] Excel COM 转换成功: {sourcePath} -> {convertedPath}");
            return LegacyConversionResult.Success(convertedPath, tempDir);
        }
        catch (OperationCanceledException)
        {
            return LegacyConversionResult.Fail("已取消 Excel COM 转换。");
        }
        catch (Exception ex)
        {
            return LegacyConversionResult.Fail($"Excel COM 转换失败: {ex.Message}");
        }
        finally
        {
            // 确保 Excel 进程被关闭
            try { workbook?.Close(false); } catch { }
            try { excelApp?.Quit(); } catch { }

            if (workbook != null)
            {
                try { Marshal.ReleaseComObject(workbook); } catch { }
                workbook = null;
            }
            if (excelApp != null)
            {
                try { Marshal.ReleaseComObject(excelApp); } catch { }
                excelApp = null;
            }
        }
    }

    /// <summary>
    /// 清理 COM 转换产生的临时目录。
    /// </summary>
    public static void Cleanup(LegacyConversionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.TempDirectory))
        {
            try
            {
                if (Directory.Exists(result.TempDirectory))
                    Directory.Delete(result.TempDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                LoggingService.Warning($"清理 COM 临时目录失败: {result.TempDirectory}, 错误: {ex.Message}");
            }
        }
    }
}
