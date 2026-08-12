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
    /// <summary>单个 .md 文件输出</summary>
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

public class GeneralSettings
{
    public string DefaultOutputDir { get; set; } = string.Empty;
    public bool KeepOriginalFileName { get; set; } = true;
    public bool OverwriteExistingFile { get; set; } = false;
    public bool AutoOpenOutputDir { get; set; } = false;
    /// <summary>界面语言：zh-CN（默认）或 en-US。</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>是否已完成首次启动引导。默认 false，完成引导后持久化为 true。</summary>
    public bool HasCompletedOnboarding { get; set; } = false;
}

public class AppearanceSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public MotionLevel Motion { get; set; } = MotionLevel.Smooth;
    public double Scale { get; set; } = 1.0;
}

public class ConversionSettings
{
    public bool RecursiveScan { get; set; } = true;
    public bool IgnoreHiddenFiles { get; set; } = true;
    public int MaxConcurrentTasks { get; set; } = 2;
    public bool ContinueOnError { get; set; } = true;
    public bool PreserveFolderStructure { get; set; } = true;
    public OutputPackageMode OutputPackageMode { get; set; } = OutputPackageMode.SingleMd;

    /// <summary>R3: 单次文件夹扫描的文件数量上限，达到后停止扫描（默认 5000）。</summary>
    public int MaxScanFileCount { get; set; } = 5000;
}

public class TemplateSettings
{
    public string DefaultDocxTemplate { get; set; } = string.Empty;
    public string OfficialDocTemplate { get; set; } = string.Empty;
}

public class PreviewSettings
{
    public MarkdownToDocxPreviewSettings MarkdownToDocx { get; set; } = new();
    public DocumentToMarkdownPreviewSettings DocumentToMarkdown { get; set; } = new();
    public FormatDocPreviewSettings FormatDoc { get; set; } = new();
}

public class MarkdownToDocxPreviewSettings
{
    public string TemplatePath { get; set; } = string.Empty;
    public string TitleStyle { get; set; } = "公文标题";
    public string BodyStyle { get; set; } = "正文";
    public string CodeBlockStyle { get; set; } = "等宽";
    public bool GenerateToc { get; set; } = false;
    public bool KeepImages { get; set; } = true;
    public string HeaderText { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
    public string PageMargin { get; set; } = "上 2.8 / 下 2.6 / 左右 2.8";

    // --- 结构化字段（排版引擎实际读取） ---
    public string TitleFont { get; set; } = "方正小标宋简体";
    public string HeadingFont { get; set; } = "黑体";
    public string BodyFont { get; set; } = "仿宋_GB2312";
    public string SubheadingFont { get; set; } = "楷体_GB2312";
    public string CodeBlockFont { get; set; } = "Consolas";
    public double BodyFontSizePt { get; set; } = 16.0;
    public double TitleFontSizePt { get; set; } = 22.0;
    public double HeadingFontSizePt { get; set; } = 18.0;
    public double SubheadingFontSizePt { get; set; } = 16.0;
    public double CodeBlockFontSizePt { get; set; } = 10.5;
    public double LineSpacingPt { get; set; } = 28.0;
    public double FirstLineIndentChars { get; set; } = 2.0;
    public double BeforeSpacingPt { get; set; } = 0;
    public double AfterSpacingPt { get; set; } = 0;
    public double PageMarginTopCm { get; set; } = 3.7;
    public double PageMarginBottomCm { get; set; } = 3.5;
    public double PageMarginLeftCm { get; set; } = 2.8;
    public double PageMarginRightCm { get; set; } = 2.6;
    public string FormatScheme { get; set; } = "标准公文格式";

    /// <summary>Pipeline 排版引擎模板 ID（official-report / meeting-minutes / inspection-report）</summary>
    public string PipelineTemplateId { get; set; } = "official-report";
}

public class DocumentToMarkdownPreviewSettings
{
    public bool ExtractImages { get; set; } = true;
    public bool PreserveTables { get; set; } = true;
    public bool SplitByPage { get; set; } = false;
    public bool EnableOcr { get; set; } = false;
}

public class FormatDocPreviewSettings
{
    public string TemplatePath { get; set; } = string.Empty;
    public string FormatScheme { get; set; } = "标准公文格式";
    public string FontFamily { get; set; } = "仿宋";
    public string TitleLevel { get; set; } = "三级标题";
    public string ParagraphIndent { get; set; } = "首行缩进 2 字符";
    public string PageMargin { get; set; } = "上 3.7 / 下 3.5 / 左右 2.8";
    public bool HeaderFooterEnabled { get; set; } = true;
    public string HeaderText { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;

    // --- 结构化字段（排版引擎实际读取） ---
    public string TitleFont { get; set; } = "方正小标宋简体";
    public string HeadingFont { get; set; } = "黑体";
    public string BodyFont { get; set; } = "仿宋_GB2312";
    public string SubheadingFont { get; set; } = "楷体_GB2312";
    public string CodeBlockFont { get; set; } = "Consolas";
    public double BodyFontSizePt { get; set; } = 16.0;
    public double TitleFontSizePt { get; set; } = 22.0;
    public double HeadingFontSizePt { get; set; } = 18.0;
    public double SubheadingFontSizePt { get; set; } = 16.0;
    public double CodeBlockFontSizePt { get; set; } = 10.5;
    public double LineSpacingPt { get; set; } = 28.0;
    public double FirstLineIndentChars { get; set; } = 2.0;
    public double PageMarginTopCm { get; set; } = 3.7;
    public double PageMarginBottomCm { get; set; } = 3.5;
    public double PageMarginLeftCm { get; set; } = 2.8;
    public double PageMarginRightCm { get; set; } = 2.6;
    public double BeforeSpacingPt { get; set; } = 0;
    public double AfterSpacingPt { get; set; } = 0;
}

public class RecentState
{
    public List<string> RecentFolders { get; set; } = new();
    public List<string> RecentOutputDirectories { get; set; } = new();

    /// <summary>F4: 最近转换记录（最近 20 条）。</summary>
    public List<ConversionRecord> RecentConversions { get; set; } = new();
}

/// <summary>
/// F4: 单次转换的历史记录条目，持久化于 RecentState。
/// </summary>
public class ConversionRecord
{
    /// <summary>转换完成时间</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>源文件完整路径</summary>
    public string SourceFilePath { get; set; } = string.Empty;

    /// <summary>源文件名</summary>
    public string SourceFileName { get; set; } = string.Empty;

    /// <summary>输出文件完整路径（失败时为空）</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>是否转换成功</summary>
    public bool Success { get; set; }

    /// <summary>失败时的错误信息</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>质量评分 0.0-1.0（不可用时为 0）</summary>
    public double QualityScore { get; set; }

    /// <summary>所属模式：ToMarkdown / MarkdownToDocx / FormatDoc</summary>
    public string Mode { get; set; } = string.Empty;
}
