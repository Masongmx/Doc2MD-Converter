using System.IO;
using System.Text.Json;
using Doc2MD.Services;

namespace Doc2MD.Failures;

/// <summary>
/// 基于 JSON 文件的轻量失败知识库实现（线程安全 + 自动滚动上限）
/// </summary>
public class JsonFailureRepository : IFailureRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private readonly int _maxRecords;
    private List<FailureRecord> _cache = [];
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonFailureRepository(string? filePath = null, int maxRecords = 500)
    {
        _filePath = filePath ?? Path.Combine(AppPaths.AppDataDirectory, "failures.json");
        _maxRecords = Math.Max(50, maxRecords);
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _cache.Count;
            }
        }
    }

    public void Save(FailureRecord record)
    {
        lock (_lock)
        {
            EnsureLoaded();

            // 若已有同 SHA-256 且同类型的记录，更新之
            var existingIndex = !string.IsNullOrEmpty(record.SourceSha256)
                ? _cache.FindIndex(r => r.SourceSha256 == record.SourceSha256 && r.Category == record.Category)
                : -1;

            if (existingIndex >= 0)
            {
                _cache[existingIndex] = record;
            }
            else
            {
                _cache.Insert(0, record);
            }

            // 滚动上限控制
            if (_cache.Count > _maxRecords)
            {
                _cache = _cache.Take(_maxRecords).ToList();
            }

            PersistToFile();
        }
    }

    public IReadOnlyList<FailureRecord> GetAll()
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _cache.ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<FailureRecord> QueryByCategory(FailureCategory category)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _cache.Where(r => r.Category == category).ToList().AsReadOnly();
        }
    }

    public bool TryGetRecentSolution(string sha256, out FailureRecord? record)
    {
        record = null;
        if (string.IsNullOrEmpty(sha256)) return false;

        lock (_lock)
        {
            EnsureLoaded();
            record = _cache.FirstOrDefault(r => r.SourceSha256 == sha256 && r.IsResolved);
            return record != null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            PersistToFile();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _cache = JsonSerializer.Deserialize<List<FailureRecord>>(json, JsonOptions) ?? [];
            }
        }
        catch (Exception ex)
        {
            LoggingService.Warning($"[JsonFailureRepository] 读取失败库失败，已重置为空: {ex.Message}");
            _cache = [];
        }
    }

    private void PersistToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_cache, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            LoggingService.Warning($"[JsonFailureRepository] 写入失败库失败: {ex.Message}");
        }
    }
}
