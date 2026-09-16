using Doc2MD.Models;

namespace Doc2MD.Humanizer;

/// <summary>
/// Markdown 可读性转换器接口
/// </summary>
public interface IHumanizerTransform
{
    string Name { get; }
    Task<string> ApplyAsync(string markdown, ConversionResult result, HumanizerOptions options, CancellationToken cancellationToken);
}
