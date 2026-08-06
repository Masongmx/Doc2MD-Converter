using System.IO;
using System.Text;

namespace Doc2MD.Services;

public static class TextFileReader
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(true, true);
    private static readonly Encoding Gb18030;

    static TextFileReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Gb18030 = Encoding.GetEncoding(54936);
    }

    public static string ReadAllText(string path)
    {
        return ReadAllText(path, out _);
    }

    public static string ReadAllText(string path, out Encoding encoding)
    {
        encoding = DetectEncoding(path);

        try
        {
            return File.ReadAllText(path, encoding);
        }
        catch (DecoderFallbackException)
        {
            encoding = Gb18030;
            return File.ReadAllText(path, encoding);
        }
    }

    private static Encoding DetectEncoding(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length >= 3 &&
            stream.ReadByte() == 0xEF &&
            stream.ReadByte() == 0xBB &&
            stream.ReadByte() == 0xBF)
        {
            return Utf8WithBom;
        }

        return StrictUtf8;
    }
}
