using System;
using System.Windows;
using System.Windows.Media;
using Doc2MD.Models;
using Doc2MD.Services;
using Microsoft.Win32;

namespace Doc2MD;

public partial class App : Application
{
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
            var config = new ConfigService().Config;
            LoggingService.Info("应用程序启动");
            ApplyAppearanceSettings(config.Appearance);
            base.OnStartup(e);
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
            MessageBox.Show($"未处理异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LoggingService.Error("UI线程未处理异常", e.Exception);
        MessageBox.Show($"UI异常: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    public void ApplyAppearanceSettings(AppearanceSettings appearance)
    {
        var theme = ResolveThemeMode(appearance.Theme);
        ApplyTheme(theme);
    }

    private void ApplyTheme(ThemeMode theme)
    {
        if (theme == ThemeMode.Dark)
        {
            SetBrushColor("BgMainBrush", "#0F172A");
            SetBrushColor("BgCardBrush", "#111827");
            SetBrushColor("BgActiveBrush", "#2A2113");
            SetBrushColor("BgSubtleBrush", "#0B1220");
            SetBrushColor("BgFooterBrush", "#0D1526");
            SetBrushColor("BgFileListBrush", "#0D1526");
            SetBrushColor("PrimaryBrush", "#F59E0B");
            SetBrushColor("PrimaryHoverBrush", "#D97706");
            SetBrushColor("PrimarySoftBrush", "#3A2A10");
            SetBrushColor("TextMainBrush", "#F9FAFB");
            SetBrushColor("TextSecondaryBrush", "#CBD5E1");
            SetBrushColor("TextMutedBrush", "#94A3B8");
            SetBrushColor("TextWeakBrush", "#64748B");
            SetBrushColor("BorderBrush", "#334155");
            SetBrushColor("BorderStrongBrush", "#475569");
            SetBrushColor("DividerBrush", "#1C2536");
            SetBrushColor("HeaderButtonHoverBrush", "#1F2937");
            SetBrushColor("DangerHeaderHoverBrush", "#7F1D1D");
            SetBrushColor("DangerHeaderForegroundBrush", "#FCA5A5");
            SetBrushColor("DarkButtonBrush", "#020617");
            SetBrushColor("DarkButtonHoverBrush", "#000000");
            SetBrushColor("DisabledBackgroundBrush", "#334155");
            SetBrushColor("DisabledForegroundBrush", "#64748B");
            SetBrushColor("SecondaryHoverBrush", "#1F2937");
            SetBrushColor("SecondaryDisabledBackgroundBrush", "#1E293B");
            SetBrushColor("SecondaryDisabledForegroundBrush", "#64748B");
            SetBrushColor("GhostHoverBrush", "#1F2937");
            SetBrushColor("InputBackgroundBrush", "#1F2937");
            SetBrushColor("ProgressTrackBrush", "#334155");
            SetBrushColor("DropZoneBackgroundBrush", "#161F2F");
            SetBrushColor("DropZoneBorderBrush", "#F59E0B");
            SetBrushColor("SkeletonPrimaryBrush", "#1F2937");
            SetBrushColor("SkeletonSecondaryBrush", "#334155");
            SetBrushColor("ModeIconBorderBrush", "#B47A1A");
            SetBrushColor("ModeIconCornerBrush", "#B47A1A");
            // Status indicator dark colors (F-09)
            SetBrushColor("StatusPendingBgBrush", "#1E293B");
            SetBrushColor("StatusProcessingBgBrush", "#1E3A5F");
            SetBrushColor("StatusDoneBgBrush", "#14412A");
            SetBrushColor("StatusFailedBgBrush", "#4C1515");
            SetBrushColor("StatusUnsupportedBgBrush", "#422D0E");
            SetBrushColor("StatusSkippedBgBrush", "#1E293B");
            SetBrushColor("StatusPendingFgBrush", "#94A3B8");
            SetBrushColor("StatusProcessingFgBrush", "#60A5FA");
            SetBrushColor("StatusDoneFgBrush", "#4ADE80");
            SetBrushColor("StatusFailedFgBrush", "#F87171");
            SetBrushColor("StatusUnsupportedFgBrush", "#FBBF24");
            SetBrushColor("StatusSkippedFgBrush", "#94A3B8");
            // Toast dark colors (F-10)
            SetBrushColor("ToastSuccessBgBrush", "#14412A");
            SetBrushColor("ToastWarningBgBrush", "#422D0E");
            SetBrushColor("ToastErrorBgBrush", "#4C1515");
            SetBrushColor("ToastInfoBgBrush", "#1E3A5F");
            // Tone dark colors
            SetBrushColor("ToneSuccessBrush", "#4ADE80");
            SetBrushColor("ToneWarningBrush", "#FBBF24");
            SetBrushColor("ToneErrorBrush", "#F87171");
            SetBrushColor("ToneInfoBrush", "#60A5FA");
            return;
        }

        SetBrushColor("BgMainBrush", "#E0E2E6");
        SetBrushColor("BgCardBrush", "#FFFFFF");
        SetBrushColor("BgActiveBrush", "#FFF3E2");
        SetBrushColor("BgSubtleBrush", "#F3F4F6");
        SetBrushColor("BgFooterBrush", "#F3F4F6");
        SetBrushColor("BgFileListBrush", "#F3F4F6");
        SetBrushColor("PrimaryBrush", "#D98212");
        SetBrushColor("PrimaryHoverBrush", "#B96700");
        SetBrushColor("PrimarySoftBrush", "#FFEDCA");
        SetBrushColor("TextMainBrush", "#111827");
        SetBrushColor("TextSecondaryBrush", "#4B5563");
        SetBrushColor("TextMutedBrush", "#6B7280");
        SetBrushColor("TextWeakBrush", "#9CA3AF");
        SetBrushColor("BorderBrush", "#E5E7EB");
        SetBrushColor("BorderStrongBrush", "#D1D5DB");
        SetBrushColor("DividerBrush", "#D5D8DD");
        SetBrushColor("HeaderButtonHoverBrush", "#F3F4F6");
        SetBrushColor("DangerHeaderHoverBrush", "#FEE2E2");
        SetBrushColor("DangerHeaderForegroundBrush", "#B91C1C");
        SetBrushColor("DarkButtonBrush", "#111827");
        SetBrushColor("DarkButtonHoverBrush", "#000000");
        SetBrushColor("DisabledBackgroundBrush", "#D1D5DB");
        SetBrushColor("DisabledForegroundBrush", "#9CA3AF");
        SetBrushColor("SecondaryHoverBrush", "#F9FAFB");
        SetBrushColor("SecondaryDisabledBackgroundBrush", "#F3F4F6");
        SetBrushColor("SecondaryDisabledForegroundBrush", "#9CA3AF");
        SetBrushColor("GhostHoverBrush", "#F3F4F6");
        SetBrushColor("InputBackgroundBrush", "#F9FAFB");
        SetBrushColor("ProgressTrackBrush", "#E5E7EB");
        SetBrushColor("DropZoneBackgroundBrush", "#FFF8ED");
        SetBrushColor("DropZoneBorderBrush", "#D4A84B");
        SetBrushColor("SkeletonPrimaryBrush", "#F3F4F6");
        SetBrushColor("SkeletonSecondaryBrush", "#E5E7EB");
        SetBrushColor("ModeIconBorderBrush", "#D4A84B");
        SetBrushColor("ModeIconCornerBrush", "#D4A84B");
        // Status indicator light colors (F-09)
        SetBrushColor("StatusPendingBgBrush", "#F3F4F6");
        SetBrushColor("StatusProcessingBgBrush", "#DBEAFE");
        SetBrushColor("StatusDoneBgBrush", "#DCFCE7");
        SetBrushColor("StatusFailedBgBrush", "#FEE2E2");
        SetBrushColor("StatusUnsupportedBgBrush", "#FEF3C7");
        SetBrushColor("StatusSkippedBgBrush", "#F3F4F6");
        SetBrushColor("StatusPendingFgBrush", "#6B7280");
        SetBrushColor("StatusProcessingFgBrush", "#2563EB");
        SetBrushColor("StatusDoneFgBrush", "#16A34A");
        SetBrushColor("StatusFailedFgBrush", "#DC2626");
        SetBrushColor("StatusUnsupportedFgBrush", "#D97706");
        SetBrushColor("StatusSkippedFgBrush", "#6B7280");
        // Toast light colors (F-10)
        SetBrushColor("ToastSuccessBgBrush", "#ECFDF3");
        SetBrushColor("ToastWarningBgBrush", "#FFF7ED");
        SetBrushColor("ToastErrorBgBrush", "#FEF2F2");
        SetBrushColor("ToastInfoBgBrush", "#EFF6FF");
        // Tone light colors
        SetBrushColor("ToneSuccessBrush", "#16A34A");
        SetBrushColor("ToneWarningBrush", "#F59E0B");
        SetBrushColor("ToneErrorBrush", "#DC2626");
        SetBrushColor("ToneInfoBrush", "#2563EB");
    }

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
