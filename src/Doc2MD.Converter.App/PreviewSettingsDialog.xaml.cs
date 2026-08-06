using System.Windows;
using Doc2MD.Models;
using Doc2MD.Services;
using Doc2MD.ViewModels;
using Microsoft.Win32;

namespace Doc2MD;

public partial class PreviewSettingsDialog : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public PreviewSettingsDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    // === MarkdownToDocx 方案操作 ===

    private void ApplyProfile_Click_md2docx(object sender, RoutedEventArgs e)
    {
        var schemeName = ViewModel.Settings.Preview.MarkdownToDocx.FormatScheme;
        var profile = FormattingProfile.GetBuiltIn(schemeName);
        FormattingProfileService.ApplyTo(profile, ViewModel.Settings.Preview.MarkdownToDocx);
        ViewModel.NotifySettingsChanged();
    }

    private void ExportProfile_Click_md2docx(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "排版方案文件|*.doc2md-profile.json",
            DefaultExt = ".doc2md-profile.json",
            FileName = "自定义排版方案"
        };
        if (dialog.ShowDialog() == true)
        {
            var profile = FormattingProfileService.ExtractProfile(ViewModel.Settings.Preview.MarkdownToDocx);
            if (FormattingProfileService.SaveProfileToFile(profile, dialog.FileName))
            {
                MessageBox.Show($"排版方案已导出到:\n{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ImportProfile_Click_md2docx(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "排版方案文件|*.doc2md-profile.json|JSON 文件|*.json|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            var profile = FormattingProfileService.LoadProfileFromFile(dialog.FileName);
            if (profile != null)
            {
                FormattingProfileService.ApplyTo(profile, ViewModel.Settings.Preview.MarkdownToDocx);
                ViewModel.Settings.Preview.MarkdownToDocx.FormatScheme = FormattingProfile.Custom;
                ViewModel.NotifySettingsChanged();
                MessageBox.Show($"已导入方案: {profile.Name}\n{profile.Description}", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("无法读取排版方案文件，请检查文件格式。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // === FormatDoc 方案操作 ===

    private void ApplyProfile_Click_formatdoc(object sender, RoutedEventArgs e)
    {
        var schemeName = ViewModel.Settings.Preview.FormatDoc.FormatScheme;
        var profile = FormattingProfile.GetBuiltIn(schemeName);
        FormattingProfileService.ApplyTo(profile, ViewModel.Settings.Preview.FormatDoc);
        ViewModel.NotifySettingsChanged();
    }

    private void ExportProfile_Click_formatdoc(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "排版方案文件|*.doc2md-profile.json",
            DefaultExt = ".doc2md-profile.json",
            FileName = "自定义排版方案"
        };
        if (dialog.ShowDialog() == true)
        {
            var profile = FormattingProfileService.ExtractProfile(ViewModel.Settings.Preview.FormatDoc);
            if (FormattingProfileService.SaveProfileToFile(profile, dialog.FileName))
            {
                MessageBox.Show($"排版方案已导出到:\n{dialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ImportProfile_Click_formatdoc(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "排版方案文件|*.doc2md-profile.json|JSON 文件|*.json|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            var profile = FormattingProfileService.LoadProfileFromFile(dialog.FileName);
            if (profile != null)
            {
                FormattingProfileService.ApplyTo(profile, ViewModel.Settings.Preview.FormatDoc);
                ViewModel.Settings.Preview.FormatDoc.FormatScheme = FormattingProfile.Custom;
                ViewModel.NotifySettingsChanged();
                MessageBox.Show($"已导入方案: {profile.Name}\n{profile.Description}", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("无法读取排版方案文件，请检查文件格式。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // === 通用 ===

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PersistSettings("预览设置已保存");
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("未保存的更改将丢失，确定关闭吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        ViewModel.ReloadSettings();
        Close();
    }
}
