using System.Windows;
using Doc2MD.ViewModels;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace Doc2MD;

public partial class SettingsDialog : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public SettingsDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (string.IsNullOrEmpty(viewModel.SelectedSettingsSection))
        {
            viewModel.SelectedSettingsSection = "通用设置";
        }
    }

    private void BrowseDefaultOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择默认输出目录" };
        if (dialog.ShowDialog() == true)
        {
            ViewModel.Settings.General.DefaultOutputDir = dialog.FolderName;
            ViewModel.NotifySettingsChanged();
        }
    }

    private void OpenDefaultOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = ViewModel.Settings.General.DefaultOutputDir;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }

    private void BrowseDefaultTemplate_Click(object sender, RoutedEventArgs e)
    {
        BrowseTemplate(path => ViewModel.Settings.Templates.DefaultDocxTemplate = path);
        ViewModel.NotifySettingsChanged();
    }

    private void BrowseOfficialTemplate_Click(object sender, RoutedEventArgs e)
    {
        BrowseTemplate(path => ViewModel.Settings.Templates.OfficialDocTemplate = path);
        ViewModel.NotifySettingsChanged();
    }

    private void ResetDefaultTemplate_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Settings.Templates.DefaultDocxTemplate = string.Empty;
        ViewModel.NotifySettingsChanged();
    }

    private void ResetOfficialTemplate_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Settings.Templates.OfficialDocTemplate = string.Empty;
        ViewModel.NotifySettingsChanged();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenLogsDirectory();
    }

    private void OpenLicenses_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("当前版本暂未内置开源许可页。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // 同步模板设置到对应的 PreviewSettings
        SyncTemplateSettings();
        ViewModel.PersistSettings();
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 将模板设置同步到 PreviewSettings，使引擎能读取模板路径
    /// </summary>
    private void SyncTemplateSettings()
    {
        var templates = ViewModel.Settings.Templates;
        ViewModel.Settings.Preview.MarkdownToDocx.TemplatePath = templates.DefaultDocxTemplate;
        ViewModel.Settings.Preview.FormatDoc.TemplatePath = templates.OfficialDocTemplate;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("未保存的更改将丢失，确定关闭吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        // Revert settings by reloading from disk
        ViewModel.ReloadSettings();
        Close();
    }

    private static void BrowseTemplate(Action<string> assign)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Word 模板|*.docx;*.dotx|所有文件|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            assign(dialog.FileName);
        }
    }
}
