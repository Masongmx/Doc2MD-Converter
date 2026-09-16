namespace Doc2MD.Failures;

/// <summary>
/// 加密文件策略：标记为加密，不进行无谓的自动重试，引导用户解密
/// </summary>
public class SkipAndMarkStrategy : IRetryStrategy
{
    public FailureCategory AppliesTo => FailureCategory.SourceFileEncrypted;

    public Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = true,
            Message = "文件已加密，请解除密码保护后重试"
        });
    }
}

/// <summary>
/// 依赖缺失策略：提示用户安装对应外部组件（如 LibreOffice）
/// </summary>
public class InstallDependencyStrategy : IRetryStrategy
{
    public FailureCategory AppliesTo => FailureCategory.DependencyMissing;

    public Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = true,
            Message = "缺少必要依赖（如 LibreOffice），请先安装所需组件"
        });
    }
}

/// <summary>
/// 损坏文件策略：尝试通过 OCR 或文本提取兜底重试
/// </summary>
public class OcrFallbackStrategy : IRetryStrategy
{
    public FailureCategory AppliesTo => FailureCategory.SourceFileCorrupted;

    public Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken)
    {
        if (context.AttemptCount <= 1 && context.EnableOcrFallback)
        {
            return Task.FromResult(new RetryExecutionResult
            {
                ShouldRetryImmediately = true,
                RequiresUserAction = false,
                Handled = true,
                Message = "尝试通过 OCR/文本兜底重试转换"
            });
        }

        return Task.FromResult(new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = false,
            Message = "常规与兜底重试均失败，文件可能已严重损坏"
        });
    }
}

/// <summary>
/// 文件锁定占用策略：提示用户关闭正在打开该文件的程序，延迟重试
/// </summary>
public class UnlockAndRetryStrategy : IRetryStrategy
{
    public FailureCategory AppliesTo => FailureCategory.FileLocked;

    public async Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken)
    {
        if (context.AttemptCount <= 2)
        {
            // 等待 500ms 观察占用是否解除
            await Task.Delay(500, cancellationToken);
            return new RetryExecutionResult
            {
                ShouldRetryImmediately = true,
                RequiresUserAction = false,
                Handled = true,
                Message = "正在尝试重新访问被占用文件"
            };
        }

        return new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = false,
            Message = "文件持续被其他程序占用，请关闭后手动重试"
        };
    }
}

/// <summary>
/// 资源不足策略：触发垃圾回收与单线程重试
/// </summary>
public class DowngradeConcurrencyStrategy : IRetryStrategy
{
    public FailureCategory AppliesTo => FailureCategory.ResourceExhausted;

    public Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (context.AttemptCount <= 1)
        {
            return Task.FromResult(new RetryExecutionResult
            {
                ShouldRetryImmediately = true,
                RequiresUserAction = false,
                Handled = true,
                Message = "已执行内存清理，正在进行单线程重试"
            });
        }

        return Task.FromResult(new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = false,
            Message = "系统内存持续不足，建议关闭其他大型软件"
        });
    }
}

/// <summary>
/// 内部未知异常策略：安全重试 1 次防抖
/// </summary>
public class RetryOnceStrategy : IRetryStrategy
{
    public FailureCategory AppliesTo => FailureCategory.InternalException;

    public Task<RetryExecutionResult> ExecuteAsync(RetryContext context, CancellationToken cancellationToken)
    {
        if (context.AttemptCount <= 1)
        {
            return Task.FromResult(new RetryExecutionResult
            {
                ShouldRetryImmediately = true,
                RequiresUserAction = false,
                Handled = true,
                Message = "发生瞬态内部异常，正在尝试重新转换"
            });
        }

        return Task.FromResult(new RetryExecutionResult
        {
            ShouldRetryImmediately = false,
            RequiresUserAction = true,
            Handled = false,
            Message = "内部转换重试失败"
        });
    }
}
