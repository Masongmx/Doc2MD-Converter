using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Doc2MD.Models;
using Doc2MD.ViewModels;

namespace Doc2MD;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private MainViewModel ViewModel => _viewModel;

    /// <summary>
    /// 通过构造函数注入 ViewModel（DI 容器解析），不再从 DataContext 强制转换。
    /// </summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.NotifyIfConfigCorrupted();
        SyncModeCardSelection();
    }

    /// <summary>F5: 全局快捷键。Ctrl+O 添加文件，Ctrl+Shift+O 添加文件夹，F5 刷新，
    /// Ctrl+Enter 开始转换，Escape 取消处理，Ctrl+Z 撤销清空。</summary>
    protected override async void OnPreviewKeyDown(KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (ctrl && shift && e.Key == Key.O)
        {
            e.Handled = true;
            await ViewModel.BrowseFolderAsync();
            return;
        }
        if (ctrl && e.Key == Key.O)
        {
            e.Handled = true;
            await ViewModel.BrowseFilesAsync();
            return;
        }
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            await ViewModel.RefreshFolderAsync();
            return;
        }
        if (ctrl && e.Key == Key.Enter)
        {
            e.Handled = true;
            await ViewModel.StartProcessingAsync();
            return;
        }
        if (e.Key == Key.Escape && ViewModel.IsProcessing)
        {
            e.Handled = true;
            ViewModel.CancelProcessing();
            return;
        }
        if (ctrl && e.Key == Key.Z)
        {
            e.Handled = true;
            ViewModel.UndoClearFiles();
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedModeIndex))
        {
            Dispatcher.BeginInvoke(SyncModeCardSelection);
        }
        else if (e.PropertyName is nameof(ViewModel.IsProcessing)
                 or nameof(ViewModel.ProcessCurrent)
                 or nameof(ViewModel.ProcessTotal))
        {
            // U8: 处理中动态更新窗口标题，便于任务栏查看进度
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel.IsProcessing)
                {
                    Title = $"Doc2MD Converter - 正在处理 ({ViewModel.ProcessCurrent}/{ViewModel.ProcessTotal})";
                }
                else
                {
                    Title = "Doc2MD Converter";
                }
            });
        }
        else if (e.PropertyName == nameof(ViewModel.ProcessProgressPercent))
        {
            Dispatcher.BeginInvoke(() =>
            {
                var targetScale = ViewModel.ProcessProgressPercent / 100.0;
                if (ViewModel.IsMotionOff)
                {
                    ProgressBarScale.ScaleX = targetScale;
                }
                else
                {
                    var animation = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        To = targetScale,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    ProgressBarScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                }
            });
        }
        else if (e.PropertyName == nameof(ViewModel.IsScanning))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel.IsScanning && !ViewModel.IsMotionOff)
                {
                    StartShimmerAnimation();
                }
                else
                {
                    StopShimmerAnimation();
                }
            });
        }
        else if (e.PropertyName == nameof(ViewModel.IsToastVisible))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel.IsToastVisible)
                {
                    // Toast appearing
                    if (ViewModel.IsMotionOff)
                    {
                        ToastBorder.Opacity = 1;
                        ToastTranslateTransform.Y = 0;
                    }
                    else
                    {
                        var yAnim = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = -12, To = 0,
                            Duration = TimeSpan.FromMilliseconds(200),
                            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                        };
                        var opAnim = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 0, To = 1,
                            Duration = TimeSpan.FromMilliseconds(200),
                            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                        };
                        ToastBorder.BeginAnimation(Border.OpacityProperty, opAnim);
                        ToastTranslateTransform.BeginAnimation(TranslateTransform.YProperty, yAnim);
                    }
                }
                else
                {
                    // Toast disappearing
                    if (ViewModel.IsMotionOff)
                    {
                        // 关闭动效时直接隐藏 Toast（与动画结束态一致：透明 + 上移）
                        ToastBorder.Opacity = 0;
                        ToastTranslateTransform.Y = -8;
                    }
                    else
                    {
                        var yAnim = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            To = -8,
                            Duration = TimeSpan.FromMilliseconds(150),
                            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                        };
                        var opAnim = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(150),
                            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                        };
                        ToastBorder.BeginAnimation(Border.OpacityProperty, opAnim);
                        ToastTranslateTransform.BeginAnimation(TranslateTransform.YProperty, yAnim);
                    }
                }
            });
        }
    }

    private System.Windows.Media.Animation.Storyboard? _shimmerStoryboard;

    private void StartShimmerAnimation()
    {
        if (_shimmerStoryboard != null) return;

        var shimmerBars = new[] { ShimmerBar1, ShimmerBar2, ShimmerBar3, ShimmerBar4, ShimmerBar5 };
        _shimmerStoryboard = new System.Windows.Media.Animation.Storyboard { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };

        for (int i = 0; i < shimmerBars.Length; i++)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.35,
                To = 0.75,
                Duration = TimeSpan.FromSeconds(1.5),
                BeginTime = TimeSpan.FromSeconds(i * 0.2),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(animation, shimmerBars[i]);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(animation, new PropertyPath(Border.OpacityProperty));
            _shimmerStoryboard.Children.Add(animation);
        }

        _shimmerStoryboard.Begin();
    }

    private void StopShimmerAnimation()
    {
        if (_shimmerStoryboard == null) return;
        _shimmerStoryboard.Stop();
        _shimmerStoryboard = null;

        // Reset opacity
        foreach (var bar in new[] { ShimmerBar1, ShimmerBar2, ShimmerBar3, ShimmerBar4, ShimmerBar5 })
        {
            bar.Opacity = 1;
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        ViewModel.IsDragActive = false;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var dropped = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        await ViewModel.HandleDroppedPathsAsync(dropped);

        // P6: 拖放真实存在的路径后播放成功动画（边框从绿色渐变为主题色）
        if (dropped.Any(path => File.Exists(path) || Directory.Exists(path)))
        {
            PlayDropSuccessAnimation();
        }
    }

    /// <summary>P6: 拖拽区边框从拖拽高亮色（PrimaryBrush）渐变为主题边框色，仅动效开启时播放。</summary>
    private void PlayDropSuccessAnimation()
    {
        if (ViewModel.IsMotionOff || DropZoneBorder == null) return;

        var from = TryGetBrushColor("PrimaryBrush");
        var to = TryGetBrushColor("DropZoneBorderBrush");
        if (from == null || to == null) return;

        var animated = new SolidColorBrush(from.Value);
        DropZoneBorder.BorderBrush = animated;

        var animation = new ColorAnimation
        {
            From = from.Value,
            To = to.Value,
            Duration = TimeSpan.FromMilliseconds(700),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animated.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private static Color? TryGetBrushColor(string resourceKey)
    {
        return Application.Current.TryFindResource(resourceKey) is SolidColorBrush brush
            ? brush.Color
            : null;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            ViewModel.IsDragActive = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        ViewModel.IsDragActive = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    /// <summary>U7: 模式卡片统一点击处理（ModeIndex 即 AppMode 枚举值）。</summary>
    private async void ModeCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.ModeCard card)
        {
            await ViewModel.SwitchModeAsync((AppMode)card.ModeIndex);
        }
    }

    /// <summary>U7: 同步三个模式卡片的选中态。</summary>
    private void SyncModeCardSelection()
    {
        var index = ViewModel.SelectedModeIndex;
        ModeCard0.UpdateSelection(index);
        ModeCard1.UpdateSelection(index);
        ModeCard2.UpdateSelection(index);
    }
    private async void AddFiles_Click(object sender, RoutedEventArgs e) => await ViewModel.BrowseFilesAsync();
    private async void AddFolder_Click(object sender, RoutedEventArgs e) => await ViewModel.BrowseFolderAsync();
    private void ClearFiles_Click(object sender, RoutedEventArgs e)
    {
        // P4: 不再用 MessageBox 阻塞确认，改为 Toast + 3 秒撤销窗口
        ViewModel.ClearFiles();
    }

    private void UndoClear_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UndoClearFiles();
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.HasSelection = FileListView.SelectedItems.Count > 0;
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = FileListView.SelectedItems.Cast<FileItem>().ToList();
        ViewModel.RemoveSelectedFiles(selected);
    }
    private async void RefreshFolder_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshFolderAsync();
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => ViewModel.OpenCurrentFolder();
    private async void ChangeFolder_Click(object sender, RoutedEventArgs e) => await ViewModel.BrowseFolderAsync();
    private void BrowseOutput_Click(object sender, RoutedEventArgs e) => ViewModel.BrowseOutputDirectory();
    private void OpenOutput_Click(object sender, RoutedEventArgs e) => ViewModel.OpenOutputDirectory();
    private async void PrimaryAction_Click(object sender, RoutedEventArgs e) => await ViewModel.StartProcessingAsync();
    private void CancelProcessing_Click(object sender, RoutedEventArgs e) => ViewModel.CancelProcessing();
    private async void PreviewToggle_Click(object sender, RoutedEventArgs e) => await ViewModel.TogglePreviewAsync();
    private void ClosePreview_Click(object sender, RoutedEventArgs e) => ViewModel.ClosePreview();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(ViewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HelpDialog(ViewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void PreviewSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PreviewSettingsDialog(ViewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private async void FileAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FileItem item })
        {
            await ViewModel.HandleFileActionAsync(item);
        }
    }

    private void CopyErrorMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: FileItem item } && !string.IsNullOrWhiteSpace(item.ErrorMessage))
        {
            System.Windows.Clipboard.SetText(item.ErrorMessage);
            ViewModel.ShowToastFeedback("已复制错误信息");
        }
    }

    private void CopyOutputPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: FileItem item })
        {
            ViewModel.CopyOutputPath(item);
        }
    }

    private void CompleteOnboarding_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CompleteOnboarding();
    }

    private async void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsInteractionLocked) return;
        await ViewModel.BrowseFilesAsync();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void ToggleMaximize_Click(object sender, RoutedEventArgs e) => ToggleWindowState();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T target)
            {
                return target;
            }

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
