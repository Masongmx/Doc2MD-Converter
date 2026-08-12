using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using Doc2MD.Models;
using Doc2MD.Pipeline.Services;
using Doc2MD.Services;
using Microsoft.Win32;

namespace Doc2MD.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ConversionService _conversionService;
    private readonly ConfigService _configService;
    private readonly ToastService _toastService;
    private readonly FileScanService _fileScanService;
    private readonly ILoggingService _logger;
    private readonly Dictionary<AppMode, ObservableCollection<FileItem>> _modeFiles;
    private readonly Dictionary<AppMode, string> _modeOutputDirectories;
    private readonly Dictionary<AppMode, string?> _modeCurrentFolders;
    private readonly Dictionary<AppMode, string?> _lastResolvedOutputDirectories;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _processCts;
    private int _scanVersion;

    private AppMode _currentMode = AppMode.ToMarkdown;
    private bool _isScanning;
    private bool _isProcessing;
    private bool _isDragActive;
    private bool _isSwitchingFolder;
    private bool _isModeTransitioning;
    private string _taskPanelTransitionPhase = "steady";
    private string _fileListTransitionPhase = "steady";
    private string _statusText = "就绪";
    private string _statusTone = "success";
    private string _scanStatusPrimary = "正在扫描文件夹...";
    private string _scanStatusSecondary = "正在识别文件和子目录";
    private int _scanFound;
    private int _scanSupported;
    private int _processCurrent;
    private int _processTotal;
    private string _toastMessage = string.Empty;
    private string _toastTone = "info";
    private bool _isToastVisible;
    private string _selectedSettingsSection = "通用设置";
    private string _selectedHelpSection = "使用指南";

    // F2: 转换预览状态
    private FileItem? _selectedFile;
    private bool _isPreviewVisible;
    private FlowDocument? _previewDocument;
    private string _previewFileName = string.Empty;

    public MainViewModel()
        : this(new ConfigService(), LoggingService.Logger, new ToastService(), new ConversionService())
    {
    }

    /// <summary>
    /// 注入构造函数（DI 迁移 C1）。允许通过 DI 容器注入配置、日志、Toast 与转换服务，
    /// 提升可测试性与可替换性。
    /// </summary>
    public MainViewModel(ConfigService configService, ILoggingService logger, ToastService toastService, ConversionService conversionService)
    {
        _configService = configService;
        _logger = logger;
        _conversionService = conversionService;
        _toastService = toastService;
        _toastService.Changed += () =>
        {
            ToastMessage = _toastService.Message;
            ToastTone = _toastService.Tone;
            IsToastVisible = _toastService.IsVisible;
        };
        _fileScanService = new FileScanService(_configService.Config);

        _modeFiles = new Dictionary<AppMode, ObservableCollection<FileItem>>
        {
            [AppMode.ToMarkdown] = new ObservableCollection<FileItem>(),
            [AppMode.MarkdownToDocx] = new ObservableCollection<FileItem>(),
            [AppMode.FormatDoc] = new ObservableCollection<FileItem>()
        };
        _modeOutputDirectories = new Dictionary<AppMode, string>
        {
            [AppMode.ToMarkdown] = _configService.Config.General.DefaultOutputDir,
            [AppMode.MarkdownToDocx] = _configService.Config.General.DefaultOutputDir,
            [AppMode.FormatDoc] = _configService.Config.General.DefaultOutputDir
        };
        _modeCurrentFolders = new Dictionary<AppMode, string?>
        {
            [AppMode.ToMarkdown] = null,
            [AppMode.MarkdownToDocx] = null,
            [AppMode.FormatDoc] = null
        };
        _lastResolvedOutputDirectories = new Dictionary<AppMode, string?>
        {
            [AppMode.ToMarkdown] = null,
            [AppMode.MarkdownToDocx] = null,
            [AppMode.FormatDoc] = null
        };

        foreach (var entry in _modeFiles)
        {
            entry.Value.CollectionChanged += (_, args) => OnModeCollectionChanged(entry.Key, args);
        }

        if (Application.Current is App app)
        {
            app.ApplyAppearanceSettings(Settings.Appearance);
        }

        _conversionService.FileCompleted += (_, _) => RefreshFileSummaryProperties();

        // 初始化语言并订阅切换事件
        LanguageService.SetLanguage(Settings.General.Language);
        LanguageService.LanguageChanged += OnLanguageChanged;

        LoggingService.Info("主界面已初始化");
    }

    private void OnLanguageChanged()
    {
        // 语言切换后刷新所有依赖本地化文本的属性
        RaiseModeDependentProperties();
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(ProcessProgressText));
        OnPropertyChanged(nameof(CurrentActionVerb));
        RefreshFileSummaryProperties();
        RaiseOnboardingProperties();
    }

    private void RaiseOnboardingProperties()
    {
        OnPropertyChanged(nameof(OnboardingTitle));
        OnPropertyChanged(nameof(OnboardingSubtitle));
        OnPropertyChanged(nameof(OnboardingStep1Title));
        OnPropertyChanged(nameof(OnboardingStep1Desc));
        OnPropertyChanged(nameof(OnboardingStep2Title));
        OnPropertyChanged(nameof(OnboardingStep2Desc));
        OnPropertyChanged(nameof(OnboardingStep3Title));
        OnPropertyChanged(nameof(OnboardingStep3Desc));
        OnPropertyChanged(nameof(OnboardingStartText));
        OnPropertyChanged(nameof(OnboardingSkipText));
    }

    /// <summary>
    /// 检测配置加载状态，若配置损坏已重置则提示用户。
    /// 由窗口 Loaded 事件调用，确保 UI 已就绪。
    /// </summary>
    public void NotifyIfConfigCorrupted()
    {
        if (_configService.WasLoadCorrupted)
        {
            ShowToast(LanguageService.GetString("Toast_ConfigCorrupted"), ToastType.Warning);
        }
    }

    /// <summary>向用户展示一条成功提示（供视图层事件复用，如复制错误信息）。</summary>
    public void ShowToastFeedback(string message)
    {
        ShowToast(message, ToastType.Success);
    }

    // ===== F2: 转换预览交互 =====

    /// <summary>切换预览面板显示状态。预览打开时渲染当前选中文件。</summary>
    public async Task TogglePreviewAsync()
    {
        if (IsPreviewVisible)
        {
            ClosePreview();
            return;
        }

        IsPreviewVisible = true;
        var file = SelectedFile;
        if (file != null && !IsProcessing && !IsScanning)
        {
            await RenderPreviewAsync(file);
        }
        else
        {
            PreviewDocument = null;
            _previewFileName = string.Empty;
            OnPropertyChanged(nameof(PreviewPanelTitle));
        }
    }

    /// <summary>关闭预览面板并清空渲染结果。</summary>
    public void ClosePreview()
    {
        if (!IsPreviewVisible && PreviewDocument == null)
        {
            return;
        }
        IsPreviewVisible = false;
        PreviewDocument = null;
        _previewFileName = string.Empty;
        OnPropertyChanged(nameof(PreviewPanelTitle));
    }

    private async Task RenderPreviewAsync(FileItem file)
    {
        if (file.Type != FileType.Markdown)
        {
            ShowToast(LanguageService.GetString("Preview_NotMarkdown"), ToastType.Warning);
            return;
        }

        if (!File.Exists(file.FullPath))
        {
            ShowToast(LanguageService.GetString("Preview_FileMissing"), ToastType.Warning);
            return;
        }

        try
        {
            // 文件读取与语义解析在后台线程执行；FlowDocument 必须留在 UI 线程构建
            var markdown = await Task.Run(() => File.ReadAllText(file.FullPath));
            var semantic = await Task.Run(() => SemanticDocumentConverter.Convert(markdown));
            PreviewDocument = MarkdownPreviewBuilder.Build(semantic);
            _previewFileName = file.FileName;
            OnPropertyChanged(nameof(PreviewPanelTitle));
        }
        catch (Exception ex)
        {
            _logger.Error($"预览渲染失败: {file.FullPath}", ex);
            PreviewDocument = null;
            _previewFileName = string.Empty;
            OnPropertyChanged(nameof(PreviewPanelTitle));
            ShowToast(LanguageService.GetString("Preview_RenderFailed"), ToastType.Error);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppConfig Settings => _configService.Config;

    /// <summary>F4: 最近转换历史记录（最近 20 条）。</summary>
    public IReadOnlyList<ConversionRecord> RecentConversions =>
        _configService.Config.Recent.RecentConversions;

    /// <summary>True when MotionLevel is Off — all animations should be suppressed.</summary>
    public bool IsMotionOff => Settings.Appearance.Motion == MotionLevel.Off;

    /// <summary>True when MotionLevel is Smooth — enables extra polish animations.</summary>
    public bool IsMotionSmooth => Settings.Appearance.Motion == MotionLevel.Smooth;
    public string AppVersion => Constants.AppVersion.FullString;

    public AppMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (_currentMode == value) return;
            _currentMode = value;
            OnPropertyChanged();
            ClosePreview();
            RaiseModeDependentProperties();
        }
    }

    public int SelectedModeIndex => CurrentMode switch
    {
        AppMode.MarkdownToDocx => 1,
        AppMode.FormatDoc => 2,
        _ => 0
    };

    public ObservableCollection<FileItem> ActiveFiles => _modeFiles[CurrentMode];

    // ===== F2: 转换预览 =====

    /// <summary>文件列表中当前选中的文件（绑定 ListView.SelectedItem）。</summary>
    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (ReferenceEquals(_selectedFile, value)) return;
            _selectedFile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanPreview));
            if (_isPreviewVisible && value != null && !IsProcessing && !IsScanning)
            {
                _ = RenderPreviewAsync(value);
            }
        }
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        private set
        {
            if (_isPreviewVisible == value) return;
            _isPreviewVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanClosePreview));
        }
    }

    /// <summary>预览面板渲染出的文档（须在 UI 线程赋值）。</summary>
    public FlowDocument? PreviewDocument
    {
        get => _previewDocument;
        private set
        {
            if (ReferenceEquals(_previewDocument, value)) return;
            _previewDocument = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreviewDocument));
        }
    }

    public bool HasPreviewDocument => PreviewDocument != null;
    public bool IsPreviewButtonVisible => CurrentMode == AppMode.MarkdownToDocx;
    public bool CanClosePreview => IsPreviewVisible;
    public bool CanPreview => IsPreviewButtonVisible && SelectedFile != null && !IsProcessing && !IsScanning;
    public string PreviewButtonText => LanguageService.GetString("Button_Preview");
    public string PreviewCloseText => LanguageService.GetString("Button_Close");
    public string PreviewEmptyText => LanguageService.GetString("Preview_Empty");
    public string PreviewPanelTitle => string.IsNullOrEmpty(_previewFileName)
        ? LanguageService.GetString("Preview_PanelTitle")
        : string.Format(LanguageService.CurrentCulture, "{0} - {1}", LanguageService.GetString("Preview_PanelTitle"), _previewFileName);
    public string CurrentModeTitle => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("Mode_ToDocx_Title"),
        AppMode.FormatDoc => LanguageService.GetString("Mode_Format_Title"),
        _ => LanguageService.GetString("Mode_ToMarkdown_Title")
    };
    public string CurrentModeDescription => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("Mode_ToDocx_Desc"),
        AppMode.FormatDoc => LanguageService.GetString("Mode_Format_Desc"),
        _ => LanguageService.GetString("Mode_ToMarkdown_Desc")
    };
    public string CurrentModeDetail => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("Mode_ToDocx_Detail"),
        AppMode.FormatDoc => LanguageService.GetString("Mode_Format_Detail"),
        _ => LanguageService.GetString("Mode_ToMarkdown_Detail")
    };
    public string AddFileButtonText => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("Button_AddMarkdown"),
        AppMode.FormatDoc => LanguageService.GetString("Button_AddWord"),
        _ => LanguageService.GetString("Button_AddFiles")
    };
    public string DropZoneTitle => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("DropZone_ToDocx_Title"),
        AppMode.FormatDoc => LanguageService.GetString("DropZone_Format_Title"),
        _ => LanguageService.GetString("DropZone_ToMarkdown_Title")
    };
    public string DropZoneSubtitle => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("DropZone_ToDocx_Subtitle"),
        AppMode.FormatDoc => LanguageService.GetString("DropZone_Format_Subtitle"),
        _ => LanguageService.GetString("DropZone_ToMarkdown_Subtitle")
    };
    public string DropZoneFootnote => LanguageService.GetString("DropZone_Footnote");
    public string DragActiveText => LanguageService.GetString("DropZone_DragActive");
    public double UiScale => Settings.Appearance.Scale <= 0 ? 1.0 : Settings.Appearance.Scale;
    public string ActiveOutputDirectory
    {
        get => _modeOutputDirectories[CurrentMode];
        set
        {
            if (_modeOutputDirectories[CurrentMode] == value) return;
            _modeOutputDirectories[CurrentMode] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedOutputDirectory));
            OnPropertyChanged(nameof(CanOpenOutputDirectory));
            OnPropertyChanged(nameof(CanPrimaryAction));
            OnPropertyChanged(nameof(OutputDirectoryDisplay));
        }
    }
    public string OutputDirectoryDisplay => ActiveOutputDirectory;
    public bool HasSelectedOutputDirectory => !string.IsNullOrWhiteSpace(ActiveOutputDirectory);
    public string? CurrentFolder => _modeCurrentFolders[CurrentMode];
    public bool HasCurrentFolder => !string.IsNullOrWhiteSpace(CurrentFolder);
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (_isScanning == value) return;
            _isScanning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionLocked));
            OnPropertyChanged(nameof(CanStartProcessing));
            OnPropertyChanged(nameof(CanPrimaryAction));
            OnPropertyChanged(nameof(CanChangeMode));
            OnPropertyChanged(nameof(CanClearFiles));
            OnPropertyChanged(nameof(CanUseFolderActions));
            OnPropertyChanged(nameof(CanPreview));
        }
    }
    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (_isProcessing == value) return;
            _isProcessing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionLocked));
            OnPropertyChanged(nameof(CanStartProcessing));
            OnPropertyChanged(nameof(CanPrimaryAction));
            OnPropertyChanged(nameof(CanChangeMode));
            OnPropertyChanged(nameof(CanClearFiles));
            OnPropertyChanged(nameof(CanUseFolderActions));
            OnPropertyChanged(nameof(PrimaryActionText));
            OnPropertyChanged(nameof(CanPreview));
        }
    }
    public bool IsDragActive
    {
        get => _isDragActive;
        set
        {
            if (_isDragActive == value) return;
            _isDragActive = value;
            OnPropertyChanged();
        }
    }
    public bool IsSwitchingFolder
    {
        get => _isSwitchingFolder;
        private set
        {
            if (_isSwitchingFolder == value) return;
            _isSwitchingFolder = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionLocked));
            OnPropertyChanged(nameof(CanStartProcessing));
            OnPropertyChanged(nameof(CanPrimaryAction));
            OnPropertyChanged(nameof(CanChangeMode));
            OnPropertyChanged(nameof(CanClearFiles));
            OnPropertyChanged(nameof(CanUseFolderActions));
        }
    }
    public bool IsModeTransitioning
    {
        get => _isModeTransitioning;
        private set
        {
            if (_isModeTransitioning == value) return;
            _isModeTransitioning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInteractionLocked));
            OnPropertyChanged(nameof(CanChangeMode));
        }
    }
    public bool IsInteractionLocked => IsScanning || IsProcessing || IsSwitchingFolder || IsModeTransitioning;
    public bool CanChangeMode => !IsInteractionLocked;
    public bool CanUseFolderActions => HasCurrentFolder && !IsInteractionLocked;
    public bool CanClearFiles => ActiveFiles.Count > 0 && !IsInteractionLocked;
    public bool CanOpenOutputDirectory => !IsInteractionLocked && Directory.Exists(GetOpenableOutputDirectory());
    public bool CanStartProcessing => !IsInteractionLocked && ActiveFiles.Any(IsRunnableFile);
    public bool CanPrimaryAction => CanStartProcessing || (!IsInteractionLocked && IsPrimaryOpenOutputAction && CanOpenOutputDirectory);

    /// <summary>主按钮当前是否为"打开输出目录"动作（用于与纯生成动作区分）。</summary>
    public bool IsPrimaryOpenOutputAction =>
        !IsProcessing &&
        !IsScanning &&
        !IsSwitchingFolder &&
        ActiveFiles.Count > 0 &&
        ActiveFiles.All(file => !FileScanService.IsSupportedForMode(file.FullPath, CurrentMode) || file.Status == FileStatus.Done) &&
        ActiveFiles.Any(file => file.Status == FileStatus.Done);
    public string TaskPanelTransitionPhase
    {
        get => _taskPanelTransitionPhase;
        private set
        {
            if (_taskPanelTransitionPhase == value) return;
            _taskPanelTransitionPhase = value;
            OnPropertyChanged();
        }
    }
    public string FileListTransitionPhase
    {
        get => _fileListTransitionPhase;
        private set
        {
            if (_fileListTransitionPhase == value) return;
            _fileListTransitionPhase = value;
            OnPropertyChanged();
        }
    }
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }
    public string StatusTone
    {
        get => _statusTone;
        private set
        {
            if (_statusTone == value) return;
            _statusTone = value;
            OnPropertyChanged();
        }
    }
    public string ScanStatusPrimary
    {
        get => _scanStatusPrimary;
        private set
        {
            if (_scanStatusPrimary == value) return;
            _scanStatusPrimary = value;
            OnPropertyChanged();
        }
    }
    public string ScanStatusSecondary
    {
        get => _scanStatusSecondary;
        private set
        {
            if (_scanStatusSecondary == value) return;
            _scanStatusSecondary = value;
            OnPropertyChanged();
        }
    }
    public int ScanFound
    {
        get => _scanFound;
        private set
        {
            if (_scanFound == value) return;
            _scanFound = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentFolderSummaryText));
        }
    }
    public int ScanSupported
    {
        get => _scanSupported;
        private set
        {
            if (_scanSupported == value) return;
            _scanSupported = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentFolderSummaryText));
        }
    }
    public int ProcessCurrent
    {
        get => _processCurrent;
        private set
        {
            if (_processCurrent == value) return;
            _processCurrent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessProgressText));
            OnPropertyChanged(nameof(ProcessProgressPercent));
            OnPropertyChanged(nameof(ProcessProgressPercentText));
            OnPropertyChanged(nameof(PrimaryActionText));
        }
    }
    public int ProcessTotal
    {
        get => _processTotal;
        private set
        {
            if (_processTotal == value) return;
            _processTotal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessProgressText));
            OnPropertyChanged(nameof(ProcessProgressPercent));
            OnPropertyChanged(nameof(ProcessProgressPercentText));
            OnPropertyChanged(nameof(ProcessProgressMaximum));
            OnPropertyChanged(nameof(PrimaryActionText));
        }
    }
    public int ProcessProgressMaximum => ProcessTotal <= 0 ? 1 : ProcessTotal;
    public double ProcessProgressPercent => ProcessTotal == 0 ? 0 : ProcessCurrent * 100d / ProcessTotal;
    public string ProcessProgressText
    {
        get
        {
            if (IsProcessing)
            {
                return LanguageService.GetFormatted("Progress_Processing", CurrentActionVerb, ProcessCurrent, ProcessTotal);
            }

            if (ProcessTotal > 0)
            {
                if (FailedCount > 0)
                {
                    return LanguageService.GetFormatted("Progress_PartiallyFailed", DoneCount, FailedCount);
                }

                return LanguageService.GetFormatted("Progress_Completed", ProcessCurrent, ProcessTotal);
            }

            return LanguageService.GetString("Progress_NoTasks");
        }
    }
    public string ProcessProgressPercentText => ProcessTotal <= 0 ? string.Empty : $"{Math.Round(ProcessProgressPercent):0}%";
    public string CurrentActionVerb => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("Action_Verb_ToDocx"),
        AppMode.FormatDoc => LanguageService.GetString("Action_Verb_Format"),
        _ => LanguageService.GetString("Action_Verb_ToMarkdown")
    };
    public string PrimaryActionText
    {
        get
        {
            if (IsProcessing)
            {
                return LanguageService.GetFormatted("Progress_Processing", " ", ProcessCurrent, ProcessTotal).Trim();
            }

            // 存在失败项且没有真正待处理的新任务时：主按钮变为"重试失败"
            var failedCount = FailedCount;
            var hasPendingNew = PendingCount > 0;
            if (!hasPendingNew && failedCount > 0)
            {
                return LanguageService.GetFormatted("Button_RetryFailed", failedCount);
            }

            if (ActiveFiles.Count > 0 &&
                ActiveFiles.All(file => !FileScanService.IsSupportedForMode(file.FullPath, CurrentMode) || file.Status == FileStatus.Done) &&
                ActiveFiles.Any(file => file.Status == FileStatus.Done))
            {
                return LanguageService.GetString("Button_OpenOutputDir");
            }

            return CurrentMode switch
            {
                AppMode.MarkdownToDocx => LanguageService.GetString("Button_GenerateDocx"),
                AppMode.FormatDoc => LanguageService.GetString("Button_StartFormat"),
                _ => LanguageService.GetString("Button_GenerateMarkdown")
            };
        }
    }
    public string CurrentFolderSummaryText
    {
        get
        {
            if (HasCurrentFolder)
            {
                return LanguageService.GetFormatted("FolderSummary_Current", CurrentFolder ?? string.Empty, ScanFound, ScanSupported);
            }

            return ActiveFiles.Count == 0
                ? string.Empty
                : LanguageService.GetFormatted("FolderSummary_Added", ActiveFiles.Count);
        }
    }
    public bool HasCurrentFolderSummary => HasCurrentFolder || ActiveFiles.Count > 0;
    public int PendingCount => ActiveFiles.Count(file => file.Status == FileStatus.Pending);
    public int ProcessingCount => ActiveFiles.Count(file => file.Status == FileStatus.Processing);
    public int DoneCount => ActiveFiles.Count(file => file.Status == FileStatus.Done);
    public int FailedCount => ActiveFiles.Count(file => file.Status == FileStatus.Failed);
    public int UnsupportedCount => ActiveFiles.Count(file => file.Status == FileStatus.Unsupported);
    public string ToastMessage
    {
        get => _toastMessage;
        private set
        {
            if (_toastMessage == value) return;
            _toastMessage = value;
            OnPropertyChanged();
        }
    }
    public string ToastTone
    {
        get => _toastTone;
        private set
        {
            if (_toastTone == value) return;
            _toastTone = value;
            OnPropertyChanged();
        }
    }
    public bool IsToastVisible
    {
        get => _isToastVisible;
        private set
        {
            if (_isToastVisible == value) return;
            _isToastVisible = value;
            OnPropertyChanged();
        }
    }
    public string SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        set
        {
            if (_selectedSettingsSection == value) return;
            _selectedSettingsSection = value;
            OnPropertyChanged();
        }
    }
    public string SelectedHelpSection
    {
        get => _selectedHelpSection;
        set
        {
            if (_selectedHelpSection == value) return;
            _selectedHelpSection = value;
            OnPropertyChanged();
        }
    }

    /// <summary>首次启动时是否显示新手引导浮层（P1）。</summary>
    public bool ShowOnboarding => !Settings.General.HasCompletedOnboarding;

    // ==== 新手引导本地化文本（P1） ====
    public string OnboardingTitle => LanguageService.GetString("Onboarding_Title");
    public string OnboardingSubtitle => LanguageService.GetString("Onboarding_Subtitle");
    public string OnboardingStep1Title => LanguageService.GetString("Onboarding_Step1_Title");
    public string OnboardingStep1Desc => LanguageService.GetString("Onboarding_Step1_Desc");
    public string OnboardingStep2Title => LanguageService.GetString("Onboarding_Step2_Title");
    public string OnboardingStep2Desc => LanguageService.GetString("Onboarding_Step2_Desc");
    public string OnboardingStep3Title => LanguageService.GetString("Onboarding_Step3_Title");
    public string OnboardingStep3Desc => LanguageService.GetString("Onboarding_Step3_Desc");
    public string OnboardingStartText => LanguageService.GetString("Onboarding_Start");
    public string OnboardingSkipText => LanguageService.GetString("Onboarding_Skip");

    /// <summary>完成新手引导，持久化标记并关闭浮层。</summary>
    public void CompleteOnboarding()
    {
        if (Settings.General.HasCompletedOnboarding) return;
        Settings.General.HasCompletedOnboarding = true;
        _configService.Save();
        OnPropertyChanged(nameof(ShowOnboarding));
    }

    public async Task SwitchModeAsync(AppMode mode)
    {
        if (CurrentMode == mode || IsInteractionLocked) return;

        _logger.Info($"切换模式: {CurrentMode} -> {mode}");
        IsModeTransitioning = true;

        await AnimateTaskPanelOutAsync();
        CurrentMode = mode;
        await AnimateTaskPanelInAsync();

        IsModeTransitioning = false;
    }

    public async Task HandleDroppedPathsAsync(string[] droppedPaths)
    {
        var directories = droppedPaths.Where(Directory.Exists).ToArray();
        if (directories.Length > 0)
        {
            await SwitchFolderAsync(directories[0], isRefresh: false);
            return;
        }

        await AddFilesAsync(droppedPaths);
    }

    public async Task BrowseFilesAsync()
    {
        var allFiles = LanguageService.GetString("Filter_AllFiles");
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = CurrentMode switch
            {
                AppMode.MarkdownToDocx => $"{LanguageService.GetString("Filter_Markdown")}|*.md;*.markdown|{allFiles}|*.*",
                AppMode.FormatDoc => $"{LanguageService.GetString("Filter_Word")}|*.doc;*.docx|{allFiles}|*.*",
                _ => $"{LanguageService.GetString("Filter_SupportedDocs")}|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx|{allFiles}|*.*"
            }
        };

        if (dialog.ShowDialog() == true)
        {
            await AddFilesAsync(dialog.FileNames);
        }
    }

    public async Task BrowseFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = LanguageService.GetString("Dialog_FolderTitle")
        };

        if (dialog.ShowDialog() == true)
        {
            await SwitchFolderAsync(dialog.FolderName, isRefresh: false);
        }
    }

    public async Task RefreshFolderAsync()
    {
        if (!HasCurrentFolder || CurrentFolder == null) return;
        await SwitchFolderAsync(CurrentFolder, isRefresh: true);
    }

    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        var candidates = filePaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (candidates.Count == 0) return;

        ClearCurrentFolderContext();

        var existingPaths = new HashSet<string>(ActiveFiles.Select(file => file.FullPath), StringComparer.OrdinalIgnoreCase);
        var added = new List<FileItem>();
        var skipped = 0;
        var unsupported = 0;
        var legacyCount = 0;

        foreach (var path in candidates)
        {
            if (!existingPaths.Add(path))
            {
                skipped++;
                continue;
            }

            var item = CreateFileItem(path);
            if (FileScanService.IsSupportedForMode(path, CurrentMode))
            {
                item.Status = FileStatus.Pending;
                added.Add(item);

                // 检测旧格式文件（.doc/.xls/.ppt），需要 LibreOffice 兜底
                if (LegacyOfficeConverter.IsLegacyOfficeFormat(item.Extension))
                {
                    legacyCount++;
                }
            }
            else
            {
                item.Status = FileStatus.Unsupported;
                added.Add(item);
                unsupported++;
            }
        }

        foreach (var item in added)
        {
            ActiveFiles.Add(item);
        }

        StatusText = added.Count > 0 ? LanguageService.GetFormatted("Add_StatusAdded", added.Count) : LanguageService.GetString("Status_NoNewFiles");
        StatusTone = unsupported > 0 ? "warning" : "success";
        _logger.Info($"手动添加文件: {added.Count}，跳过重复: {skipped}，不支持: {unsupported}");

        if (added.Count > 0)
        {
            ShowToast(LanguageService.GetFormatted("Toast_AddedFiles", added.Count), unsupported > 0 ? ToastType.Warning : ToastType.Success);
        }

        if (skipped > 0)
        {
            ShowToast(LanguageService.GetFormatted("Toast_SkippedDuplicates", skipped), ToastType.Info);
        }

        if (unsupported > 0)
        {
            ShowToast(LanguageService.GetFormatted("Toast_UnsupportedRetained", unsupported), ToastType.Warning);
        }

        // 检测到旧格式文件但未安装 LibreOffice 时警告用户
        if (legacyCount > 0 && !LegacyOfficeConverter.IsLibreOfficeAvailable())
        {
            ShowToast(LanguageService.GetFormatted("Toast_LegacyNeedLibreOffice", legacyCount), ToastType.Warning);
        }

        RefreshFileSummaryProperties();
    }

    // F5/P4: 清空前的快照与 3 秒撤销窗口，供 Ctrl+Z 或 Toast 撤销按钮恢复
    private List<FileItem>? _clearSnapshot;
    private CancellationTokenSource? _undoCts;

    /// <summary>P4: 是否处于 3 秒撤销窗口内（快照未过期）。</summary>
    public bool CanUndoClear => _clearSnapshot is { Count: > 0 };

    public void ClearFiles()
    {
        if (ActiveFiles.Count == 0) return;

        // P4: 保存快照并打开 3 秒撤销窗口
        _clearSnapshot = new List<FileItem>(ActiveFiles);
        OnPropertyChanged(nameof(CanUndoClear));
        StartUndoWindow();

        var cleared = ActiveFiles.Count;
        ActiveFiles.Clear();
        _modeCurrentFolders[CurrentMode] = null;
        ScanFound = 0;
        ScanSupported = 0;
        StatusText = LanguageService.GetString("Status_ListCleared");
        StatusTone = "success";
        LoggingService.Info($"清空文件列表: {CurrentMode}");
        ShowToast(LanguageService.GetFormatted("Toast_ListClearedUndo", cleared), ToastType.Info);
        RefreshFileSummaryProperties();
    }

    /// <summary>F5: 撤销最近一次清空文件列表操作（Ctrl+Z 或 Toast 撤销按钮）。</summary>
    public void UndoClearFiles()
    {
        if (_clearSnapshot == null || _clearSnapshot.Count == 0)
        {
            ShowToast(LanguageService.GetString("Toast_NothingToUndo"), ToastType.Info);
            return;
        }

        _undoCts?.Cancel();
        var restored = _clearSnapshot;
        _clearSnapshot = null;
        OnPropertyChanged(nameof(CanUndoClear));

        foreach (var item in restored)
        {
            ActiveFiles.Add(item);
        }
        StatusText = LanguageService.GetString("Status_ListRestored");
        StatusTone = "success";
        LoggingService.Info($"已撤销清空文件列表: {CurrentMode}");
        ShowToast(LanguageService.GetString("Toast_ListRestored"), ToastType.Success);
        RefreshFileSummaryProperties();
    }

    /// <summary>P4: 3 秒后自动关闭撤销窗口，防止误操作无限期撤销。</summary>
    private void StartUndoWindow()
    {
        _undoCts?.Cancel();
        _undoCts?.Dispose();
        _undoCts = new CancellationTokenSource();
        var token = _undoCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000, token);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    _clearSnapshot = null;
                    OnPropertyChanged(nameof(CanUndoClear));
                });
            }
            catch (OperationCanceledException)
            {
                // 已撤销或再次清空时主动取消，忽略
            }
        }, token);
    }

    public void RemoveFile(FileItem item)
    {
        ActiveFiles.Remove(item);
        _logger.Info($"移除文件: {item.FullPath}");
        RefreshFileSummaryProperties();
    }

    // P5: 文件列表是否有选中项（驱动「移除选中」按钮显隐）
    private bool _hasSelection;

    public bool HasSelection
    {
        get => _hasSelection;
        set
        {
            if (_hasSelection == value) return;
            _hasSelection = value;
            OnPropertyChanged();
        }
    }

    /// <summary>P5: 批量移除选中的文件（ListView SelectionMode=Extended 多选）。</summary>
    public void RemoveSelectedFiles(IReadOnlyCollection<FileItem> items)
    {
        if (items == null || items.Count == 0) return;

        foreach (var item in items)
        {
            ActiveFiles.Remove(item);
        }
        _logger.Info($"批量移除 {items.Count} 个文件");
        ShowToast(LanguageService.GetFormatted("Toast_RemovedFiles", items.Count), ToastType.Info);
        RefreshFileSummaryProperties();
        OnPropertyChanged(nameof(SelectedFile));
    }

    public async Task HandleFileActionAsync(FileItem item)
    {
        switch (item.Status)
        {
            case FileStatus.Done:
                // 成功项：优先打开输出文件，否则打开所在目录
                if (!string.IsNullOrWhiteSpace(item.OutputPath))
                {
                    OpenPath(item.OutputPath);
                }
                else
                {
                    OpenPath(item.Directory);
                }
                break;
            case FileStatus.Processing:
                CancelProcessing();
                break;
            case FileStatus.Failed:
                item.Status = FileStatus.Pending;
                item.ErrorMessage = null;
                ShowToast(LanguageService.GetFormatted("Toast_MarkedForRetry", item.FileName), ToastType.Info);
                RefreshFileSummaryProperties();
                break;
            case FileStatus.Unsupported:
            case FileStatus.Skipped:
            case FileStatus.Pending:
                RemoveFile(item);
                break;
        }

        await Task.CompletedTask;
    }

    /// <summary>复制指定文件项的输出路径（F3：输出管理快捷入口）。</summary>
    public void CopyOutputPath(FileItem item)
    {
        var path = !string.IsNullOrWhiteSpace(item.OutputPath) ? item.OutputPath : item.FullPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            System.Windows.Clipboard.SetText(path);
            ShowToast(LanguageService.GetString("Toast_CopiedOutputPath"), ToastType.Success);
        }
        catch (Exception ex)
        {
            _logger.Error($"复制输出路径失败: {ex.Message}");
            ShowToast(LanguageService.GetString("Toast_CopyFailed"), ToastType.Error);
        }
    }

    public async Task StartProcessingAsync()
    {
        if (!IsProcessing && IsPrimaryOpenOutputAction && CanOpenOutputDirectory && !CanStartProcessing)
        {
            OpenOutputDirectory();
            return;
        }

        if (IsProcessing)
        {
            OpenOutputDirectory();
            return;
        }

        var runnableFiles = ActiveFiles
            .Where(IsRunnableFile)
            .ToList();

        if (runnableFiles.Count == 0)
        {
            ShowToast(LanguageService.GetString("Toast_NoProcessableFiles"), ToastType.Warning);
            return;
        }

        var outputDirectory = await EnsureOutputDirectoryAsync(runnableFiles);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        _lastResolvedOutputDirectories[CurrentMode] = outputDirectory;
        OnPropertyChanged(nameof(CanOpenOutputDirectory));

        ProcessCurrent = 0;
        ProcessTotal = runnableFiles.Count;
        IsProcessing = true;
        StatusText = LanguageService.GetString("Status_Generating");
        StatusTone = "info";
        _logger.Info($"开始处理任务: 模式={CurrentMode}, 文件数={runnableFiles.Count}, 输出目录={outputDirectory}");

        _processCts?.Cancel();
        _processCts?.Dispose();
        _processCts = new CancellationTokenSource();
        var token = _processCts.Token;

        var inputRoot = Settings.Conversion.PreserveFolderStructure
            ? GetCommonInputRoot(runnableFiles)
            : null;

        var failedPaths = new List<string>();
        var semaphore = new SemaphoreSlim(Math.Max(1, Settings.Conversion.MaxConcurrentTasks));
        var tasks = runnableFiles.Select(async file =>
        {
            await semaphore.WaitAsync(token);
            try
            {
                await RunSingleFileAsync(file, outputDirectory, inputRoot, token);
                if (file.Status == FileStatus.Failed)
                {
                    lock (failedPaths)
                    {
                        failedPaths.Add(file.FullPath);
                    }

                    if (!Settings.Conversion.ContinueOnError)
                    {
                        _processCts?.Cancel();
                    }
                }
            }
            finally
            {
                semaphore.Release();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProcessCurrent++;
                    RefreshFileSummaryProperties();
                });
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            StatusText = failedPaths.Count > 0 ? LanguageService.GetString("Status_Interrupted") : LanguageService.GetString("Status_Cancelled");
            StatusTone = failedPaths.Count > 0 ? "warning" : "info";
        }
        finally
        {
            IsProcessing = false;
            semaphore.Dispose();
            _processCts?.Dispose();
            _processCts = null;
        }

        var failedCount = ActiveFiles.Count(file => file.Status == FileStatus.Failed);
        if (failedCount > 0)
        {
            StatusText = LanguageService.GetString("Status_PartiallyFailed");
            StatusTone = "warning";
            ShowToast(LanguageService.GetString("Toast_PartiallyFailed"), ToastType.Error);
        }
        else if (ActiveFiles.Any(file => file.Status == FileStatus.Done))
        {
            StatusText = LanguageService.GetString("Status_Generated");
            StatusTone = "success";
            ShowToast(LanguageService.GetString("Toast_ConversionDone"), ToastType.Success);

            if (Settings.General.AutoOpenOutputDir)
            {
                OpenOutputDirectory();
            }
        }
        else
        {
            StatusText = LanguageService.GetString("Status_Cancelled");
            StatusTone = "info";
        }
    }

    public void CancelProcessing()
    {
        if (IsProcessing)
        {
            _processCts?.Cancel();
            StatusText = LanguageService.GetString("Status_Cancelling");
            StatusTone = "info";
            _logger.Warning("用户取消当前处理任务");
        }

        if (IsScanning)
        {
            _scanCts?.Cancel();
            IsScanning = false;
            IsSwitchingFolder = false;
            FileListTransitionPhase = "steady";
            StatusText = LanguageService.GetString("Status_ScanCancelled");
            StatusTone = "warning";
            _logger.Warning("用户取消文件夹扫描");
        }
    }

    public void BrowseOutputDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = LanguageService.GetString("Dialog_OutputDirTitle")
        };

        if (dialog.ShowDialog() == true)
        {
            ActiveOutputDirectory = dialog.FolderName;
            _configService.RememberRecentOutputDirectory(dialog.FolderName);
            ShowToast(LanguageService.GetString("Toast_OutputDirUpdated"), ToastType.Success);
        }
    }

    public void OpenOutputDirectory()
    {
        OpenPath(GetOpenableOutputDirectory());
    }

    public void OpenCurrentFolder()
    {
        if (CurrentFolder != null)
        {
            OpenPath(CurrentFolder);
        }
    }

    public void OpenLogsDirectory()
    {
        Directory.CreateDirectory(AppPaths.LogDirectory);
        OpenPath(AppPaths.LogDirectory);
    }

    public void CopySoftwareInfo()
    {
        var info = LanguageService.GetFormatted(
            "SoftwareInfo_Template",
            AppVersion,
            Environment.NewLine,
            AppPaths.LogDirectory);
        Clipboard.SetText(info);
        ShowToast(LanguageService.GetString("Toast_CopySoftwareInfo"), ToastType.Success);
    }

    public void PersistSettings(string? successMessage = null)
    {
        _configService.Save();
        _modeOutputDirectories[AppMode.ToMarkdown] = string.IsNullOrWhiteSpace(_modeOutputDirectories[AppMode.ToMarkdown])
            ? Settings.General.DefaultOutputDir
            : _modeOutputDirectories[AppMode.ToMarkdown];
        _modeOutputDirectories[AppMode.MarkdownToDocx] = string.IsNullOrWhiteSpace(_modeOutputDirectories[AppMode.MarkdownToDocx])
            ? Settings.General.DefaultOutputDir
            : _modeOutputDirectories[AppMode.MarkdownToDocx];
        _modeOutputDirectories[AppMode.FormatDoc] = string.IsNullOrWhiteSpace(_modeOutputDirectories[AppMode.FormatDoc])
            ? Settings.General.DefaultOutputDir
            : _modeOutputDirectories[AppMode.FormatDoc];
        if (Application.Current is App app)
        {
            app.ApplyAppearanceSettings(Settings.Appearance);
        }

        // 同步语言设置到 LanguageService（仅当语言已变更时触发刷新）
        LanguageService.SetLanguage(Settings.General.Language);

        _logger.Info("设置已保存");
        ShowToast(successMessage ?? LanguageService.GetString("Toast_SettingsSaved"), ToastType.Success);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(UiScale));
        OnPropertyChanged(nameof(IsMotionOff));
        OnPropertyChanged(nameof(IsMotionSmooth));
        RaiseModeDependentProperties();
    }

    public void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(Settings));
    }

    public void ReloadSettings()
    {
        _configService.Reload();
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(UiScale));
        RaiseModeDependentProperties();
    }

    // F6: 更新检查互斥锁，防止重复点击并发请求
    private bool _isCheckingForUpdate;

    /// <summary>F6: 轮询 GitHub Releases API 检查新版本；有新版本时询问并跳转下载页。</summary>
    public async void CheckForUpdates()
    {
        if (_isCheckingForUpdate) return;
        _isCheckingForUpdate = true;

        try
        {
            ShowToast(LanguageService.GetString("Toast_CheckingUpdate"), ToastType.Info);
            var result = await Task.Run(() => new UpdateService().CheckForUpdateAsync());

            if (!result.Succeeded)
            {
                ShowToast(LanguageService.GetString("Toast_UpdateCheckFailed"), ToastType.Error);
                return;
            }

            if (!result.IsUpdateAvailable)
            {
                var message = LanguageService.GetFormatted("Toast_UpToDate", result.LatestVersion);
                ShowToast(message, ToastType.Success);
                return;
            }

            // 有新版本：询问是否跳转下载（优先直链，其次 Release 页）
            var targetUrl = !string.IsNullOrWhiteSpace(result.DownloadUrl)
                ? result.DownloadUrl
                : result.ReleaseUrl;
            var dialog = LanguageService.GetFormatted(
                "Dialog_UpdateAvailable",
                result.LatestVersion,
                result.CurrentVersion,
                targetUrl);

            if (MessageBox.Show(
                    dialog,
                    LanguageService.GetString("Dialog_UpdateTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error("[Update] 检查更新异常", ex);
            ShowToast(LanguageService.GetString("Toast_UpdateCheckFailed"), ToastType.Error);
        }
        finally
        {
            _isCheckingForUpdate = false;
        }
    }

    private async Task RunSingleFileAsync(
        FileItem file,
        string outputDirectory,
        string? inputRoot,
        CancellationToken cancellationToken)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            file.Status = FileStatus.Processing;
            file.ErrorMessage = null;
        });

        ConversionResult? result = null;
        switch (CurrentMode)
        {
            case AppMode.FormatDoc:
                result = await RunFormattingAsync(file, outputDirectory, cancellationToken);
                break;
            case AppMode.MarkdownToDocx:
                result = await RunPipelineMd2DocxAsync(file, outputDirectory, inputRoot, cancellationToken);
                break;
            default:
                result = await _conversionService.ConvertFileAsync(
                    file,
                    outputDirectory,
                    Settings.Conversion.PreserveFolderStructure,
                    inputRoot,
                    ConversionTarget.Markdown,
                    Settings,
                    cancellationToken);
                break;
        }

        RecordConversion(file, result);
    }

    /// <summary>F4: 转换完成后写入历史记录（仅记录完成或失败的终态）。</summary>
    private void RecordConversion(FileItem file, ConversionResult? result)
    {
        if (file.Status != FileStatus.Done && file.Status != FileStatus.Failed)
        {
            return;
        }

        _configService.RememberConversion(new ConversionRecord
        {
            Timestamp = DateTime.Now,
            SourceFilePath = file.FullPath,
            SourceFileName = file.FileName,
            OutputPath = file.OutputPath ?? string.Empty,
            Success = file.Status == FileStatus.Done,
            ErrorMessage = file.ErrorMessage ?? string.Empty,
            QualityScore = result?.Quality.QualityScore ?? 0,
            Mode = CurrentMode.ToString()
        });
        OnPropertyChanged(nameof(RecentConversions));
    }

    private async Task<ConversionResult?> RunFormattingAsync(FileItem file, string outputDirectory, CancellationToken cancellationToken)
    {
        try
        {
            // 每次排版使用当前设置构造 DocxFormatter，确保设置变更后立即生效
            var formatter = new DocxFormatter(Settings.Preview.FormatDoc);
            var result = await Task.Run(() => formatter.Format(file.FullPath, outputDirectory, cancellationToken), cancellationToken);
            if (result.Success)
            {
                file.Status = FileStatus.Done;
                file.ErrorMessage = null;
                file.OutputPath = result.OutputPath;
                LoggingService.Info($"[Formatting] 完成: {file.FullPath}");
                return result;
            }

            file.Status = FileStatus.Failed;
            file.ErrorMessage = result.ErrorMessage;
            LoggingService.Warning($"[Formatting] 失败: {file.FullPath} - {result.ErrorMessage}");
            return new ConversionResult { Success = false, ErrorMessage = result.ErrorMessage };
        }
        catch (OperationCanceledException)
        {
            file.Status = FileStatus.Pending;
            file.ErrorMessage = null;
            throw;
        }
        catch (Exception ex)
        {
            file.Status = FileStatus.Failed;
            file.ErrorMessage = ex.Message;
            _logger.Error($"[Formatting] 异常: {file.FullPath}", ex);
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<ConversionResult?> RunPipelineMd2DocxAsync(FileItem file, string outputDirectory, string? inputRoot, CancellationToken cancellationToken)
    {
        try
        {
            var templateId = Settings.Preview.MarkdownToDocx.PipelineTemplateId;
            if (string.IsNullOrWhiteSpace(templateId))
                templateId = "official-report";

            var converter = new MarkdownToDocxConverter();

            // Resolve output directory: apply PreserveFolderStructure if configured
            var currentOutputDirectory = outputDirectory;
            if (Settings.Conversion.PreserveFolderStructure && !string.IsNullOrWhiteSpace(inputRoot))
            {
                var fileDir = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
                if (fileDir.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = fileDir[inputRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.IsNullOrWhiteSpace(relativePath))
                        currentOutputDirectory = Path.Combine(outputDirectory, relativePath);
                }
            }

            Directory.CreateDirectory(currentOutputDirectory);

            var outputPath = Path.Combine(currentOutputDirectory,
                Path.GetFileNameWithoutExtension(file.FullPath) + ".docx");

            // Handle same-name output file: append suffix if file already exists
            if (File.Exists(outputPath) && !Settings.General.OverwriteExistingFile)
            {
                var counter = 1;
                var dir = Path.GetDirectoryName(outputPath)!;
                var nameWithoutExt = Path.GetFileNameWithoutExtension(file.FullPath);
                do
                {
                    outputPath = Path.Combine(dir, $"{nameWithoutExt}_{counter}.docx");
                    counter++;
                } while (File.Exists(outputPath));
            }

            var result = await Task.Run(
                () => converter.Convert(file.FullPath, outputPath, templateId),
                cancellationToken);

            if (result.Success)
            {
                file.Status = FileStatus.Done;
                file.ErrorMessage = null;
                file.OutputPath = result.OutputPath;
                LoggingService.Info($"[Pipeline md2docx] 完成: {file.FullPath} -> {result.OutputPath}");
                return new ConversionResult { Success = true, OutputPath = result.OutputPath };
            }

            file.Status = FileStatus.Failed;
            file.ErrorMessage = result.ErrorMessage;
            LoggingService.Warning($"[Pipeline md2docx] 失败: {file.FullPath} - {result.ErrorMessage}");
            return new ConversionResult { Success = false, ErrorMessage = result.ErrorMessage };
        }
        catch (OperationCanceledException)
        {
            file.Status = FileStatus.Pending;
            file.ErrorMessage = null;
            throw;
        }
        catch (Exception ex)
        {
            file.Status = FileStatus.Failed;
            file.ErrorMessage = ex.Message;
            LoggingService.Error($"[Pipeline md2docx] 异常: {file.FullPath}", ex);
            return new ConversionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task SwitchFolderAsync(string folderPath, bool isRefresh)
    {
        if (!Directory.Exists(folderPath))
        {
            ShowToast(LanguageService.GetString("Toast_FolderNotExist"), ToastType.Error);
            return;
        }

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        var scanVersion = ++_scanVersion;

        IsSwitchingFolder = true;
        StatusText = isRefresh ? LanguageService.GetString("Scan_StatusRefreshing") : LanguageService.GetString("Scan_StatusSwitching");
        StatusTone = "info";

        await AnimateFileListOutAsync();

        FileListTransitionPhase = "scanning";
        IsScanning = true;
        ScanStatusPrimary = LanguageService.GetString("Scan_PrimaryScanning");
        ScanStatusSecondary = LanguageService.GetString("Scan_SecondaryDefault");
        ScanFound = 0;
        ScanSupported = 0;

        var startedAt = DateTime.UtcNow;
        var progress = new Progress<FileScanService.ScanProgressInfo>(info =>
        {
            ScanFound = info.Found;
            ScanSupported = info.Supported;
            ScanStatusSecondary = LanguageService.GetFormatted("Scan_SecondaryProgress", info.Found, info.Supported);
        });

        FileScanService.FolderScanResult result;
        try
        {
            result = await Task.Run(() => _fileScanService.ScanFolder(folderPath, CurrentMode, token, progress), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            LoggingService.Error($"扫描文件夹失败: {folderPath}", ex);
            StatusText = LanguageService.GetString("Status_ScanFailed");
            StatusTone = "error";
            ShowToast(LanguageService.GetString("Toast_ScanFailed"), ToastType.Error);
            IsScanning = false;
            IsSwitchingFolder = false;
            FileListTransitionPhase = "steady";
            return;
        }

        var visibleFor = DateTime.UtcNow - startedAt;
        var remaining = TimeSpan.FromMilliseconds(200) - visibleFor;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, token);
        }

        if (scanVersion != _scanVersion || token.IsCancellationRequested)
        {
            return;
        }

        ReplaceModeFiles(result.Files);
        _modeCurrentFolders[CurrentMode] = folderPath;
        _configService.RememberRecentFolder(folderPath);

        ScanFound = result.Found;
        ScanSupported = result.Supported;
        IsScanning = false;
        await AnimateFileListInAsync();
        IsSwitchingFolder = false;

        var loadedMessage = LanguageService.GetFormatted("Scan_LoadedMessage", result.Supported, CurrentModeFileLabel);
        StatusText = loadedMessage;
        StatusTone = result.Unsupported > 0 ? "warning" : "success";
        _logger.Info($"扫描完成: {folderPath}, 总数={result.Found}, 可处理={result.Supported}, 不支持={result.Unsupported}");

        ShowToast(loadedMessage, ToastType.Success);
        if (result.Truncated)
        {
            // R3: 达到扫描上限时明确告知用户，避免误以为漏文件
            var truncateMessage = LanguageService.GetFormatted("Scan_Truncated", result.Found, result.Supported);
            StatusText = truncateMessage;
            StatusTone = "warning";
            ShowToast(truncateMessage, ToastType.Warning);
        }
        else if (result.Unsupported > 0)
        {
            ShowToast(LanguageService.GetFormatted("Scan_IgnoredUnsupported", result.Unsupported), ToastType.Warning);
        }

        // 文件夹内包含旧格式文件且未安装 LibreOffice 时提示
        if (result.Files.Any(f => LegacyOfficeConverter.IsLegacyOfficeFormat(f.Extension)) && !LegacyOfficeConverter.IsLibreOfficeAvailable())
        {
            ShowToast(LanguageService.GetString("Toast_FolderLegacyNeedLibreOffice"), ToastType.Warning);
        }

        RefreshFileSummaryProperties();
    }

    private static bool IsRunnableFile(FileItem file)
    {
        return file.Status is FileStatus.Pending or FileStatus.Failed;
    }

    private async Task<string?> EnsureOutputDirectoryAsync(IReadOnlyCollection<FileItem> runnableFiles)
    {
        var explicitDirectory = ActiveOutputDirectory;
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
        {
            return EnsureDirectoryExists(explicitDirectory);
        }

        if (!string.IsNullOrWhiteSpace(Settings.General.DefaultOutputDir))
        {
            return EnsureDirectoryExists(Settings.General.DefaultOutputDir, allowPrompt: true);
        }

        var fallback = CurrentFolder;
        if (string.IsNullOrWhiteSpace(fallback))
        {
            fallback = GetCommonInputRoot(runnableFiles) ?? runnableFiles.FirstOrDefault()?.Directory;
        }

        if (string.IsNullOrWhiteSpace(fallback))
        {
            ShowToast(LanguageService.GetString("Toast_SelectOutputDir"), ToastType.Warning);
            return null;
        }

        return EnsureDirectoryExists(fallback, allowPrompt: false);
    }

    private string? EnsureDirectoryExists(string directory, bool allowPrompt = true)
    {
        if (Directory.Exists(directory))
        {
            return directory;
        }

        var shouldCreate = !allowPrompt || MessageBox.Show(
            $"{LanguageService.GetString("Dialog_OutputDirNotExist")}{Environment.NewLine}{directory}",
            LanguageService.GetString("Dialog_CreateOutputDir"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        if (!shouldCreate)
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        ShowToast(LanguageService.GetString("Toast_OutputDirCreated"), ToastType.Success);
        return directory;
    }

    private string GetOpenableOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_lastResolvedOutputDirectories[CurrentMode]))
        {
            return _lastResolvedOutputDirectories[CurrentMode]!;
        }

        if (!string.IsNullOrWhiteSpace(ActiveOutputDirectory))
        {
            return ActiveOutputDirectory;
        }

        return Settings.General.DefaultOutputDir;
    }

    private static string? GetCommonInputRoot(IEnumerable<FileItem> files)
    {
        var directories = files
            .Select(file => file.Directory)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (directories.Count == 0)
        {
            return null;
        }

        if (directories.Count == 1)
        {
            return directories[0];
        }

        var commonParts = directories[0]
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        foreach (var directory in directories.Skip(1))
        {
            var parts = directory
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var length = Math.Min(commonParts.Count, parts.Length);
            var matched = 0;

            while (matched < length &&
                   commonParts[matched].Equals(parts[matched], StringComparison.OrdinalIgnoreCase))
            {
                matched++;
            }

            commonParts = commonParts.Take(matched).ToList();
            if (commonParts.Count == 0)
            {
                break;
            }
        }

        if (commonParts.Count == 0)
        {
            return Path.GetPathRoot(directories[0]);
        }

        var prefix = directories[0].StartsWith(Path.DirectorySeparatorChar)
            ? Path.DirectorySeparatorChar.ToString()
            : string.Empty;

        return prefix + string.Join(Path.DirectorySeparatorChar, commonParts);
    }

    private void ReplaceModeFiles(IEnumerable<FileItem> files)
    {
        ActiveFiles.Clear();
        foreach (var file in files)
        {
            ActiveFiles.Add(file);
        }
    }

    private void ClearCurrentFolderContext()
    {
        _modeCurrentFolders[CurrentMode] = null;
        ScanFound = 0;
        ScanSupported = 0;
        OnPropertyChanged(nameof(CurrentFolderSummaryText));
        OnPropertyChanged(nameof(HasCurrentFolder));
        OnPropertyChanged(nameof(CanUseFolderActions));
    }

    private void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error($"打开路径失败: {path}", ex);
            ShowToast(LanguageService.GetString("Toast_OpenPathFailed"), ToastType.Error);
        }
    }

    private void ShowToast(string message, ToastType type = ToastType.Info)
    {
        _toastService.Show(message, type);
    }

    private async Task AnimateTaskPanelOutAsync()
    {
        if (Settings.Appearance.Motion == MotionLevel.Off)
        {
            TaskPanelTransitionPhase = "steady";
            return;
        }

        TaskPanelTransitionPhase = "exit";
        await Task.Delay(Settings.Appearance.Motion == MotionLevel.Standard ? 90 : 120);
    }

    private async Task AnimateTaskPanelInAsync()
    {
        if (Settings.Appearance.Motion == MotionLevel.Off)
        {
            TaskPanelTransitionPhase = "steady";
            return;
        }

        TaskPanelTransitionPhase = "enter";
        await Task.Delay(Settings.Appearance.Motion == MotionLevel.Standard ? 120 : 180);
        TaskPanelTransitionPhase = "steady";
    }

    private async Task AnimateFileListOutAsync()
    {
        if (Settings.Appearance.Motion == MotionLevel.Off)
        {
            FileListTransitionPhase = "scanning";
            return;
        }

        FileListTransitionPhase = "exit";
        await Task.Delay(Settings.Appearance.Motion == MotionLevel.Standard ? 90 : 140);
    }

    private async Task AnimateFileListInAsync()
    {
        if (Settings.Appearance.Motion == MotionLevel.Off)
        {
            FileListTransitionPhase = "steady";
            return;
        }

        FileListTransitionPhase = "enter";
        await Task.Delay(Settings.Appearance.Motion == MotionLevel.Standard ? 140 : 220);
        FileListTransitionPhase = "steady";
    }

    private void RaiseModeDependentProperties()
    {
        OnPropertyChanged(nameof(SelectedModeIndex));
        OnPropertyChanged(nameof(ActiveFiles));
        OnPropertyChanged(nameof(CurrentModeTitle));
        OnPropertyChanged(nameof(CurrentModeDescription));
        OnPropertyChanged(nameof(CurrentModeDetail));
        OnPropertyChanged(nameof(AddFileButtonText));
        OnPropertyChanged(nameof(DropZoneTitle));
        OnPropertyChanged(nameof(DropZoneSubtitle));
        OnPropertyChanged(nameof(ActiveOutputDirectory));
        OnPropertyChanged(nameof(OutputDirectoryDisplay));
        OnPropertyChanged(nameof(HasSelectedOutputDirectory));
        OnPropertyChanged(nameof(CurrentFolder));
        OnPropertyChanged(nameof(HasCurrentFolder));
        OnPropertyChanged(nameof(CurrentFolderSummaryText));
        OnPropertyChanged(nameof(HasCurrentFolderSummary));
        OnPropertyChanged(nameof(CurrentActionVerb));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(CanOpenOutputDirectory));
        OnPropertyChanged(nameof(CanPrimaryAction));
        OnPropertyChanged(nameof(ProcessProgressMaximum));
        OnPropertyChanged(nameof(ProcessProgressText));
        OnPropertyChanged(nameof(ProcessProgressPercentText));
        OnPropertyChanged(nameof(IsPreviewButtonVisible));
        OnPropertyChanged(nameof(PreviewButtonText));
        OnPropertyChanged(nameof(PreviewPanelTitle));
        OnPropertyChanged(nameof(PreviewEmptyText));
        OnPropertyChanged(nameof(CanPreview));
        RefreshFileSummaryProperties();
    }

    private void OnModeCollectionChanged(AppMode mode, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems != null)
        {
            foreach (FileItem item in args.OldItems)
            {
                item.PropertyChanged -= OnFileItemPropertyChanged;
            }
        }

        if (args.NewItems != null)
        {
            foreach (FileItem item in args.NewItems)
            {
                item.PropertyChanged += OnFileItemPropertyChanged;
            }
        }

        if (mode == CurrentMode)
        {
            RefreshFileSummaryProperties();
        }
    }

    private void OnFileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FileItem item || !ActiveFiles.Contains(item))
        {
            return;
        }

        if (e.PropertyName is nameof(FileItem.Status) or nameof(FileItem.OutputPath))
        {
            RefreshFileSummaryProperties();
        }
    }

    private void RefreshFileSummaryProperties()
    {
        OnPropertyChanged(nameof(CurrentFolderSummaryText));
        OnPropertyChanged(nameof(HasCurrentFolderSummary));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(ProcessingCount));
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(UnsupportedCount));
        OnPropertyChanged(nameof(CanStartProcessing));
        OnPropertyChanged(nameof(CanPrimaryAction));
        OnPropertyChanged(nameof(CanClearFiles));
        OnPropertyChanged(nameof(CanUseFolderActions));
        OnPropertyChanged(nameof(CanOpenOutputDirectory));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(ProcessProgressText));
        OnPropertyChanged(nameof(ProcessProgressPercentText));
    }

    private string CurrentModeFileLabel => CurrentMode switch
    {
        AppMode.MarkdownToDocx => LanguageService.GetString("FileLabel_Markdown"),
        AppMode.FormatDoc => LanguageService.GetString("FileLabel_Word"),
        _ => LanguageService.GetString("FileLabel_Document")
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static FileItem CreateFileItem(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return new FileItem
        {
            FullPath = filePath,
            FileName = Path.GetFileName(filePath),
            Directory = Path.GetDirectoryName(filePath) ?? string.Empty,
            Extension = ext,
            Type = FileItem.GetFileType(ext),
            Status = FileStatus.Pending
        };
    }
}
