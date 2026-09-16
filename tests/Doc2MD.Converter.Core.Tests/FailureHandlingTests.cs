using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Doc2MD.Failures;
using Doc2MD.Models;
using Xunit;

namespace Doc2MD.Converter.Core.Tests;

public class FailureHandlingTests
{
    [Fact]
    public void PathSanitizer_ShouldAnonymizeUserPath()
    {
        var input = @"C:\Users\JohnDoe\Documents\Secret\doc1.docx";
        var sanitized = PathSanitizer.Anonymize(input);
        Assert.DoesNotContain("JohnDoe", sanitized);
        Assert.Contains(@"C:\Users\***\Documents\Secret\doc1.docx", sanitized);
    }

    [Fact]
    public void PathSanitizer_ShouldHandleEmptyAndNull()
    {
        Assert.Equal(string.Empty, PathSanitizer.Anonymize(null));
        Assert.Equal(string.Empty, PathSanitizer.Anonymize(""));
        Assert.Equal(string.Empty, PathSanitizer.SanitizeDirectory(null));
    }

    [Theory]
    [InlineData("The process cannot access the file because it is being used by another process", typeof(IOException), FailureCategory.FileLocked)]
    [InlineData("File is locked by Word", typeof(IOException), FailureCategory.FileLocked)]
    [InlineData("Password protected document", typeof(InvalidOperationException), FailureCategory.SourceFileEncrypted)]
    [InlineData("The file is encrypted", typeof(Exception), FailureCategory.SourceFileEncrypted)]
    [InlineData("LibreOffice is not found in path", typeof(Exception), FailureCategory.DependencyMissing)]
    [InlineData("tesseract OCR engine not found", typeof(Exception), FailureCategory.DependencyMissing)]
    [InlineData("Out of memory", typeof(OutOfMemoryException), FailureCategory.ResourceExhausted)]
    [InlineData("Unknown unsupported format", typeof(NotSupportedException), FailureCategory.FormatUnsupported)]
    [InlineData("The package XML is corrupt or invalid", typeof(Exception), FailureCategory.SourceFileCorrupted)]
    public void RuleBasedFailureDiagnoser_ShouldIdentifyCorrectCategory(string errorMsg, Type exceptionType, FailureCategory expectedCategory)
    {
        var diagnoser = new RuleBasedFailureDiagnoser();
        var ex = (Exception)Activator.CreateInstance(exceptionType, errorMsg)!;

        var context = diagnoser.Diagnose(ex, @"C:\test\sample.docx", errorMsg);

        Assert.Equal(expectedCategory, context.Category);
        Assert.Equal("sample.docx", context.SourceFileName);
        Assert.False(string.IsNullOrWhiteSpace(context.SuggestedAction));
    }

    [Fact]
    public void RuleBasedFailureDiagnoser_ShouldIdentifyCancellation()
    {
        var diagnoser = new RuleBasedFailureDiagnoser();
        var ex = new OperationCanceledException();

        var context = diagnoser.Diagnose(ex, @"C:\test\sample.docx");

        Assert.Equal(FailureCategory.UserCancelled, context.Category);
        Assert.False(context.IsRetryable);
    }

    [Fact]
    public void JsonFailureRepository_ShouldRecordAndRetrieveFailures()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"fail_repo_test_{Guid.NewGuid():N}.json");
        try
        {
            var repo = new JsonFailureRepository(tempFile);
            var record = new FailureRecord
            {
                FailureId = Guid.NewGuid().ToString("N"),
                SourceFileName = "test.docx",
                SourceDirectory = @"C:\Users\***\docs",
                Category = FailureCategory.FileLocked,
                RootCause = "Locked",
                SuggestedAction = "Close file",
                TimestampUtc = DateTime.UtcNow
            };

            repo.Save(record);

            var recent = repo.GetAll();
            Assert.Single(recent);
            Assert.Equal("test.docx", recent[0].SourceFileName);
            Assert.Equal(FailureCategory.FileLocked, recent[0].Category);

            var filtered = repo.QueryByCategory(FailureCategory.FileLocked);
            Assert.Single(filtered);

            repo.Clear();
            var afterClear = repo.GetAll();
            Assert.Empty(afterClear);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RetryOrchestrator_ShouldExecuteMatchingStrategy()
    {
        var orchestrator = new RetryOrchestrator();
        var context = new RetryContext
        {
            Diagnosis = new FailureDiagnosis
            {
                Category = FailureCategory.DependencyMissing,
                RootCause = "LibreOffice not found",
                SuggestedAction = "Install LibreOffice",
                IsRetryable = false
            },
            FilePath = "sample.pdf",
            OutputDirectory = Path.GetTempPath(),
            AttemptCount = 1
        };

        var result = await orchestrator.ExecuteStrategyAsync(context, CancellationToken.None);

        Assert.False(result.ShouldRetryImmediately);
        Assert.True(result.RequiresUserAction);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void FailureOrchestrator_ShouldDiagnoseAndRecordOnFailure()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"orch_test_{Guid.NewGuid():N}.json");
        try
        {
            var repo = new JsonFailureRepository(tempFile);
            var diagnoser = new RuleBasedFailureDiagnoser();
            var orchestrator = new FailureOrchestrator(diagnoser, new RetryOrchestrator(), repo);

            var ex = new IOException("The process cannot access the file because it is being used by another process");
            var conversionResult = new ConversionResult();
            var context = orchestrator.HandleFailure(conversionResult, ex, @"C:\Users\Bob\test.docx", 1);

            Assert.Equal(FailureCategory.FileLocked, context.Category);
            Assert.NotNull(conversionResult.FailureContext);

            var recent = repo.GetAll();
            Assert.Single(recent);
            Assert.Equal("test.docx", recent[0].SourceFileName);
            Assert.DoesNotContain("Bob", recent[0].SourceDirectory);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
