using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Failures;

/// <summary>
/// 失败处理与重试门面编排器
/// </summary>
public class FailureOrchestrator
{
    private readonly IFailureDiagnoser _diagnoser;
    private readonly RetryOrchestrator _retryOrchestrator;
    private readonly IFailureRepository _repository;

    public FailureOrchestrator(
        IFailureDiagnoser? diagnoser = null,
        RetryOrchestrator? retryOrchestrator = null,
        IFailureRepository? repository = null)
    {
        _diagnoser = diagnoser ?? new RuleBasedFailureDiagnoser();
        _retryOrchestrator = retryOrchestrator ?? new RetryOrchestrator();
        _repository = repository ?? new JsonFailureRepository();
    }

    public IFailureRepository Repository => _repository;
    public IFailureDiagnoser Diagnoser => _diagnoser;
    public RetryOrchestrator RetryOrchestrator => _retryOrchestrator;

    /// <summary>
    /// 处理单次转换失败：诊断原因、挂载上下文、持久化到知识库
    /// </summary>
    public FailureContext HandleFailure(ConversionResult result, Exception? ex, string filePath, int attemptCount = 1)
    {
        var context = _diagnoser.Diagnose(ex, filePath, result.ErrorMessage);
        context.AttemptCount = attemptCount;
        context.FileSha256 = result.Metadata.SourceFileHashSha256;

        result.FailureContext = context;
        result.ErrorMessage = $"{context.Category.GetDescription()}: {context.ErrorMessage}";

        // 异步或安全持久化到失败库
        try
        {
            var record = FailureRecord.FromContext(context);
            _repository.Save(record);
        }
        catch (Exception repoEx)
        {
            LoggingService.Warning($"[FailureOrchestrator] 保存失败记录异常: {repoEx.Message}");
        }

        return context;
    }
}
