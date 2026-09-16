using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Doc2MD.DependencyInjection;
using Doc2MD.Models;
using Doc2MD.Services;
using Doc2MD.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Doc2MD;

public partial class App : Application
{
    /// <summary>DI 容器实例，供解析服务使用。</summary>
    internal IServiceProvider? Services { get; private set; }

    /// <summary>供系统主题变更回调和设置持久化时使用的当前外观配置。</summary>
    internal AppearanceSettings? CurrentAppearance { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // 构建 DI 容器并同步日志门面
            Services = new ServiceCollection().BuildApplicationServices();
            var config = Services.GetRequiredService<ConfigService>().Config;
            LoggingService.Info("应用程序启动");
            ApplyAppearanceSettings(config.Appearance);

            // 从容器解析主窗口与 ViewModel，取代 XAML StartupUri 的隐式实例化
            var mainWindow = new MainWindow(Services.GetRequiredService<MainViewModel>());
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LoggingService.Error("应用程序启动失败", ex);
            MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LoggingService.Error("未处理的域异常", ex);
            // 域级异常意味着进程即将终止，无法阻止崩溃，但必须先落盘日志再提示
            try
            {
                MessageBox.Show($"发生未处理的严重异常，应用即将关闭：{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}日志已保存至：{AppPaths.LogDirectory}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // 提示失败不影响日志
            }
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LoggingService.Error("UI线程未处理异常", e.Exception);
        MessageBox.Show($"发生异常：{e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // 后台 Task 未观察异常：记录日志并标记已观察，防止进程因未处理异常终止。
        LoggingService.Error("后台任务未处理异常", e.Exception);
        e.SetObserved();
    }

    public void ApplyAppearanceSettings(AppearanceSettings appearance)
    {
        CurrentAppearance = appearance;
        var theme = ResolveThemeMode(appearance.Theme);
        ApplyTheme(theme);
        UpdateThemeSubscription(appearance.Theme);
    }

    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["BgMainBrush"] = "#0F172A",
        ["BgCardBrush"] = "#111827",
        ["BgActiveBrush"] = "#2A2113",
        ["BgSubtleBrush"] = "#0B1220",
        ["BgFooterBrush"] = "#0D1526",
        ["BgFileListBrush"] = "#0D1526",
        ["PrimaryBrush"] = "#F59E0B",
        ["PrimaryHoverBrush"] = "#D97706",
        ["PrimarySoftBrush"] = "#3A2A10",
        ["TextMainBrush"] = "#F9FAFB",
        ["TextSecondaryBrush"] = "#CBD5E1",
        ["TextMutedBrush"] = "#94A3B8",
        ["TextWeakBrush"] = "#64748B",
        ["BorderBrush"] = "#334155",
        ["BorderStrongBrush"] = "#475569",
        ["DividerBrush"] = "#1C2536",
        ["HeaderButtonHoverBrush"] = "#1F2937",
        ["DangerHeaderHoverBrush"] = "#7F1D1D",
        ["DangerHeaderForegroundBrush"] = "#FCA5A5",
        ["DarkButtonBrush"] = "#020617",
        ["DarkButtonHoverBrush"] = "#000000",
        ["DisabledBackgroundBrush"] = "#334155",
        ["DisabledForegroundBrush"] = "#64748B",
        ["SecondaryHoverBrush"] = "#1F2937",
        ["SecondaryDisabledBackgroundBrush"] = "#1E293B",
        ["SecondaryDisabledForegroundBrush"] = "#64748B",
        ["GhostHoverBrush"] = "#1F2937",
        ["InputBackgroundBrush"] = "#1F2937",
        ["ProgressTrackBrush"] = "#334155",
        ["DropZoneBackgroundBrush"] = "#161F2F",
        ["DropZoneBorderBrush"] = "#F59E0B",
        ["SkeletonPrimaryBrush"] = "#1F2937",
        ["SkeletonSecondaryBrush"] = "#334155",
        ["ModeIconBorderBrush"] = "#B47A1A",
        ["ModeIconCornerBrush"] = "#B47A1A",
        ["StatusPendingBgBrush"] = "#1E293B",
        ["StatusProcessingBgBrush"] = "#1E3A5F",
        ["StatusDoneBgBrush"] = "#14412A",
        ["StatusFailedBgBrush"] = "#4C1515",
        ["StatusUnsupportedBgBrush"] = "#422D0E",
        ["StatusSkippedBgBrush"] = "#1E293B",
        ["StatusPendingFgBrush"] = "#94A3B8",
        ["StatusProcessingFgBrush"] = "#60A5FA",
        ["StatusDoneFgBrush"] = "#4ADE80",
        ["StatusFailedFgBrush"] = "#F87171",
        ["StatusUnsupportedFgBrush"] = "#FBBF24",
        ["StatusSkippedFgBrush"] = "#94A3B8",
        ["ToastSuccessBgBrush"] = "#14412A",
        ["ToastWarningBgBrush"] = "#422D0E",
        ["ToastErrorBgBrush"] = "#4C1515",
        ["ToastInfoBgBrush"] = "#1E3A5F",
        ["ToneSuccessBrush"] = "#4ADE80",
        ["ToneWarningBrush"] = "#FBBF24",
        ["ToneErrorBrush"] = "#F87171",
        ["ToneInfoBrush"] = "#60A5FA"
    };

    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["BgMainBrush"] = "#E0E2E6",
        ["BgCardBrush"] = "#FFFFFF",
        ["BgActiveBrush"] = "#FFF3E2",
        ["BgSubtleBrush"] = "#F3F4F6",
        ["BgFooterBrush"] = "#F3F4F6",
        ["BgFileListBrush"] = "#F3F4F6",
        ["PrimaryBrush"] = "#D98212",
        ["PrimaryHoverBrush"] = "#B96700",
        ["PrimarySoftBrush"] = "#FFEDCA",
        ["TextMainBrush"] = "#111827",
        ["TextSecondaryBrush"] = "#4B5563",
        ["TextMutedBrush"] = "#6B7280",
        ["TextWeakBrush"] = "#9CA3AF",
        ["BorderBrush"] = "#E5E7EB",
        ["BorderStrongBrush"] = "#D1D5DB",
        ["DividerBrush"] = "#D5D8DD",
        ["HeaderButtonHoverBrush"] = "#F3F4F6",
        ["DangerHeaderHoverBrush"] = "#FEE2E2",
        ["DangerHeaderForegroundBrush"] = "#B91C1C",
        ["DarkButtonBrush"] = "#111827",
        ["DarkButtonHoverBrush"] = "#000000",
        ["DisabledBackgroundBrush"] = "#D1D5DB",
        ["DisabledForegroundBrush"] = "#9CA3AF",
        ["SecondaryHoverBrush"] = "#F9FAFB",
        ["SecondaryDisabledBackgroundBrush"] = "#F3F4F6",
        ["SecondaryDisabledForegroundBrush"] = "#9CA3AF",
        ["GhostHoverBrush"] = "#F3F4F6",
        ["InputBackgroundBrush"] = "#F9FAFB",
        ["ProgressTrackBrush"] = "#E5E7EB",
        ["DropZoneBackgroundBrush"] = "#FFF8ED",
        ["DropZoneBorderBrush"] = "#D4A84B",
        ["SkeletonPrimaryBrush"] = "#F3F4F6",
        ["SkeletonSecondaryBrush"] = "#E5E7EB",
        ["ModeIconBorderBrush"] = "#D4A84B",
        ["ModeIconCornerBrush"] = "#D4A84B",
        ["StatusPendingBgBrush"] = "#F3F4F6",
        ["StatusProcessingBgBrush"] = "#DBEAFE",
        ["StatusDoneBgBrush"] = "#DCFCE7",
        ["StatusFailedBgBrush"] = "#FEE2E2",
        ["StatusUnsupportedBgBrush"] = "#FEF3C7",
        ["StatusSkippedBgBrush"] = "#F3F4F6",
        ["StatusPendingFgBrush"] = "#6B7280",
        ["StatusProcessingFgBrush"] = "#2563EB",
        ["StatusDoneFgBrush"] = "#16A34A",
        ["StatusFailedFgBrush"] = "#DC2626",
        ["StatusUnsupportedFgBrush"] = "#D97706",
        ["StatusSkippedFgBrush"] = "#6B7280",
        ["ToastSuccessBgBrush"] = "#ECFDF3",
        ["ToastWarningBgBrush"] = "#FFF7ED",
        ["ToastErrorBgBrush"] = "#FEF2F2",
        ["ToastInfoBgBrush"] = "#EFF6FF",
        ["ToneSuccessBrush"] = "#16A34A",
        ["ToneWarningBrush"] = "#F59E0B",
        ["ToneErrorBrush"] = "#DC2626",
        ["ToneInfoBrush"] = "#2563EB"
    };

