using CommunityToolkit.Mvvm.ComponentModel;

namespace Doc2MD.Models;

public enum ThemeMode
{
    Light,
    Dark,
    System
}

public enum MotionLevel
{
    Off,
    Standard,
    Smooth
}

public enum OutputPackageMode
{
    /// <summary>旧模式：单个 .md 文件输出</summary>
    SingleMd,
    /// <summary>混合包模式：document-name/ 目录 + document.md + .meta.json + .quality_report.json</summary>
    HybridPackage
}

public class AppConfig
{
    public GeneralSettings General { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public ConversionSettings Conversion { get; set; } = new();
    public TemplateSettings Templates { get; set; } = new();
    public PreviewSettings Preview { get; set; } = new();
    public RecentState Recent { get; set; } = new();
}

public partial class GeneralSettings : ObservableObject
{
    [ObservableProperty]
    private string _defaultOutputDir = string.Empty;

    [ObservableProperty]
    private bool _keepOriginalFileName = true;

    [ObservableProperty]
    private bool _overwriteExistingFile = false;

    [ObservableProperty]
    private bool _autoOpenOutputDir = false;
}

public partial class AppearanceSettings : ObservableObject
{
    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.Light;

    [ObservableProperty]
    private MotionLevel _motion = MotionLevel.Smooth;

    [ObservableProperty]
    private double _scale = 1.0;
}

public partial class ConversionSettings : ObservableObject
{
    [ObservableProperty]
    private bool _recursiveScan = true;

    [ObservableProperty]
    private bool _ignoreHiddenFiles = true;

    [ObservableProperty]
    private int _maxConcurrentTasks = 2;

    [ObservableProperty]
    private bool _continueOnError = true;

    [ObservableProperty]
    private bool _preserveFolderStructure = true;

    [ObservableProperty]
    private OutputPackageMode _outputPackageMode = OutputPackageMode.HybridPackage;
}

public partial class TemplateSettings : ObservableObject
{
    [ObservableProperty]
    private string _defaultDocxTemplate = string.Empty;

    [ObservableProperty]
    private string _officialDocTemplate = string.Empty;
}

public class PreviewSettings
{
    public MarkdownToDocxPreviewSettings MarkdownToDocx { get; set; } = new();
    public DocumentToMarkdownPreviewSettings DocumentToMarkdown { get; set; } = new();
    public FormatDocPreviewSettings FormatDoc { get; set; } = new();
}

public partial class MarkdownToDocxPreviewSettings : ObservableObject
{
    [ObservableProperty]
    private string _templatePath = string.Empty;

    [ObservableProperty]
    private string _titleStyle = "公文标题";

    [ObservableProperty]
    private string _bodyStyle = "正文";

    [ObservableProperty]
    private string _codeBlockStyle = "等宽";

    [ObservableProperty]
    private bool _generateToc = false;

    [ObservableProperty]
    private bool _keepImages = true;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _footerText = string.Empty;

    [ObservableProperty]
    private string _pageMargin = "上 2.8 / 下 2.6 / 左右 2.8";

    // --- 结构化字段（排版引擎实际读取） ---

    /// <summary>公文标题字体</summary>
    [ObservableProperty]
    private string _titleFont = "方正小标宋简体";

    /// <summary>黑体标题字体（二级标题）</summary>
    [ObservableProperty]
    private string _headingFont = "黑体";

    /// <summary>正文字体</summary>
    [ObservableProperty]
    private string _bodyFont = "仿宋_GB2312";

    /// <summary>三级标题字体（楷体）</summary>
    [ObservableProperty]
    private string _subheadingFont = "楷体_GB2312";

    /// <summary>代码块字体</summary>
    [ObservableProperty]
    private string _codeBlockFont = "Consolas";

    /// <summary>正文字号（磅），默认三号字 16pt</summary>
    [ObservableProperty]
    private double _bodyFontSizePt = 16.0;

    /// <summary>标题字号（磅），默认二号字 22pt</summary>
    [ObservableProperty]
    private double _titleFontSizePt = 22.0;

    /// <summary>二级标题字号（磅），默认三号字加粗 18pt</summary>
    [ObservableProperty]
    private double _headingFontSizePt = 18.0;

    /// <summary>三级标题字号（磅），默认三号字 16pt</summary>
    [ObservableProperty]
    private double _subheadingFontSizePt = 16.0;

    /// <summary>代码块字号（磅），默认 10.5pt</summary>
    [ObservableProperty]
    private double _codeBlockFontSizePt = 10.5;

    /// <summary>固定行距（磅），默认 28pt</summary>
    [ObservableProperty]
    private double _lineSpacingPt = 28.0;

    /// <summary>首行缩进（字符数），默认 2</summary>
    [ObservableProperty]
    private double _firstLineIndentChars = 2.0;

