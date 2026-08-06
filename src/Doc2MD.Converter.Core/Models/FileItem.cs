using CommunityToolkit.Mvvm.ComponentModel;

namespace Doc2MD.Models;

public enum AppMode
{
    ToMarkdown,
    MarkdownToDocx,
    FormatDoc
}

public enum FileStatus
{
    Pending,
    Processing,
    Done,
    Failed,
    Unsupported,
    Skipped
}

public enum FileType
{
    Unknown,
    Word,
    Excel,
    PowerPoint,
    Text,
    PDF,
    Markdown
}

public enum ConversionTarget
{
    Markdown,
    OfficialDocx
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public partial class FileItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public FileType Type { get; set; } = FileType.Unknown;

    [ObservableProperty]
    private FileStatus _status = FileStatus.Pending;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    private bool _isSelected = true;

    public static FileType GetFileType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".docx" or ".doc" => FileType.Word,
            ".xlsx" or ".xls" => FileType.Excel,
            ".pptx" or ".ppt" => FileType.PowerPoint,
            ".txt" => FileType.Text,
            ".pdf" => FileType.PDF,
            ".md" or ".markdown" => FileType.Markdown,
            _ => FileType.Unknown
        };
    }
}
