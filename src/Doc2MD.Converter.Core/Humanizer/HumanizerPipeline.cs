using Doc2MD.Models;
using Doc2MD.Services;

namespace Doc2MD.Humanizer;

/// <summary>
/// Markdown 可读性优化责任链流水线
/// </summary>
public class HumanizerPipeline
{
    private readonly List<IHumanizerTransform> _transforms = [];

    public HumanizerPipeline(IEnumerable<IHumanizerTransform>? transforms = null)
    {
        if (transforms != null)
        {
            _transforms.AddRange(transforms);
        }
        else
        {
            // 默认 5 大原生转换器
            _transforms.Add(new HeadingNormalizerTransform());
            _transforms.Add(new TableFormatterTransform());
            _transforms.Add(new ListNormalizerTransform());
            _transforms.Add(new DocHighlightTransform());
            _transforms.Add(new TocGeneratorTransform());
        }
    }

    public void AddTransform(IHumanizerTransform transform)
    {
        _transforms.Add(transform);
    }

    /// <summary>
    /// 对 Markdown 执行可读性增强。单个 Transform 异常时独立降级并记录 Warning，不阻塞整文。
    /// </summary>
    public async Task<string> ProcessAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(markdown))
        {
            return markdown;
        }

        var currentMarkdown = markdown;

        foreach (var transform in _transforms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                currentMarkdown = await transform.ApplyAsync(currentMarkdown, result, options, cancellationToken);
            }
            catch (Exception ex)
            {
                LoggingService.Warning($"[HumanizerPipeline] 转换器 {transform.Name} 执行异常，已安全回退: {ex.Message}");
            }
        }

        return currentMarkdown;
    }
}
