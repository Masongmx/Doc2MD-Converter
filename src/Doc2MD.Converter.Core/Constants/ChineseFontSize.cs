namespace Doc2MD.Constants;

/// <summary>
/// 中文字号（号制）与磅值（pt）双向映射。
/// 用于 UI 显示中文字号名称（如"二号"、"三号"），内部存储仍为磅值。
/// </summary>
public static class ChineseFontSize
{
    /// <summary>
    /// 中文字号 → 磅值对照表（按磅值降序排列）
    /// </summary>
    public static readonly (string Name, double Pt)[] AllSizes =
    [
        ("初号", 42.0),
        ("小初", 36.0),
        ("一号", 26.0),
        ("小一", 24.0),
        ("二号", 22.0),
        ("小二", 18.0),
        ("三号", 16.0),
        ("小三", 15.0),
        ("四号", 14.0),
        ("小四", 12.0),
        ("五号", 10.5),
        ("小五", 9.0),
        ("六号", 7.5),
        ("小六", 6.5),
        ("七号", 5.5),
        ("八号", 5.0),
    ];

    private static readonly Dictionary<string, double> NameToPt = AllSizes
        .ToDictionary(x => x.Name, x => x.Pt);

    private static readonly Dictionary<double, string> PtToName = AllSizes
        .ToDictionary(x => x.Pt, x => x.Name);

    /// <summary>
    /// 磅值 → 中文字号名，精确匹配时返回对应名称，无精确匹配返回 null
    /// </summary>
    public static string? TryGetName(double pt)
    {
        return PtToName.TryGetValue(pt, out var name) ? name : null;
    }

    /// <summary>
    /// 磅值 → 中文字号名，精确匹配返回名称，否则找最接近的标准字号
    /// </summary>
    public static string GetName(double pt)
    {
        if (PtToName.TryGetValue(pt, out var name))
            return name;

        // 找最接近的
        var closest = AllSizes.OrderBy(x => Math.Abs(x.Pt - pt)).First();
        return $"{closest.Name}(≈{pt:F1}pt)";
    }

    /// <summary>
    /// 中文字号名 → 磅值，无效返回 null
    /// </summary>
    public static double? TryGetPt(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return NameToPt.TryGetValue(name, out var pt) ? pt : null;
    }

    /// <summary>
    /// 获取所有中文字号名（用于 ComboBox 绑定）
    /// </summary>
    public static string[] AllNames => AllSizes.Select(x => x.Name).ToArray();
}
