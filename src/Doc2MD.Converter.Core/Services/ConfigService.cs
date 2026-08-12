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

    /// <summary>配置文件加载失败时为 true，供 UI 层提示用户。</summary>
    public bool WasLoadCorrupted { get; private set; }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = Normalize(Deserialize(json));
                UpgradeFromLegacy(json, config);
                return config;
            }

            if (File.Exists(AppPaths.LegacyConfigPath))
            {
                return Normalize(DeserializeLegacy(File.ReadAllText(AppPaths.LegacyConfigPath)));
            }
        }
        catch (Exception ex)
        {
            LoggingService.Error($"加载配置文件失败，已重置为默认设置: {_configPath}", ex);
            WasLoadCorrupted = true;
        }

        return new AppConfig();
    }

    private static AppConfig Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    /// <summary>
    /// 旧版本配置升级：旧配置文件中没有 HasCompletedOnboarding 字段，说明是升级用户而非新用户，
    /// 直接视为已完成首次引导，避免升级后每次启动都弹出全屏引导浮层（浮层会遮黑标题栏并拦截按钮点击）。
    /// </summary>
    private static void UpgradeFromLegacy(string json, AppConfig config)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var hasFlag = doc.RootElement.TryGetProperty("General", out var general)
                          && general.TryGetProperty("HasCompletedOnboarding", out _);
            if (!hasFlag)
            {
                config.General.HasCompletedOnboarding = true;
            }
        }
        catch
        {
            // 解析失败时保持默认行为（由 Normalize 兜底）
        }
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
        config.Recent.RecentConversions ??= new List<ConversionRecord>();

        if (config.Conversion.MaxConcurrentTasks < 1)
        {
            config.Conversion.MaxConcurrentTasks = 1;
        }

        if (config.Conversion.MaxScanFileCount < 1)
        {
            config.Conversion.MaxScanFileCount = 5000;
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

    /// <summary>F4: 记录一条转换历史，保留最近 20 条并立即持久化。</summary>
    public void RememberConversion(ConversionRecord record)
    {
        if (record == null) return;

        var history = _config.Recent.RecentConversions;
        history.Insert(0, record);
        while (history.Count > 20)
        {
            history.RemoveAt(history.Count - 1);
        }
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
