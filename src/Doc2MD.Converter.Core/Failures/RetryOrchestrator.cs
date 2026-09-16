namespace Doc2MD.Failures;

/// <summary>
/// 重试策略编排器
/// </summary>
public class RetryOrchestrator
{
    private readonly Dictionary<FailureCategory, IRetryStrategy> _strategies = new();

    public RetryOrchestrator(IEnumerable<IRetryStrategy>? strategies = null)
    {
        if (strategies != null)
        {
            foreach (var s in strategies)
            {
                _strategies[s.AppliesTo] = s;
            }
        }
        else
        {
            // 注册默认策略集合
            Register(new SkipAndMarkStrategy());
            Register(new InstallDependencyStrategy());
            Register(new OcrFallbackStrategy());
            Register(new UnlockAndRetryStrategy());
            Register(new DowngradeConcurrencyStrategy());
            Register(new RetryOnceStrategy());
        }
    }

    public void Register(IRetryStrategy strategy)
    {
        _strategies[strategy.AppliesTo] = strategy;
    }

    public async Task<RetryExecutionResult> ExecuteStrategyAsync(RetryContext context, CancellationToken cancellationToken)
    {
        if (_strategies.TryGetValue(context.Diagnosis.Category, out var strategy))
        {
            return await strategy.ExecuteAsync(context, cancellationToken);
        }

        // 默认不自动重试
        return new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = false,
            Message = context.Diagnosis.SuggestedAction
        };
    }
}
