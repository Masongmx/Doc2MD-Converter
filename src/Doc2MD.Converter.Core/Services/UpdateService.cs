using System.Net.Http;
using System.Text.Json;

namespace Doc2MD.Services;

/// <summary>F6: GitHub Releases 自动更新检查结果。</summary>
public sealed class UpdateCheckResult
{
    /// <summary>是否存在可用新版本。</summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>远端最新版本号（规范化后的 tag，如 2.1.0）。</summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>当前本地版本号。</summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>Release 页面地址（html_url）。</summary>
    public string ReleaseUrl { get; init; } = string.Empty;

    /// <summary>首选 Windows 资产下载地址（browser_download_url），可能为空。</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Release 说明（body），可能为空。</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>检查是否成功（网络/解析失败为 false，IsUpdateAvailable 恒为 false）。</summary>
    public bool Succeeded { get; init; }
}

/// <summary>
/// F6: 基于 GitHub Releases API 的自动更新检查服务。
/// 轮询 <c>releases/latest</c> 接口，解析 tag_name 与当前版本比较；
/// 不自动下载安装，仅提示新版本并跳转下载页（符合离线工具的产品定位）。
/// </summary>
public sealed class UpdateService
{
    /// <summary>
    /// 发布仓库 Owner。
    /// 注意：此为占位值，项目开源后请改为真实 GitHub 仓库名，
    /// 否则更新检查会请求不存在的仓库并静默跳过。
    /// </summary>
    public const string RepositoryOwner = "Doc2MD-Converter";

    /// <summary>
    /// 发布仓库 Name。
    /// 注意：此为占位值，项目开源后请改为真实 GitHub 仓库名。
    /// </summary>
    public const string RepositoryName = "Doc2MD-Converter";

    /// <summary>默认 Release 页面地址（无 tag 时兜底）。</summary>
    public static string DefaultReleasesUrl =>
        $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Doc2MD-Converter");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>检查 GitHub Releases 是否有比当前版本更新的版本。</summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LoggingService.Warning($"[Update] GitHub API 返回 {(int)response.StatusCode}，跳过更新检查");
                return new UpdateCheckResult { CurrentVersion = Doc2MD.Constants.AppVersion.Version };
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var root = doc.RootElement;

            var tagName = GetString(root, "tag_name");
            var releaseUrl = GetString(root, "html_url");
            var notes = GetString(root, "body");
            var downloadUrl = FindWindowsAssetUrl(root);

            var isAvailable = IsNewerVersion(tagName, Doc2MD.Constants.AppVersion.Version);

            return new UpdateCheckResult
            {
                IsUpdateAvailable = isAvailable,
                LatestVersion = NormalizeTag(tagName),
                CurrentVersion = Doc2MD.Constants.AppVersion.Version,
                ReleaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? DefaultReleasesUrl : releaseUrl,
                DownloadUrl = downloadUrl,
                ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                Succeeded = true
            };
        }
        catch (Exception ex)
        {
            LoggingService.Warning($"[Update] 检查更新失败: {ex.Message}");
            return new UpdateCheckResult
            {
                CurrentVersion = Doc2MD.Constants.AppVersion.Version,
                Succeeded = false
            };
        }
    }

    private static string? FindWindowsAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(url)) continue;

            // 优先 .exe / .msix，其次 .zip；跳过 .dll / .pdb / .nupkg 等
            var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            if (ext is ".exe" or ".msix" or ".msixbundle" or ".zip")
            {
                return url;
            }
        }

        return null;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>把 tag（如 v2.1.0、release-2.1.0）规范化为纯版本号。</summary>
    public static string NormalizeTag(string tag)
    {
        var version = TryParseVersion(tag);
        return version?.ToString() ?? tag.TrimStart('v');
    }

    /// <summary>判断远端 tag 对应的版本是否严格新于本地版本（无解析结果的返回 false）。</summary>
    public static bool IsNewerVersion(string? remoteTag, string? localVersion)
    {
        var remote = TryParseVersion(remoteTag);
        var local = TryParseVersion(localVersion);
        return remote != null && local != null && remote > local;
    }

    /// <summary>从任意 tag 文本中提取首个 X.Y.Z(.W) 版本片段。</summary>
    public static Version? TryParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 提取首个 X.Y.Z(.W) 片段，忽略前后缀
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+\.\d+(\.\d+)?(\.\d+)?");
        return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
    }
}
