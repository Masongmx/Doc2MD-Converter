using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Doc2MD.Models;

namespace Doc2MD.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool current && !current;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool current && !current;
    }
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        FileStatus.Pending => "待处理",
        FileStatus.Processing => "处理中",
        FileStatus.Done => "已完成",
        FileStatus.Failed => "失败",
        FileStatus.Unsupported => "不支持",
        FileStatus.Skipped => "已跳过",
        _ => "待处理"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileStatusActionTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        FileStatus.Done => "打开",
        FileStatus.Processing => "取消",
        FileStatus.Failed => "重试",
        FileStatus.Unsupported => "移除",
        FileStatus.Skipped => "移除",
        _ => "删除"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileTypeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        FileType.Word => "W",
        FileType.Excel => "X",
        FileType.PowerPoint => "P",
        FileType.PDF => "PDF",
        FileType.Markdown => "M",
        FileType.Text => "TXT",
        _ => "文"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Theme-aware: looks up brushes from Application.Resources so dark mode colors auto-apply.
/// F-09/F-38 fix: No longer caches static brushes.
/// </summary>
public class FileStatusBackgroundConverter : IValueConverter
{
    private static readonly Dictionary<FileStatus, string> ResourceKeys = new()
    {
        [FileStatus.Pending] = "StatusPendingBgBrush",
        [FileStatus.Processing] = "StatusProcessingBgBrush",
        [FileStatus.Done] = "StatusDoneBgBrush",
        [FileStatus.Failed] = "StatusFailedBgBrush",
        [FileStatus.Unsupported] = "StatusUnsupportedBgBrush",
        [FileStatus.Skipped] = "StatusSkippedBgBrush",
    };

    private static readonly Brush FallbackBrush = new SolidColorBrush(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FileStatus status) return FallbackBrush;
        var key = ResourceKeys.GetValueOrDefault(status);
        if (key != null && Application.Current?.Resources[key] is Brush brush)
            return brush;
        return FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileStatusForegroundConverter : IValueConverter
{
    private static readonly Dictionary<FileStatus, string> ResourceKeys = new()
    {
        [FileStatus.Pending] = "StatusPendingFgBrush",
        [FileStatus.Processing] = "StatusProcessingFgBrush",
        [FileStatus.Done] = "StatusDoneFgBrush",
        [FileStatus.Failed] = "StatusFailedFgBrush",
        [FileStatus.Unsupported] = "StatusUnsupportedFgBrush",
        [FileStatus.Skipped] = "StatusSkippedFgBrush",
    };

    private static readonly Brush FallbackBrush = new SolidColorBrush(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FileStatus status) return FallbackBrush;
        var key = ResourceKeys.GetValueOrDefault(status);
        if (key != null && Application.Current?.Resources[key] is Brush brush)
            return brush;
        return FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FileStatusProcessingVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is FileStatus status && status == FileStatus.Processing
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// F-10 fix: Toast background now theme-aware via resources.
/// </summary>
public class ToastBackgroundConverter : IValueConverter
{
    private static readonly Dictionary<string, string> ResourceKeys = new()
    {
        ["success"] = "ToastSuccessBgBrush",
        ["warning"] = "ToastWarningBgBrush",
        ["error"] = "ToastErrorBgBrush",
        ["info"] = "ToastInfoBgBrush",
    };

    private static readonly Brush FallbackBrush = new SolidColorBrush(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var tone = value?.ToString();
        if (tone != null && ResourceKeys.GetValueOrDefault(tone) is string key
            && Application.Current?.Resources[key] is Brush brush)
            return brush;
        return FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ToneBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, string> ResourceKeys = new()
    {
        ["success"] = "ToneSuccessBrush",
        ["warning"] = "ToneWarningBrush",
        ["error"] = "ToneErrorBrush",
        ["info"] = "ToneInfoBrush",
    };

    private static readonly Brush FallbackBrush = new SolidColorBrush(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var tone = value?.ToString();
        if (tone != null && ResourceKeys.GetValueOrDefault(tone) is string key
            && Application.Current?.Resources[key] is Brush brush)
            return brush;
        return FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double percent) return 0d;
        if (!double.TryParse(parameter?.ToString(), out var totalWidth)) totalWidth = 220d;
        if (double.IsNaN(percent) || double.IsInfinity(percent)) return 0d;
        var clamped = Math.Max(0d, Math.Min(100d, percent));
        return totalWidth * clamped / 100d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
