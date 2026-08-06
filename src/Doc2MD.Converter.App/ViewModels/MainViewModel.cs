using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Doc2MD.Models;
using Doc2MD.Services;
using Microsoft.Win32;

namespace Doc2MD.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ConversionService _conversionService;
    private readonly ConfigService _configService;
    private readonly ToastService _toastService;
    private readonly FileScanService _fileScanService;
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

    public MainViewModel()
    {
        _configService = new ConfigService();
        _conversionService = new ConversionService();
        _toastService = new ToastService();
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
        LoggingService.Info("主界面已初始化");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppConfig Settings => _configService.Config;
    public string AppVersion => Constants.AppVersion.FullString;

    public AppMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (_currentMode == value) return;
            _currentMode = value;
            OnPropertyChanged();
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
    public string CurrentModeTitle => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "Markdown 转 DOCX",
        AppMode.FormatDoc => "一键排版",
        _ => "文档转 Markdown"
    };
    public string CurrentModeDescription => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "按照公文格式生成 Word 文档",
        AppMode.FormatDoc => "统一字体、标题、段落与页边距",
        _ => "支持 PDF / Word / Excel / PPT"
    };
    public string CurrentModeDetail => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "批量转换，高效稳定",
        AppMode.FormatDoc => "规范化排版，提升文档质量",
        _ => "本地解析，提取文本内容"
    };
    public string AddFileButtonText => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "添加 Markdown",
        AppMode.FormatDoc => "添加 Word",
        _ => "添加文件"
    };
    public string DropZoneTitle => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "拖拽 Markdown 文件或文件夹到这里",
        AppMode.FormatDoc => "拖拽 Word 文档或文件夹到这里",
        _ => "拖拽文档或文件夹到这里"
    };
    public string DropZoneSubtitle => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "支持 .md / .markdown 文件",
        AppMode.FormatDoc => "支持 .doc / .docx 文件",
        _ => "支持 PDF / Word / Excel / PPT"
    };
    public string DropZoneFootnote => "也可以点击上方按钮添加";
    public string DragActiveText => "松开以添加文件";
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
    public bool CanPrimaryAction => CanStartProcessing || (!IsInteractionLocked && PrimaryActionText == "打开输出目录" && CanOpenOutputDirectory);
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
                return $"{CurrentActionVerb}：{ProcessCurrent} / {ProcessTotal}";
            }

            if (ProcessTotal > 0)
            {
                if (FailedCount > 0)
                {
                    return $"部分失败：{DoneCount} 成功，{FailedCount} 失败";
                }

                return $"已完成：{ProcessCurrent} / {ProcessTotal}";
            }

            return "暂无任务";
        }
    }
    public string ProcessProgressPercentText => ProcessTotal <= 0 ? string.Empty : $"{Math.Round(ProcessProgressPercent):0}%";
    public string CurrentActionVerb => CurrentMode switch
    {
        AppMode.MarkdownToDocx => "正在生成 DOCX",
        AppMode.FormatDoc => "正在排版",
        _ => "正在生成 Markdown"
    };
    public string PrimaryActionText
    {
        get
        {
            if (IsProcessing)
            {
                return $"正在处理 {ProcessCurrent} / {ProcessTotal}";
            }

            if (ActiveFiles.Count > 0 &&
                ActiveFiles.All(file => !FileScanService.IsSupportedForMode(file.FullPath, CurrentMode) || file.Status == FileStatus.Done) &&
                ActiveFiles.Any(file => file.Status == FileStatus.Done))
            {
                return "打开输出目录";
            }

            return CurrentMode switch
            {
                AppMode.MarkdownToDocx => "生成 DOCX",
                AppMode.FormatDoc => "开始排版",
                _ => "生成 Markdown"
            };
        }
    }
    public string CurrentFolderSummaryText
    {
        get
        {
            if (HasCurrentFolder)
            {
                return $"当前文件夹：{CurrentFolder}    共 {ScanFound} 个文件，其中 {ScanSupported} 个可处理";
            }

            return ActiveFiles.Count == 0
                ? string.Empty
                : $"已添加 {ActiveFiles.Count} 个文件";
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

    public async Task SwitchModeAsync(AppMode mode)
    {
        if (CurrentMode == mode || IsInteractionLocked) return;

        LoggingService.Info($"切换模式: {CurrentMode} -> {mode}");
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
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = CurrentMode switch
            {
                AppMode.MarkdownToDocx => "Markdown 文件|*.md;*.markdown|所有文件|*.*",
                AppMode.FormatDoc => "Word 文档|*.doc;*.docx|所有文件|*.*",
                _ => "支持的文档|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx|所有文件|*.*"
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
            Title = "选择文件夹"
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

        StatusText = added.Count > 0 ? $"已添加 {added.Count} 个文件" : "未添加新文件";
        StatusTone = unsupported > 0 ? "warning" : "success";
        LoggingService.Info($"手动添加文件: {added.Count}，跳过重复: {skipped}，不支持: {unsupported}");

        if (added.Count > 0)
        {
            ShowToast($"已添加 {added.Count} 个文件", unsupported > 0 ? ToastType.Warning : ToastType.Success);
        }

        if (skipped > 0)
        {
            ShowToast($"已跳过 {skipped} 个重复文件", ToastType.Info);
        }

        if (unsupported > 0)
        {
            ShowToast($"已保留 {unsupported} 个不支持的文件", ToastType.Warning);
        }

        RefreshFileSummaryProperties();
    }

    public void ClearFiles()
    {
        if (ActiveFiles.Count == 0) return;

        ActiveFiles.Clear();
        _modeCurrentFolders[CurrentMode] = null;
        ScanFound = 0;
        ScanSupported = 0;
        StatusText = "文件列表已清空";
        StatusTone = "success";
        LoggingService.Info($"清空文件列表: {CurrentMode}");
        ShowToast("已清空文件列表", ToastType.Info);
        RefreshFileSummaryProperties();
    }

    public void RemoveFile(FileItem item)
    {
        ActiveFiles.Remove(item);
        LoggingService.Info($"移除文件: {item.FullPath}");
        RefreshFileSummaryProperties();
    }

    public async Task HandleFileActionAsync(FileItem item)
    {
        switch (item.Status)
        {
            case FileStatus.Done:
                OpenPath(item.OutputPath ?? item.Directory);
                break;
            case FileStatus.Processing:
                CancelProcessing();
                break;
            case FileStatus.Failed:
                item.Status = FileStatus.Pending;
                item.ErrorMessage = null;
                ShowToast($"已将 {item.FileName} 标记为重试", ToastType.Info);
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

    public async Task StartProcessingAsync()
    {
        if (!IsProcessing && PrimaryActionText == "打开输出目录" && CanOpenOutputDirectory && !CanStartProcessing)
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
            ShowToast("没有可处理的文件", ToastType.Warning);
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
        StatusText = "正在生成";
        StatusTone = "info";
        LoggingService.Info($"开始处理任务: 模式={CurrentMode}, 文件数={runnableFiles.Count}, 输出目录={outputDirectory}");

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
            StatusText = failedPaths.Count > 0 ? "处理已中断" : "已取消";
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
            StatusText = "部分失败";
            StatusTone = "warning";
            ShowToast("部分文件转换失败，请查看任务列表", ToastType.Error);
        }
        else if (ActiveFiles.Any(file => file.Status == FileStatus.Done))
        {
            StatusText = "生成完成";
            StatusTone = "success";
            ShowToast("转换完成，已保存到输出目录", ToastType.Success);

            if (Settings.General.AutoOpenOutputDir)
            {
                OpenOutputDirectory();
            }
        }
        else
        {
            StatusText = "已取消";
            StatusTone = "info";
        }
    }

    public void CancelProcessing()
    {
        if (IsProcessing)
        {
            _processCts?.Cancel();
            StatusText = "正在取消...";
            StatusTone = "info";
            LoggingService.Warning("用户取消当前处理任务");
        }

        if (IsScanning)
        {
            _scanCts?.Cancel();
            IsScanning = false;
            IsSwitchingFolder = false;
            FileListTransitionPhase = "steady";
            StatusText = "已取消扫描";
            StatusTone = "warning";
            LoggingService.Warning("用户取消文件夹扫描");
        }
    }

    public void BrowseOutputDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择输出目录"
        };

        if (dialog.ShowDialog() == true)
        {
            ActiveOutputDirectory = dialog.FolderName;
            _configService.RememberRecentOutputDirectory(dialog.FolderName);
            ShowToast("输出目录已更新", ToastType.Success);
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
        var info = $"文档处理器 {AppVersion}{Environment.NewLine}本地离线运行{Environment.NewLine}日志目录：{AppPaths.LogDirectory}";
        Clipboard.SetText(info);
        ShowToast("已复制软件信息", ToastType.Success);
    }

    public void PersistSettings(string successMessage = "设置已保存")
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

        LoggingService.Info("设置已保存");
        ShowToast(successMessage, ToastType.Success);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(UiScale));
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

    public void CheckForUpdates()
    {
        ShowToast("当前版本暂不支持自动更新", ToastType.Info);
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

        switch (CurrentMode)
        {
            case AppMode.FormatDoc:
                await RunFormattingAsync(file, outputDirectory, cancellationToken);
                break;
            case AppMode.MarkdownToDocx:
                await _conversionService.ConvertFileAsync(
                    file,
                    outputDirectory,
                    Settings.Conversion.PreserveFolderStructure,
                    inputRoot,
                    ConversionTarget.OfficialDocx,
                    Settings.Preview.MarkdownToDocx,
                    cancellationToken);
                break;
            default:
                await _conversionService.ConvertFileAsync(
                    file,
                    outputDirectory,
                    Settings.Conversion.PreserveFolderStructure,
                    inputRoot,
                    ConversionTarget.Markdown,
                    Settings,
                    cancellationToken);
                break;
        }
    }

    private async Task RunFormattingAsync(FileItem file, string outputDirectory, CancellationToken cancellationToken)
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
            }
            else
            {
                file.Status = FileStatus.Failed;
                file.ErrorMessage = result.ErrorMessage;
                LoggingService.Warning($"[Formatting] 失败: {file.FullPath} - {result.ErrorMessage}");
            }
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
            LoggingService.Error($"[Formatting] 异常: {file.FullPath}", ex);
        }
    }

    private async Task SwitchFolderAsync(string folderPath, bool isRefresh)
    {
        if (!Directory.Exists(folderPath))
        {
            ShowToast("所选文件夹不存在", ToastType.Error);
            return;
        }

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        var scanVersion = ++_scanVersion;

        IsSwitchingFolder = true;
        StatusText = isRefresh ? "正在刷新文件夹..." : "正在切换文件夹...";
        StatusTone = "info";

        await AnimateFileListOutAsync();

        FileListTransitionPhase = "scanning";
        IsScanning = true;
        ScanStatusPrimary = "正在扫描文件夹...";
        ScanStatusSecondary = $"正在识别{CurrentModeFileLabel}和子目录";
        ScanFound = 0;
        ScanSupported = 0;

        var startedAt = DateTime.UtcNow;
        var progress = new Progress<FileScanService.ScanProgressInfo>(info =>
        {
            ScanFound = info.Found;
            ScanSupported = info.Supported;
            ScanStatusSecondary = $"已发现 {info.Found} 个文件，其中 {info.Supported} 个可处理";
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
            StatusText = "扫描失败";
            StatusTone = "error";
            ShowToast("扫描文件夹失败，请查看日志", ToastType.Error);
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

        var loadedMessage = $"已加载 {result.Supported} 个{CurrentModeFileLabel}";
        StatusText = loadedMessage;
        StatusTone = result.Unsupported > 0 ? "warning" : "success";
        LoggingService.Info($"扫描完成: {folderPath}, 总数={result.Found}, 可处理={result.Supported}, 不支持={result.Unsupported}");

        ShowToast(loadedMessage, ToastType.Success);
        if (result.Unsupported > 0)
        {
            ShowToast($"已忽略 {result.Unsupported} 个不支持的文件", ToastType.Warning);
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
            ShowToast("请选择输出目录，默认使用源文件所在目录", ToastType.Warning);
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
            $"输出目录不存在，是否创建？{Environment.NewLine}{directory}",
            "创建输出目录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        if (!shouldCreate)
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        ShowToast("输出目录不存在，已自动创建", ToastType.Success);
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
            LoggingService.Error($"打开路径失败: {path}", ex);
            ShowToast("打开路径失败，请查看日志", ToastType.Error);
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
        AppMode.MarkdownToDocx => "Markdown 文件",
        AppMode.FormatDoc => "Word 文件",
        _ => "文档"
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
