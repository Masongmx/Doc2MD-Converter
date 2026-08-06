using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Doc2MD.Models;
using Doc2MD.Parsers;
using Doc2MD.Pipeline.Services;
using Doc2MD.Services;

namespace Doc2MD.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "convert" => RunConvert(args[1..]),
            "md2docx" => RunMd2Docx(args[1..]),
            "format" => RunFormat(args[1..]),
            "templates" => RunTemplates(),
            "version" => RunVersion(),
            _ => RunConvert(args)
        };
    }

    static void PrintHelp()
    {
        Console.WriteLine("Doc2MD Converter CLI v1.0.0");
        Console.WriteLine();
        Console.WriteLine("Usage: doc2md-converter <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  convert <input>  [--output <dir>]       Convert documents to Markdown");
        Console.WriteLine("  md2docx <input>  [--output <dir>]       Convert Markdown to official DOCX");
        Console.WriteLine("                                          [--template <id>]");
        Console.WriteLine("  format <input>  [--output <dir>]        One-click DOCX formatting");
        Console.WriteLine("                                          [--profile <name>]");
        Console.WriteLine("  templates                                List available templates");
        Console.WriteLine("  version                                  Show version");
        Console.WriteLine();
        Console.WriteLine("Common Options:");
        Console.WriteLine("  --output <dir>     Output directory (default: ./output)");
        Console.WriteLine();
        Console.WriteLine("md2docx Options:");
        Console.WriteLine("  --template <id>    Template ID (default: official-report)");
        Console.WriteLine("                     Available: official-report, meeting-minutes, inspection-report");
        Console.WriteLine();
        Console.WriteLine("format Options:");
        Console.WriteLine("  --profile <name>   Formatting profile (default: 标准公文格式)");
        Console.WriteLine("                     Available: 标准公文格式, 企业增强版, 学术论文格式");
    }

    // ========== convert 命令 ==========

    static int RunConvert(string[] args)
    {
        var (inputPath, outputDir, _) = ParseCommonArgs(args);

        if (string.IsNullOrEmpty(inputPath))
        {
            Console.Error.WriteLine("Error: No input file or directory specified.");
            return 1;
        }

        if (!Path.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input path does not exist: {inputPath}");
            return 1;
        }

        outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        var files = GetInputFiles(inputPath);
        if (files.Count == 0)
        {
            Console.Error.WriteLine("Error: No supported files found.");
            return 1;
        }

        Console.WriteLine($"Converting {files.Count} file(s) to Markdown...");
        int success = 0, failed = 0;

        foreach (var file in files)
        {
            var parser = FindParser(file, ConversionTarget.Markdown);
            if (parser == null)
            {
                Console.WriteLine($"  SKIP: {Path.GetFileName(file)} (unsupported format)");
                failed++;
                continue;
            }

            try
            {
                Console.Write($"  {Path.GetFileName(file)} ... ");
                var result = parser.Parse(file, outputDir, CancellationToken.None);

                if (result.Success)
                {
                    Console.WriteLine($"OK -> {Path.GetFileName(result.OutputPath ?? "")}");
                    success++;
                }
                else
                {
                    Console.WriteLine($"FAILED: {result.ErrorMessage}");
                    failed++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Done: {success} succeeded, {failed} failed.");
        return failed > 0 ? 1 : 0;
    }

    // ========== md2docx 命令（使用 Pipeline） ==========

    static int RunMd2Docx(string[] args)
    {
        var (inputPath, outputDir, extra) = ParseCommonArgs(args);
        var templateId = extra.GetValueOrDefault("--template", "official-report")!;

        if (string.IsNullOrEmpty(inputPath))
        {
            Console.Error.WriteLine("Error: No input Markdown file specified.");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Markdown file not found: {inputPath}");
            return 1;
        }

        outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + ".docx";
        var outputPath = Path.Combine(outputDir, outputFileName);

        Console.WriteLine($"Converting Markdown to DOCX...");
        Console.WriteLine($"  Input:    {inputPath}");
        Console.WriteLine($"  Output:   {outputPath}");
        Console.WriteLine($"  Template: {templateId}");

        try
        {
            var converter = new MarkdownToDocxConverter();
            var result = converter.Convert(inputPath, outputPath, templateId);

            if (result.Success)
            {
                Console.WriteLine();
                Console.WriteLine($"OK -> {outputPath}");

                if (result.FormatIssues.Count > 0)
                {
                    Console.WriteLine($"Format check: {result.FormatIssues.Count} issue(s) found.");
                    foreach (var issue in result.FormatIssues)
                        Console.WriteLine($"  [{issue.Severity}] {issue.Code}: {issue.Message}");
                }
                else
                {
                    Console.WriteLine("Format check: passed (no issues).");
                }

                return 0;
            }
            else
            {
                Console.Error.WriteLine($"FAILED: {result.ErrorMessage}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    // ========== format 命令 ==========

    static int RunFormat(string[] args)
    {
        var (inputPath, outputDir, extra) = ParseCommonArgs(args);
        var profileName = extra.GetValueOrDefault("--profile", "标准公文格式")!;

        if (string.IsNullOrEmpty(inputPath))
        {
            Console.Error.WriteLine("Error: No input DOCX file specified.");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: DOCX file not found: {inputPath}");
            return 1;
        }

        outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + "_formatted.docx";
        var outputPath = Path.Combine(outputDir, outputFileName);

        // 解析排版方案
        var profile = FormattingProfile.GetBuiltIn(profileName);
        var settings = new FormatDocPreviewSettings();
        FormattingProfileService.ApplyTo(profile, settings);

        Console.WriteLine($"Formatting DOCX...");
        Console.WriteLine($"  Input:   {inputPath}");
        Console.WriteLine($"  Output:  {outputPath}");
        Console.WriteLine($"  Profile: {profile.Name}");

        try
        {
            File.Copy(inputPath, outputPath, overwrite: true);
            var formatter = new DocxFormatter(settings);
            formatter.Format(outputPath, outputDir, CancellationToken.None);

            Console.WriteLine();
            Console.WriteLine($"OK -> {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    // ========== templates 命令 ==========

    static int RunTemplates()
    {
        var service = new Pipeline.Services.TemplateService();
        var templates = service.GetAllTemplates();

        Console.WriteLine("Available templates:");
        Console.WriteLine();
        foreach (var t in templates)
        {
            Console.WriteLine($"  {t.Id,-25} {t.Metadata.DisplayName}");
            Console.WriteLine($"  {"",-25} {t.Metadata.Description}");
        }

        return 0;
    }

    // ========== version 命令 ==========

    static int RunVersion()
    {
        Console.WriteLine("Doc2MD Converter v1.0.0");
        return 0;
    }

    // ========== 辅助方法 ==========

    static (string? inputPath, string? outputDir, Dictionary<string, string> extra) ParseCommonArgs(string[] args)
    {
        string? inputPath = null;
        string? outputDir = null;
        var extra = new Dictionary<string, string>();

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
            {
                outputDir = args[++i];
            }
            else if (args[i].StartsWith("--"))
            {
                var key = args[i];
                var value = (i + 1 < args.Length && !args[i + 1].StartsWith("-")) ? args[++i] : "true";
                extra[key] = value;
            }
            else if (inputPath == null)
            {
                inputPath = args[i];
            }
        }

        // Resolve to absolute path
        if (inputPath != null && !Path.IsPathRooted(inputPath))
            inputPath = Path.GetFullPath(inputPath);

        if (outputDir != null && !Path.IsPathRooted(outputDir))
            outputDir = Path.GetFullPath(outputDir);

        return (inputPath, outputDir, extra);
    }

    static List<string> GetInputFiles(string inputPath)
    {
        if (File.Exists(inputPath))
            return [inputPath];

        if (Directory.Exists(inputPath))
        {
            var supportedExtensions = new HashSet<string>
            {
                ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
                ".pdf", ".txt", ".md", ".markdown"
            };

            return Directory.EnumerateFiles(inputPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }

        return [];
    }

    static IDocumentParser? FindParser(string filePath, ConversionTarget target)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return target switch
        {
            ConversionTarget.Markdown => ext switch
            {
                ".docx" or ".doc" => new WordParser(),
                ".xlsx" or ".xls" => new ExcelParser(),
                ".pptx" or ".ppt" => new PowerPointParser(),
                ".pdf" => new PdfParser(),
                ".txt" => new TextParser(),
                _ => null
            },
            _ => null
        };
    }
}
