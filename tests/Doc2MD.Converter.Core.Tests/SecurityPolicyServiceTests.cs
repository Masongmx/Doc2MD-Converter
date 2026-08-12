using Doc2MD.Models;
using Doc2MD.Services;
using Xunit;

namespace Doc2MD.Tests;

/// <summary>
/// 安全策略服务单元测试（审查报告 S2 等）：
/// 覆盖 Windows 保留名防护、路径穿越净化、目录隔离、覆盖保护与类型/大小限制。
/// </summary>
public class SecurityPolicyServiceTests
{
    // === S2: Windows 保留设备名防护 ===

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    public void SanitizeFileName_ReservedDeviceNames_GetsUnderscorePrefix(string reservedName)
    {
        var result = SecurityPolicyService.SanitizeFileName(reservedName);
        Assert.StartsWith("_", result);
        Assert.EndsWith(reservedName, result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("nul.md")]
    [InlineData("COM3.docx")]
    [InlineData("LPT2.pdf")]
    public void SanitizeFileName_ReservedNameWithExtension_GetsUnderscorePrefix(string reservedName)
    {
        var result = SecurityPolicyService.SanitizeFileName(reservedName);
        Assert.StartsWith("_", result);
        Assert.EndsWith(reservedName, result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("报告.docx", "报告.docx")]
    [InlineData("CONFIDENTIAL.txt", "CONFIDENTIAL.txt")]   // 前缀含 CON 但不是保留名
    [InlineData("committee.md", "committee.md")]            // 包含 com 子串但不是 COM1-9
    public void SanitizeFileName_NormalNames_Unchanged(string input, string expected)
    {
        Assert.Equal(expected, SecurityPolicyService.SanitizeFileName(input));
    }

    [Fact]
    public void SanitizeFileName_PathTraversal_StripsDirectories()
    {
        Assert.Equal("safe.md", SecurityPolicyService.SanitizeFileName(@"..\..\safe.md"));
        Assert.Equal("a.txt", SecurityPolicyService.SanitizeFileName(@"C:\temp\a.txt"));
    }

    // === 路径允许与隔离 ===

    [Fact]
    public void IsPathAllowed_LocalContext_AllowsAll()
    {
        var policy = new SecurityPolicy { IsLocalContext = true };
        Assert.True(SecurityPolicyService.IsPathAllowed(@"C:\any\where\file.txt", policy));
    }

    [Fact]
    public void IsPathAllowed_NonLocalEmptyDirectories_RejectsAll()
    {
        var policy = new SecurityPolicy { IsLocalContext = false };
        Assert.False(SecurityPolicyService.IsPathAllowed(@"C:\any\where\file.txt", policy));
    }

    [Fact]
    public void IsPathAllowed_WithinAllowedDirectory_Allows()
    {
        var policy = new SecurityPolicy
        {
            IsLocalContext = true,
            AllowedDirectories = { @"C:\work\docs" }
        };
        Assert.True(SecurityPolicyService.IsPathAllowed(@"C:\work\docs\a\b.txt", policy));
        Assert.False(SecurityPolicyService.IsPathAllowed(@"C:\work\docs_evil\b.txt", policy));
    }

    [Fact]
    public void IsOutputIsolated_RejectsNestedUnderSourceOrOutput()
    {
        // 输出路径是源文件路径的子路径 → 不隔离
        Assert.False(SecurityPolicyService.IsOutputIsolated(@"C:\in\a.docx", @"C:\in\a.docx\out\a.md"));
        // 源路径是输出路径的子路径 → 不隔离
        Assert.False(SecurityPolicyService.IsOutputIsolated(@"C:\out\a.md\in\a.docx", @"C:\out\a.md"));
        // 完全不同的路径 → 隔离
        Assert.True(SecurityPolicyService.IsOutputIsolated(@"C:\in\a.docx", @"C:\in\out\a.md"));
        Assert.True(SecurityPolicyService.IsOutputIsolated(@"C:\in\a.docx", @"D:\out\a.md"));
    }

    // === 覆盖保护 ===

    [Fact]
    public void WouldOverwrite_AllowsWhenPermitted()
    {
        var policy = new SecurityPolicy { AllowOverwrite = true };
        Assert.False(SecurityPolicyService.WouldOverwrite(Path.Combine(Path.GetTempPath(), "missing_file_for_test.txt"), policy));
    }

    // === 文件类型与大小 ===

    [Fact]
    public void IsFileTypeAllowed_RespectsAllowList()
    {
        var policy = new SecurityPolicy { AllowedFileTypes = { ".docx", ".md" } };
        Assert.True(SecurityPolicyService.IsFileTypeAllowed("a.docx", policy));
        Assert.False(SecurityPolicyService.IsFileTypeAllowed("a.pdf", policy));
    }

    [Fact]
    public void IsFileTypeAllowed_EmptyAllowList_AllowsAll()
    {
        Assert.True(SecurityPolicyService.IsFileTypeAllowed("a.anything", new SecurityPolicy()));
    }

    [Fact]
    public void IsFileSizeAllowed_RespectsLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"size_{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(path, new byte[100]);
            Assert.True(SecurityPolicyService.IsFileSizeAllowed(path, new SecurityPolicy { MaxFileSizeBytes = 200 }));
            Assert.False(SecurityPolicyService.IsFileSizeAllowed(path, new SecurityPolicy { MaxFileSizeBytes = 50 }));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void IsFileSizeAllowed_MissingFile_ReturnsFalse()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no_such_file_anywhere.txt");
        Assert.False(SecurityPolicyService.IsFileSizeAllowed(missing, new SecurityPolicy { MaxFileSizeBytes = 100 }));
    }
}
