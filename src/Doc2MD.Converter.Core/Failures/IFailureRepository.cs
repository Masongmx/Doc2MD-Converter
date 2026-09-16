namespace Doc2MD.Failures;

/// <summary>
/// 失败案例知识库仓储接口
/// </summary>
public interface IFailureRepository
{
    void Save(FailureRecord record);
    IReadOnlyList<FailureRecord> GetAll();
    IReadOnlyList<FailureRecord> QueryByCategory(FailureCategory category);
    bool TryGetRecentSolution(string sha256, out FailureRecord? record);
    void Clear();
    int Count { get; }
}
