using System.ComponentModel;
using System.Runtime.CompilerServices;

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
    Markdown
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// 文件列表项。Status/ErrorMessage/OutputPath/IsSelected 需要属性变更通知。
/// </summary>
public class FileItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public FileType Type { get; set; } = FileType.Unknown;

    private FileStatus _status = FileStatus.Pending;
    private string? _errorMessage;
    private string? _outputPath;
    private bool _isSelected = true;

    public FileStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public string? OutputPath
    {
        get => _outputPath;
        set { _outputPath = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
