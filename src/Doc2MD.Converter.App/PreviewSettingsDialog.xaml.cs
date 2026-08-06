using System.Windows;
using Doc2MD.Models;
using Doc2MD.Pipeline.Services;
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

    private void ApplyPipelineTemplate_Click(object sender, RoutedEventArgs e)
    {
        var templateId = ViewModel.Settings.Preview.MarkdownToDocx.PipelineTemplateId;
        var templateService = new TemplateService();
        var template = templateService.GetTemplate(templateId);
        var opts = template.Options;

        // 将 Pipeline 模板参数填充到 UI 设置字段
        var settings = ViewModel.Settings.Preview.MarkdownToDocx;
        settings.TitleFont = opts.TitleFont;
        settings.HeadingFont = opts.Heading1Font;
        settings.BodyFont = opts.BodyFont;
        settings.SubheadingFont = opts.Heading2Font;
        settings.CodeBlockFont = "Consolas";
        settings.TitleFontSizePt = opts.TitleFontSizePt;
        settings.HeadingFontSizePt = opts.Heading1FontSizePt;
        settings.SubheadingFontSizePt = opts.Heading2FontSizePt;
        settings.BodyFontSizePt = opts.BodyFontSizePt;
        settings.CodeBlockFontSizePt = 10.5;
        settings.LineSpacingPt = opts.LineSpacingPt;
        settings.FirstLineIndentChars = opts.FirstLineIndentChars;
        settings.BeforeSpacingPt = opts.BeforeSpacingPt;
        settings.AfterSpacingPt = opts.AfterSpacingPt;
        settings.PageMarginTopCm = opts.PageMarginTopCm;
        settings.PageMarginBottomCm = opts.PageMarginBottomCm;
        settings.PageMarginLeftCm = opts.PageMarginLeftCm;
        settings.PageMarginRightCm = opts.PageMarginRightCm;

        ViewModel.NotifySettingsChanged();
        MessageBox.Show($"已应用模板: {template.Name}\n{template.Metadata.Description}", "模板已应用", MessageBoxButton.OK, MessageBoxImage.Information);
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
