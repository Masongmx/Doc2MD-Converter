using Doc2MD.Pipeline.Models;
using Doc2MD.Services;

namespace Doc2MD.Pipeline.Services;

/// <summary>
/// 统一模板存储入口：管理内置模板和用户模板。
/// GUI 不允许直接访问文件系统，必须通过此 Repository（及 TemplateService）。
/// Phase 1 仅支持内置模板，用户模板操作留作 Phase 2 占位。
/// </summary>
public class TemplateRepository
{
    private static readonly List<DocxTemplate> _builtInTemplates =
    [
        DocxTemplate.OfficialReport(),
        DocxTemplate.MeetingMinutes()
    ];

    /// <summary>加载所有内置模板</summary>
    public IReadOnlyList<DocxTemplate> LoadBuiltInTemplates() => _builtInTemplates;

    /// <summary>
    /// 加载用户自定义模板。
    /// Phase 1 返回空列表；Phase 2 从用户目录加载。
    /// </summary>
    public IReadOnlyList<DocxTemplate> LoadUserTemplates() => Array.Empty<DocxTemplate>();

    /// <summary>按 ID 获取模板（先查内置，再查用户），未找到返回 null</summary>
    public DocxTemplate? GetTemplate(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _builtInTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? LoadUserTemplates().FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    // ==== Phase 2 占位方法 ====
    // 当前版本不支持用户模板编辑：不做抛异常处理（避免调用方崩溃），
    // 改为记录 Warning 日志并返回空结果，待 Phase 2 实现。

    /// <summary>保存用户模板（Phase 2 占位：记录日志并忽略）</summary>
    public void SaveTemplate(DocxTemplate template)
    {
        LoggingService.Warning($"模板保存功能暂未开放（Phase 2），已忽略: {template?.Id ?? "<null>"}");
    }

    /// <summary>删除用户模板（Phase 2 占位：记录日志并忽略）</summary>
    public void DeleteTemplate(string id)
    {
        LoggingService.Warning($"模板删除功能暂未开放（Phase 2），已忽略: {id ?? "<null>"}");
    }

    /// <summary>克隆模板（Phase 2 占位：记录日志并返回 null）</summary>
    public DocxTemplate? CloneTemplate(DocxTemplate template)
    {
        LoggingService.Warning($"模板克隆功能暂未开放（Phase 2），返回 null: {template?.Id ?? "<null>"}");
        return null;
    }
}
