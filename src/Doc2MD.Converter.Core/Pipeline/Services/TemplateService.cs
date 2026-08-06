using Doc2MD.Pipeline.Models;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// 模板调度层：统一模板解析、选择能力，屏蔽 Repository 实现细节。
/// GUI 均通过此服务获取模板，不允许直接访问 Repository。
/// </summary>
public class TemplateService
{
    private readonly TemplateRepository _repository;

    public TemplateService()
    {
        _repository = new TemplateRepository();
    }

    /// <summary>获取所有模板（内置 + 用户）</summary>
    public IReadOnlyList<DocxTemplate> GetAllTemplates() =>
        _repository.LoadBuiltInTemplates()
            .Concat(_repository.LoadUserTemplates())
            .ToList();

    /// <summary>获取所有内置模板</summary>
    public IReadOnlyList<DocxTemplate> GetBuiltInTemplates() =>
        _repository.LoadBuiltInTemplates();

    /// <summary>
    /// 按 ID 获取模板。未知 ID 回退到默认模板。
    /// 兼容旧 ID：official-basic → official-report。
    /// </summary>
    public DocxTemplate GetTemplate(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return GetDefaultTemplate();

        // 兼容旧 FormattingProfile ID
        var resolvedId = id switch
        {
            "official-basic" => "official-report",
            _ => id
        };

        return _repository.GetTemplate(resolvedId) ?? GetDefaultTemplate();
    }

    /// <summary>获取默认模板（正式报告）</summary>
    public DocxTemplate GetDefaultTemplate() => DocxTemplate.OfficialReport();

    /// <summary>默认模板 ID</summary>
    public static string DefaultTemplateId => "official-report";

    /// <summary>判断模板 ID 是否有效</summary>
    public bool IsValidTemplate(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var resolvedId = id == "official-basic" ? "official-report" : id;
        return _repository.GetTemplate(resolvedId) != null;
    }
}
