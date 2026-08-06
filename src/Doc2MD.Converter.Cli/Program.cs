using System;
using System.IO;
using System.Linq;

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
        Console.WriteLine("  convert <input> [--output <dir>]   Convert documents to Markdown");
        Console.WriteLine("  md2docx <input>  [--output <dir>]  Convert Markdown to official DOCX");
        Console.WriteLine("  format <input>   [--output <dir>]  One-click DOCX formatting");
        Console.WriteLine("  version                            Show version");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --output <dir>   Output directory (default: ./output)");
        Console.WriteLine("  --profile <name> Formatting profile: OfficialBasic|EnterpriseEnhanced|GeneralDocument");
    }

    static int RunConvert(string[] args)
    {
        Console.WriteLine("Convert command - Phase 1 placeholder");
        Console.WriteLine("Full implementation coming in Phase 2.");
        return 0;
    }

    static int RunMd2Docx(string[] args)
    {
        Console.WriteLine("Md2Docx command - Phase 1 placeholder");
        Console.WriteLine("Full implementation coming in Phase 2.");
        return 0;
    }

    static int RunFormat(string[] args)
    {
        Console.WriteLine("Format command - Phase 1 placeholder");
        Console.WriteLine("Full implementation coming in Phase 2.");
        return 0;
    }

    static int RunVersion()
    {
        Console.WriteLine("Doc2MD Converter v1.0.0");
        return 0;
    }
}