    private void ApplyTheme(ThemeMode theme)
    {
        var palette = theme == ThemeMode.Dark ? DarkPalette : LightPalette;
        foreach (var (key, hex) in palette)
        {
            SetBrushColor(key, hex);
        }
    }

    private bool _themeSubscribed;

    private ThemeMode ResolveThemeMode(ThemeMode requestedTheme)
    {
        if (requestedTheme != ThemeMode.System)
        {
            return requestedTheme;
        }

        try
        {
            using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = personalize?.GetValue("AppsUseLightTheme");
            return value is int current && current == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            return ThemeMode.Light;
        }
    }

    /// <summary>
    /// 在 ThemeMode.System 时订阅系统主题变更，实现运行时自动跟随。
    /// 切换为非 System 模式时取消订阅。
    /// </summary>
    public void UpdateThemeSubscription(ThemeMode configuredTheme)
    {
        var shouldSubscribe = configuredTheme == ThemeMode.System;

        if (shouldSubscribe && !_themeSubscribed)
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
            _themeSubscribed = true;
        }
        else if (!shouldSubscribe && _themeSubscribed)
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
            _themeSubscribed = false;
        }
    }

    private void OnSystemPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General)
        {
            return;
        }

        // 回到 UI 线程应用主题（实例方法中可直接访问实例属性）
        Dispatcher.BeginInvoke(() =>
        {
            ApplyAppearanceSettings(CurrentAppearance ?? new AppearanceSettings { Theme = ThemeMode.System });
        });
    }

    private void SetBrushColor(string resourceKey, string colorValue)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorValue);

        if (Resources[resourceKey] is SolidColorBrush existing && !existing.IsSealed)
        {
            // Mutate in-place so both StaticResource and DynamicResource references update
            existing.Color = color;
        }
        else
        {
            // Brush is sealed or missing — replace the entry.
            // DynamicResource references will pick up the new value automatically.
            Resources[resourceKey] = new SolidColorBrush(color);
        }
    }
}
