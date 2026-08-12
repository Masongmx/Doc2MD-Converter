using System;
using System.IO;
using System.Text;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 输出包写入器：根据 OutputPackageMode 决定输出方式。
/// SingleMd 模式：沿用旧的单文件输出。
/// HybridPackage 模式：创建 document-name/ 目录，写入 document.md + .meta.json + .quality_report.json。
/// </summary>
public static class OutputPackageWriter
{
    public static WriteResult Write(
        string processedMarkdown,
        string metaJson,
        string qualityReportJson,
        ConversionResult result,
        string outputDirectory,
        OutputPackageMode mode)
    {
        Directory.CreateDirectory(outputDirectory);

        var docName = Path.GetFileNameWithoutExtension(result.Metadata.SourceFilePath ?? "output");
        // 清理文件名中的非法字符
        foreach (var c in Path.GetInvalidFileNameChars())
            docName = docName.Replace(c, '_');

        if (mode == OutputPackageMode.SingleMd)
        {
            // 旧模式：单文件 .md（含 frontmatter + AI_AGENT_NOTICE）
            var singleMdPath = Path.Combine(outputDirectory, docName + ".md");
            WriteAllTextAtomic(singleMdPath, processedMarkdown, new UTF8Encoding(true));

            var outputFiles = new List<string> { docName + ".md" };

            // 写入图片资产
            outputFiles.AddRange(WriteAssets(result, outputDirectory));
            // 写入表格 CSV
            outputFiles.AddRange(WriteTables(result, outputDirectory));

            return new WriteResult
            {
                PrimaryOutputPath = singleMdPath,
                OutputFiles = outputFiles
            };
        }

        // HybridPackage 模式
        var packageDir = Path.Combine(outputDirectory, docName ?? "output");
        Directory.CreateDirectory(packageDir);

        var mdPath = Path.Combine(packageDir, "document.md");
        WriteAllTextAtomic(mdPath, processedMarkdown, new UTF8Encoding(true));

        var metaPath = Path.Combine(packageDir, ".meta.json");
        WriteAllTextAtomic(metaPath, metaJson, new UTF8Encoding(false));

        var qualityPath = Path.Combine(packageDir, ".quality_report.json");
        WriteAllTextAtomic(qualityPath, qualityReportJson, new UTF8Encoding(false));

        var hybridFiles = new List<string> { "document.md", ".meta.json", ".quality_report.json" };

        // 写入图片资产到 packageDir/assets/
        hybridFiles.AddRange(WriteAssets(result, packageDir));
        // 写入表格 CSV 到 packageDir/tables/
        hybridFiles.AddRange(WriteTables(result, packageDir));

        return new WriteResult
        {
            PrimaryOutputPath = mdPath,
            OutputFiles = hybridFiles
        };
    }

    /// <summary>
    /// 将图片资产写入 assets/ 子目录
    /// </summary>
    private static List<string> WriteAssets(ConversionResult result, string baseDir)
    {
        var relativeFiles = new List<string>();
        if (result.ImageExports == null || result.ImageExports.Count == 0)
            return relativeFiles;

        var assetsDir = Path.Combine(baseDir, "assets");
        Directory.CreateDirectory(assetsDir);

        foreach (var img in result.ImageExports)
        {
            var imgPath = Path.Combine(assetsDir, img.FileName);
            WriteBinaryAtomic(imgPath, img.Data);
            relativeFiles.Add($"assets/{img.FileName}");
        }

        return relativeFiles;
    }

    /// <summary>
    /// 将表格 CSV 写入 tables/ 子目录
    /// </summary>
    private static List<string> WriteTables(ConversionResult result, string baseDir)
    {
        var relativeFiles = new List<string>();
        if (result.TableExports == null || result.TableExports.Count == 0)
            return relativeFiles;

        var tablesDir = Path.Combine(baseDir, "tables");
        Directory.CreateDirectory(tablesDir);

        foreach (var tbl in result.TableExports)
        {
            var csvPath = Path.Combine(tablesDir, tbl.FileName);
            WriteAllTextAtomic(csvPath, tbl.CsvContent, new UTF8Encoding(true));
            relativeFiles.Add($"tables/{tbl.FileName}");
        }

        return relativeFiles;
    }

    /// <summary>
    /// 原子写入：先写临时文件，成功后移动到目标路径，失败时清理临时文件。
    /// 防止中途崩溃留下半写文件。
    /// </summary>
    private static void WriteAllTextAtomic(string targetPath, string content, Encoding encoding)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".tmp." + Guid.NewGuid().ToString("N");

        try
        {
            File.WriteAllText(tempPath, content, encoding);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // ignore cleanup failure
            }

            throw;
        }
    }

    /// <summary>
    /// 原子写入二进制文件（用于图片资产）
    /// </summary>
    private static void WriteBinaryAtomic(string targetPath, byte[] data)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".tmp." + Guid.NewGuid().ToString("N");

        try
        {
            File.WriteAllBytes(tempPath, data);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // ignore cleanup failure
            }

            throw;
        }
    }
}

public class WriteResult
{
    /// <summary>主输出文件路径（.md），用于 ConversionResult.OutputPath</summary>
    public string PrimaryOutputPath { get; set; } = string.Empty;

    /// <summary>输出包内的所有文件相对路径</summary>
    public List<string> OutputFiles { get; set; } = [];
}
