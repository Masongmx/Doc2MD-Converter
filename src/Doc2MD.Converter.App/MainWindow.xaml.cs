using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Doc2MD.Models;
using Doc2MD.ViewModels;

namespace Doc2MD;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
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

    private async void ModeToMarkdown_Click(object sender, RoutedEventArgs e) => await ViewModel.SwitchModeAsync(AppMode.ToMarkdown);
    private async void ModeMarkdownToDocx_Click(object sender, RoutedEventArgs e) => await ViewModel.SwitchModeAsync(AppMode.MarkdownToDocx);
    private async void ModeFormatDoc_Click(object sender, RoutedEventArgs e) => await ViewModel.SwitchModeAsync(AppMode.FormatDoc);
    private async void AddFiles_Click(object sender, RoutedEventArgs e) => await ViewModel.BrowseFilesAsync();
    private async void AddFolder_Click(object sender, RoutedEventArgs e) => await ViewModel.BrowseFolderAsync();
    private void ClearFiles_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveFiles.Count > 0 &&
            MessageBox.Show("确定清空文件列表？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        ViewModel.ClearFiles();
    }
    private async void RefreshFolder_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshFolderAsync();
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => ViewModel.OpenCurrentFolder();
    private async void ChangeFolder_Click(object sender, RoutedEventArgs e) => await ViewModel.BrowseFolderAsync();
    private void BrowseOutput_Click(object sender, RoutedEventArgs e) => ViewModel.BrowseOutputDirectory();
    private void OpenOutput_Click(object sender, RoutedEventArgs e) => ViewModel.OpenOutputDirectory();
    private async void PrimaryAction_Click(object sender, RoutedEventArgs e) => await ViewModel.StartProcessingAsync();
    private void CancelProcessing_Click(object sender, RoutedEventArgs e) => ViewModel.CancelProcessing();

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
