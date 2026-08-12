using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// F6: GitHub Releases 自动更新检查服务单元测试。
/// 覆盖版本 tag 解析、规范化与版本新旧比较逻辑（网络请求不在单元测试范围内）。
/// </summary>
public class UpdateServiceTests
{
    // === TryParseVersion: 从任意 tag 文本提取版本号 ===

    [Theory]
    [InlineData("v2.1.0", "2.1.0")]
    [InlineData("2.1.0", "2.1.0")]
    [InlineData("release-3.0.1", "3.0.1")]
    [InlineData("v1.0", "1.0")]
    [InlineData("V2.0.0", "2.0.0")]
    public void TryParseVersion_ValidTag_ExtractsVersion(string tag, string expected)
    {
        var version = UpdateService.TryParseVersion(tag);
        Assert.NotNull(version);
        Assert.Equal(expected, version!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("latest")]
    [InlineData("not-a-version")]
    public void TryParseVersion_InvalidText_ReturnsNull(string? tag)
    {
        Assert.Null(UpdateService.TryParseVersion(tag));
    }

    // === NormalizeTag: 版本 tag 规范化 ===

    [Theory]
    [InlineData("v2.1.0", "2.1.0")]
    [InlineData("release-2.1.0", "2.1.0")]
    [InlineData("2.0.0", "2.0.0")]
    public void NormalizeTag_StripsPrefixes(string tag, string expected)
    {
        Assert.Equal(expected, UpdateService.NormalizeTag(tag));
    }

    // === IsNewerVersion: 新旧版本比较 ===

    [Theory]
    [InlineData("v2.1.0", "2.0.0", true)]   // 小版本升级
    [InlineData("3.0.0", "2.9.9", true)]   // 大版本升级
    [InlineData("2.0.1", "2.0.0", true)]   // 补丁升级
    [InlineData("2.0.0", "2.0.0", false)]  // 版本相同
    [InlineData("1.9.0", "2.0.0", false)]  // 远端更旧
    [InlineData("v2.0.0", "2.1.0", false)] // 本地更新
    [InlineData(null, "2.0.0", false)]     // 远端无版本
    [InlineData("2.0.0", null, false)]     // 本地无版本
    public void IsNewerVersion_ComparesCorrectly(string? remote, string? local, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewerVersion(remote, local));
    }
}
