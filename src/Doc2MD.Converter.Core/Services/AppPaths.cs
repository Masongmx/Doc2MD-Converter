using System.IO;

namespace Doc2MD.Services;

public static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Doc2MD");

    public static string ConfigPath { get; } = Path.Combine(AppDataDirectory, "app_config.json");
    public static string LegacyConfigPath { get; } = Path.Combine(AppDataDirectory, "config.json");
    public static string LogDirectory { get; } = Path.Combine(AppDataDirectory, "logs");
}
