using System.Collections.ObjectModel;
using Doc2MD.Models;
using Doc2MD.Services;
using Doc2MD.ViewModels;
using Xunit;

namespace Doc2MD.App.Tests;

public class MainViewModelTests
{
    private MainViewModel CreateViewModel()
    {
        var configService = new ConfigService();
        var logger = new FileLoggingService();
        var toastService = new ToastService();
        var conversionService = new ConversionService();
        return new MainViewModel(configService, logger, toastService, conversionService);
    }

    [Fact]
    public void Constructor_InitializesWithDefaultState()
    {
        var vm = CreateViewModel();

        Assert.Equal(AppMode.ToMarkdown, vm.CurrentMode);
        Assert.False(vm.IsProcessing);
        Assert.False(vm.IsScanning);
        Assert.Empty(vm.ActiveFiles);
        Assert.Equal("就绪", vm.StatusText);
    }

    [Theory]
    [InlineData(AppMode.ToMarkdown, "生成 Markdown")]
    [InlineData(AppMode.MarkdownToDocx, "生成 DOCX")]
    [InlineData(AppMode.FormatDoc, "开始排版")]
    public async Task SwitchMode_UpdatesCurrentModeAndActionText(AppMode mode, string expectedAction)
    {
        var vm = CreateViewModel();
        if (mode != vm.CurrentMode)
        {
            await vm.SwitchModeAsync(mode);
        }

        Assert.Equal(mode, vm.CurrentMode);
        Assert.Equal(expectedAction, vm.PrimaryActionText);
    }

    [Fact]
    public void FileSelection_CanTrackSelectedItems()
    {
        var vm = CreateViewModel();
        var item1 = new FileItem { FileName = "test1.docx", FullPath = "C:\\test1.docx", Type = FileType.Word, Status = FileStatus.Pending };
        var item2 = new FileItem { FileName = "test2.docx", FullPath = "C:\\test2.docx", Type = FileType.Word, Status = FileStatus.Pending };

        vm.ActiveFiles.Add(item1);
        vm.ActiveFiles.Add(item2);

        Assert.Equal(2, vm.ActiveFiles.Count);
    }

    [Fact]
    public void ClearFiles_And_UndoClearFiles_RestoresFileList()
    {
        var vm = CreateViewModel();
        var item1 = new FileItem { FileName = "a.docx", FullPath = "C:\\a.docx", Type = FileType.Word, Status = FileStatus.Pending };
        var item2 = new FileItem { FileName = "b.docx", FullPath = "C:\\b.docx", Type = FileType.Word, Status = FileStatus.Pending };

        vm.ActiveFiles.Add(item1);
        vm.ActiveFiles.Add(item2);

        // Clear files
        vm.ClearFiles();
        Assert.Empty(vm.ActiveFiles);
        Assert.True(vm.CanUndoClear);

        // Undo clear
        vm.UndoClearFiles();
        Assert.Equal(2, vm.ActiveFiles.Count);
        Assert.False(vm.CanUndoClear);
    }

    [Fact]
    public void TemplateSettings_SyncAndPersist_ShouldKeepOfficialAndDefaultTemplates()
    {
        var vm = CreateViewModel();
        var customTemplatePath = "C:\\CustomTemplates\\MyGovTemplate.docx";
        var defaultMd2DocxTemplate = "C:\\CustomTemplates\\MyMd2DocxTemplate.docx";

        vm.Settings.Templates.OfficialDocTemplate = customTemplatePath;
        vm.Settings.Templates.DefaultDocxTemplate = defaultMd2DocxTemplate;

        // Persist settings
        vm.PersistSettings();

        // Check if Preview settings got synchronized
        Assert.Equal(customTemplatePath, vm.Settings.Preview.FormatDoc.TemplatePath);
        Assert.Equal(defaultMd2DocxTemplate, vm.Settings.Preview.MarkdownToDocx.TemplatePath);
    }

    [Fact]
    public async Task FormatDocMode_Switch_ShouldUpdateModeCardAndActionText()
    {
        var vm = CreateViewModel();
        await vm.SwitchModeAsync(AppMode.FormatDoc);

        Assert.Equal(AppMode.FormatDoc, vm.CurrentMode);
        Assert.Equal(2, vm.SelectedModeIndex);
        Assert.Equal("开始排版", vm.PrimaryActionText);
        Assert.False(vm.IsPreviewButtonVisible); // Preview button only visible in Md2Docx
    }
}
