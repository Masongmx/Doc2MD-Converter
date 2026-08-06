using System.Windows;
using Doc2MD.ViewModels;

namespace Doc2MD;

public partial class HelpDialog : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public HelpDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SelectedHelpSection = "使用指南";
    }

    private void GoMarkdownToDocx_Click(object sender, RoutedEventArgs e) => ViewModel.SelectedHelpSection = "Markdown 转 DOCX";
    private void GoDocToMarkdown_Click(object sender, RoutedEventArgs e) => ViewModel.SelectedHelpSection = "文档转 Markdown";
    private void GoFormatDoc_Click(object sender, RoutedEventArgs e) => ViewModel.SelectedHelpSection = "一键排版";
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => ViewModel.OpenLogsDirectory();
    private void CopyInfo_Click(object sender, RoutedEventArgs e) => ViewModel.CopySoftwareInfo();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
