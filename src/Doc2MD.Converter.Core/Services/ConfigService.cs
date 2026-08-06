using System.IO;
using System.Text.Json;
using Doc2MD.Models;

namespace Doc2MD.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig _config;

    public AppConfig Config => _config;

    public ConfigService()
    {
        _configPath = AppPaths.ConfigPath;
        _config = Load();
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                return Normalize(Deserialize(File.ReadAllText(_configPath)));
            }

            if (File.Exists(AppPaths.LegacyConfigPath))
            {
                return Normalize(DeserializeLegacy(File.ReadAllText(AppPaths.LegacyConfigPath)));
            }
        }
        catch (Exception ex)
        {
            LoggingService.Error($"加载配置文件失败: {_configPath}", ex);
        }

        return new AppConfig();
    }

    private static AppConfig Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private static AppConfig DeserializeLegacy(string json)
    {
        var legacy = JsonSerializer.Deserialize<LegacyAppConfig>(json) ?? new LegacyAppConfig();
        return new AppConfig
        {
            General = new GeneralSettings
            {
                DefaultOutputDir = legacy.OutputDirectory,
                AutoOpenOutputDir = legacy.OpenOutputFolder
            },
            Conversion = new ConversionSettings
            {
                RecursiveScan = legacy.IncludeSubfolders,
                PreserveFolderStructure = legacy.PreserveStructure
            }
        };
    }

    private static AppConfig Normalize(AppConfig config)
    {
        config.General ??= new GeneralSettings();
        config.Appearance ??= new AppearanceSettings();
        config.Conversion ??= new ConversionSettings();
        config.Templates ??= new TemplateSettings();
        config.Preview ??= new PreviewSettings();
        config.Preview.MarkdownToDocx ??= new MarkdownToDocxPreviewSettings();
        config.Preview.DocumentToMarkdown ??= new DocumentToMarkdownPreviewSettings();
        config.Preview.FormatDoc ??= new FormatDocPreviewSettings();
        config.Recent ??= new RecentState();
        config.Recent.RecentFolders ??= new List<string>();
        config.Recent.RecentOutputDirectories ??= new List<string>();

        if (config.Conversion.MaxConcurrentTasks < 1)
        {
            config.Conversion.MaxConcurrentTasks = 1;
        }

        return config;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, options));
        }
        catch (Exception ex)
        {
            LoggingService.Error($"保存配置文件失败: {_configPath}", ex);
        }
    }

    public void Update(Action<AppConfig> updateAction)
    {
        updateAction(_config);
        _config = Normalize(_config);
        Save();
    }

    public void Reload()
    {
        _config = Load();
    }

    public void RememberRecentFolder(string folderPath)
    {
        Remember(_config.Recent.RecentFolders, folderPath);
        Save();
    }

    public void RememberRecentOutputDirectory(string directory)
    {
        Remember(_config.Recent.RecentOutputDirectories, directory);
        Save();
    }

    private static void Remember(List<string> collection, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        collection.RemoveAll(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
        collection.Insert(0, value);

        while (collection.Count > 10)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }

    private sealed class LegacyAppConfig
    {
        public string OutputDirectory { get; set; } = string.Empty;
        public bool IncludeSubfolders { get; set; } = true;
        public bool OpenOutputFolder { get; set; } = false;
        public bool PreserveStructure { get; set; } = true;
    }
}