    /// <summary>段前间距（磅），默认 0</summary>
    [ObservableProperty]
    private double _beforeSpacingPt = 0;

    /// <summary>段后间距（磅），默认 0</summary>
    [ObservableProperty]
    private double _afterSpacingPt = 0;

    /// <summary>页边距-上（cm）</summary>
    [ObservableProperty]
    private double _pageMarginTopCm = 3.7;

    /// <summary>页边距-下（cm）</summary>
    [ObservableProperty]
    private double _pageMarginBottomCm = 3.5;

    /// <summary>页边距-左（cm）</summary>
    [ObservableProperty]
    private double _pageMarginLeftCm = 2.8;

    /// <summary>页边距-右（cm）</summary>
    [ObservableProperty]
    private double _pageMarginRightCm = 2.6;

    /// <summary>排版方案名称</summary>
    [ObservableProperty]
    private string _formatScheme = "标准公文格式";
}

public partial class DocumentToMarkdownPreviewSettings : ObservableObject
{
    [ObservableProperty]
    private bool _extractImages = true;

    [ObservableProperty]
    private bool _preserveTables = true;

    [ObservableProperty]
    private bool _splitByPage = false;

    [ObservableProperty]
    private bool _enableOcr = false;
}

public partial class FormatDocPreviewSettings : ObservableObject
{
    [ObservableProperty]
    private string _templatePath = string.Empty;

    [ObservableProperty]
    private string _formatScheme = "标准公文格式";

    [ObservableProperty]
    private string _fontFamily = "仿宋";

    [ObservableProperty]
    private string _titleLevel = "三级标题";

    [ObservableProperty]
    private string _paragraphIndent = "首行缩进 2 字符";

    [ObservableProperty]
    private string _pageMargin = "上 3.7 / 下 3.5 / 左右 2.8";

    [ObservableProperty]
    private bool _headerFooterEnabled = true;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _footerText = string.Empty;

    // --- 结构化字段（排版引擎实际读取） ---

    /// <summary>公文标题字体</summary>
    [ObservableProperty]
    private string _titleFont = "方正小标宋简体";

    /// <summary>黑体标题字体（二级标题）</summary>
    [ObservableProperty]
    private string _headingFont = "黑体";

    /// <summary>正文字体，默认仿宋_GB2312（与 GB/T 9704 一致）</summary>
    [ObservableProperty]
    private string _bodyFont = "仿宋_GB2312";

    /// <summary>三级标题字体（楷体）</summary>
    [ObservableProperty]
    private string _subheadingFont = "楷体_GB2312";

    /// <summary>代码块字体</summary>
    [ObservableProperty]
    private string _codeBlockFont = "Consolas";

    /// <summary>正文字号（磅），默认三号字 16pt</summary>
    [ObservableProperty]
    private double _bodyFontSizePt = 16.0;

    /// <summary>标题字号（磅），默认二号字 22pt</summary>
    [ObservableProperty]
    private double _titleFontSizePt = 22.0;

    /// <summary>二级标题字号（磅），默认三号字加粗 18pt</summary>
    [ObservableProperty]
    private double _headingFontSizePt = 18.0;

    /// <summary>三级标题字号（磅），默认三号字 16pt</summary>
    [ObservableProperty]
    private double _subheadingFontSizePt = 16.0;

    /// <summary>代码块字号（磅），默认 10.5pt</summary>
    [ObservableProperty]
    private double _codeBlockFontSizePt = 10.5;

    /// <summary>固定行距（磅），默认 28pt</summary>
    [ObservableProperty]
    private double _lineSpacingPt = 28.0;

    /// <summary>首行缩进（字符数），默认 2</summary>
    [ObservableProperty]
    private double _firstLineIndentChars = 2.0;

    /// <summary>页边距-上（cm）</summary>
    [ObservableProperty]
    private double _pageMarginTopCm = 3.7;

    /// <summary>页边距-下（cm）</summary>
    [ObservableProperty]
    private double _pageMarginBottomCm = 3.5;

    /// <summary>页边距-左（cm）</summary>
    [ObservableProperty]
    private double _pageMarginLeftCm = 2.8;

    /// <summary>页边距-右（cm）</summary>
    [ObservableProperty]
    private double _pageMarginRightCm = 2.6;

    /// <summary>段前间距（磅）</summary>
    [ObservableProperty]
    private double _beforeSpacingPt = 0;

    /// <summary>段后间距（磅）</summary>
    [ObservableProperty]
    private double _afterSpacingPt = 0;
}

public class RecentState
{
    public List<string> RecentFolders { get; set; } = new();
    public List<string> RecentOutputDirectories { get; set; } = new();
}
