using System.IO;
using System.Text.RegularExpressions;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// 公文元数据提取器：从转换后的 Markdown 中识别
/// 标题、文号、发文单位、发布日期、文档类型、主题关键词
/// v2.0 移植自 Doc2KB Core，适配桌面转换器架构
/// </summary>
public static class GovMetadataExtractor
{
    // ── 文号正则 ──────────────────────────────────────────
    // 匹配：〔2024〕12号、[2024]12号、（2024）12号
    private static readonly Regex DocumentNumberPattern = new(
        @"[\〔\[\（\(]\s*(\d{4})\s*[\〕\]\）\)]\s*(\d{1,4})\s*号",
        RegexOptions.Compiled);

    // 完整文号含发文机关代字：巡办发〔2022〕8号
    private static readonly Regex FullDocumentNumberPattern = new(
        @"(?<![a-zA-Z\u4E00-\u9FFF])([a-zA-Z\u4E00-\u9FFF]{2,10})[\〔\[\（\(]\s*(\d{4})\s*[\〕\]\）\)]\s*(\d{1,4})\s*号",
        RegexOptions.Compiled);

    // ── 日期正则 ──────────────────────────────────────────
    private static readonly Regex DatePatternCn = new(
        @"(\d{4})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})\s*日",
        RegexOptions.Compiled);

    private static readonly Regex DatePatternIso = new(
        @"(\d{4})[-\.](\d{1,2})[-\.](\d{1,2})",
        RegexOptions.Compiled);

    // Frontmatter 剥离正则（防止 created_at 等字段污染日期提取）
    private static readonly Regex FrontmatterStripPattern = new(
        @"^---\s*\n.*?\n---\s*\n", RegexOptions.Singleline | RegexOptions.Compiled);

    // ── 发文单位关键词 ────────────────────────────────────
    private static readonly string[] AuthorityKeywords =
    [
        "巡察工作领导小组", "党委组织部",
        "人力资源部", "人力资源", "人资部",
        "纪检监察", "纪检组", "纪委",
        "公司办公室", "综合管理部", "综合部", "办公室",
        "集团党委", "党委", "党组", "董事会", "总经理办公室",
        "财务部", "审计部", "风控部", "法务部", "合规部",
        "工会", "团委", "妇联",
        "巡察办", "组织部",
    ];

    // ── 文档类型关键词映射 ────────────────────────────────
    private static readonly (string[] Keywords, string Type)[] DocumentTypeMap =
    [
        (["通知"], "notice"),
        (["方案", "实施方案", "工作方案"], "plan"),
        (["办法", "实施办法", "管理办法"], "measure"),
        (["制度", "管理制度"], "policy"),
        (["规定", "暂行规定"], "regulation"),
        (["请示"], "request"),
        (["报告", "情况报告", "自查报告"], "report"),
        (["培训", "培训材料", "培训方案"], "training"),
        (["清单", "问题清单", "明细表"], "checklist"),
        (["意见", "指导意见", "实施意见"], "opinion"),
        (["决定", "决议"], "decision"),
        (["函", "复函", "公函"], "letter"),
        (["纪要", "会议纪要"], "minutes"),
        (["通报", "通报批评", "通报表扬"], "bulletin"),
        (["批复"], "approval"),
    ];

    // ── 主题关键词 ────────────────────────────────────────
    private static readonly (string[] Keywords, string Topic)[] TopicKeywordMap =
    [
        (["人事", "人员", "员工", "招聘", "入职", "离职", "退休", "调动", "编制", "用工"], "hr"),
        (["薪酬", "工资", "薪资", "奖金", "绩效", "收入分配", "待遇", "补贴", "津贴"], "salary"),
        (["干部", "任免", "选拔", "考察", "提拔", "晋升", "任职", "选人用人"], "cadre"),
        (["巡察", "巡视", "整改", "监督", "专项检查", "回头看"], "inspection"),
        (["制度", "规定", "办法", "规则", "流程", "规范", "标准"], "regulation"),
        (["培训", "培训班", "培训方案", "培训计划", "技能鉴定"], "training"),
        (["考核", "评价", "评议", "测评", "KPI", "考评", "绩效考核"], "assessment"),
        (["劳动合同", "用工", "社保", "保险", "公积金", "工伤"], "labor"),
        (["组织架构", "机构设置", "职责分工", "职能调整", "部门调整"], "organization"),
        (["纪委", "廉洁", "廉政", "纪律", "作风", "问责", "处分"], "discipline"),
        (["战略协议", "战略合作", "战略规划", "行动计划", "转型方案", "行动方案"], "strategy"),
        (["数字化转型", "数字化", "信息化", "AI转型", "智慧化", "数智化"], "digital"),
        (["政企", "政企合作", "政企客户", "商企", "企业客户", "大客户"], "enterprise"),
        (["改革", "深化改革", "企业改革", "机制改革", "体制机制"], "reform"),
    ];

    private static readonly string[] InvalidTitleKeywords =
        ["目录", "前言", "扫描件", "封面", "空白", "无标题", "untitled"];

    /// <summary>
    /// 从 Markdown 文本和源文件名中提取公文元数据
    /// </summary>
    public static GovMetadata Extract(string markdown, string? sourceFileName)
    {
        var metadata = new GovMetadata();

        // 1. 标题
        metadata.Title = ExtractTitle(markdown, sourceFileName);

        // 2. 文号
        metadata.DocumentNumber = ExtractDocumentNumber(markdown);

        // 3. 发文单位
        metadata.IssuingAuthority = ExtractIssuingAuthority(markdown);

        // 4. 发布日期
        metadata.PublishDate = ExtractPublishDate(markdown);

        // 5. 文档类型
        metadata.DocumentType = ExtractDocumentType(metadata.Title);

        // 6. 主题关键词
        metadata.SubjectKeywords = ExtractSubjectKeywords(metadata.Title, markdown);

        // 7. 置信度
        metadata.Confidence = ComputeConfidence(metadata);

        return metadata;
    }

    // ══════════════════════════════════════════════════════
    //  标题识别
    // ══════════════════════════════════════════════════════

    private static string? ExtractTitle(string markdown, string? sourceFileName)
    {
        // 优先级1: Markdown 第一行一级标题
        var headingTitle = ExtractFirstH1(markdown);
        if (!string.IsNullOrEmpty(headingTitle) && !IsInvalidTitle(headingTitle))
            return CleanTitle(headingTitle);

        // 优先级2: 源文件名
        if (!string.IsNullOrEmpty(sourceFileName))
        {
            var nameTitle = Path.GetFileNameWithoutExtension(sourceFileName);
            if (!string.IsNullOrEmpty(nameTitle) && !IsInvalidTitle(nameTitle))
                return CleanTitle(nameTitle);
        }

        return null;
    }

    private static string? ExtractFirstH1(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# "))
                return trimmed[2..].Trim();
        }
        return null;
    }

    private static bool IsInvalidTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return true;
        if (title.Length < 2) return true;
        return InvalidTitleKeywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanTitle(string title)
    {
        var cleaned = Regex.Replace(title, @"^[\d]+[\.\、\.\s]+", "");
        cleaned = cleaned.Trim();
        return string.IsNullOrEmpty(cleaned) ? title : cleaned;
    }

    // ══════════════════════════════════════════════════════
    //  文号识别
    // ══════════════════════════════════════════════════════

    private static string? ExtractDocumentNumber(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return null;

        var fullMatch = FullDocumentNumberPattern.Match(markdown);
        if (fullMatch.Success)
            return fullMatch.Value;

        var simpleMatch = DocumentNumberPattern.Match(markdown);
        if (simpleMatch.Success)
            return simpleMatch.Value;

        return null;
    }

    // ══════════════════════════════════════════════════════
    //  发文单位识别
    // ══════════════════════════════════════════════════════

    private static string? ExtractIssuingAuthority(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return null;

        var headArea = markdown.Length > 500 ? markdown[..500] : markdown;
        var bodyArea = markdown.Length > 2000 ? markdown[..2000] : markdown;

        var best = FindBestAuthorityMatch(headArea);
        if (best != null) return best;

        best = FindBestAuthorityMatch(bodyArea);
        if (best != null) return best;

        if (markdown.Length > 2000)
        {
            var tailText = markdown[^Math.Min(1000, markdown.Length)..];
            best = FindBestAuthorityMatch(tailText);
            if (best != null) return best;
        }

        return null;
    }

    private static string? FindBestAuthorityMatch(string text)
    {
        string? bestMatch = null;
        foreach (var authority in AuthorityKeywords)
        {
            if (text.Contains(authority, StringComparison.Ordinal))
            {
                if (bestMatch == null || authority.Length > bestMatch.Length)
                    bestMatch = authority;
            }
        }
        return bestMatch;
    }

    // ══════════════════════════════════════════════════════
    //  发布日期识别
    // ══════════════════════════════════════════════════════

    private static string? ExtractPublishDate(string markdown)
    {
        var bodyText = StripFrontmatterForDate(markdown);
        if (string.IsNullOrEmpty(bodyText)) return null;

        var match = DatePatternCn.Match(bodyText);
        if (match.Success && IsPlausibleDate(match.Groups[1].Value))
            return NormalizeDate(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);

        var isoMatch = DatePatternIso.Match(bodyText);
        while (isoMatch.Success)
        {
            var prefixStart = Math.Max(0, isoMatch.Index - 30);
            var prefix = bodyText[prefixStart..isoMatch.Index];
            if (prefix.Contains("created_at", StringComparison.OrdinalIgnoreCase) ||
                prefix.Contains("updated_at", StringComparison.OrdinalIgnoreCase))
            {
                isoMatch = isoMatch.NextMatch();
                continue;
            }

            if (IsPlausibleDate(isoMatch.Groups[1].Value))
                return NormalizeDate(isoMatch.Groups[1].Value, isoMatch.Groups[2].Value, isoMatch.Groups[3].Value);

            isoMatch = isoMatch.NextMatch();
        }

        return null;
    }

    private static string? StripFrontmatterForDate(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return null;
        var stripped = FrontmatterStripPattern.Replace(markdown, "");
        return string.IsNullOrEmpty(stripped) ? null : stripped;
    }

    private static bool IsPlausibleDate(string yearStr)
    {
        if (!int.TryParse(yearStr, out var year)) return false;
        return year >= 1980 && year <= DateTime.UtcNow.Year;
    }

    private static string NormalizeDate(string year, string month, string day)
    {
        return $"{year}-{int.Parse(month):D2}-{int.Parse(day):D2}";
    }

    // ══════════════════════════════════════════════════════
    //  文档类型识别
    // ══════════════════════════════════════════════════════

    private static string? ExtractDocumentType(string? title)
    {
        if (string.IsNullOrEmpty(title)) return null;

        foreach (var (keywords, type) in DocumentTypeMap)
        {
            if (keywords.Any(k => title.Contains(k, StringComparison.Ordinal)))
                return type;
        }

        return "other";
    }

    // ══════════════════════════════════════════════════════
    //  主题关键词识别
    // ══════════════════════════════════════════════════════

    private static List<string> ExtractSubjectKeywords(string? title, string markdown)
    {
        var keywords = new List<string>();
        var searchText = (title ?? "") + "\n" +
            (markdown.Length > 3000 ? markdown[..3000] : markdown);

        foreach (var (matchKeywords, topic) in TopicKeywordMap)
        {
            if (matchKeywords.Any(k => searchText.Contains(k, StringComparison.Ordinal)))
                keywords.Add(topic);
        }

        return keywords;
    }

    // ══════════════════════════════════════════════════════
    //  置信度计算
    // ══════════════════════════════════════════════════════

    private static double ComputeConfidence(GovMetadata metadata)
    {
        double score = !string.IsNullOrEmpty(metadata.Title) ? 0.3 : 0.0;
        if (!string.IsNullOrEmpty(metadata.DocumentNumber)) score += 0.25;
        if (!string.IsNullOrEmpty(metadata.IssuingAuthority)) score += 0.2;
        if (!string.IsNullOrEmpty(metadata.PublishDate)) score += 0.1;
        if (!string.IsNullOrEmpty(metadata.DocumentType) && metadata.DocumentType != "other") score += 0.1;
        if (metadata.SubjectKeywords.Count > 0) score += 0.05;
        return Math.Round(Math.Min(score, 1.0), 2);
    }
}

/// <summary>
/// 公文元数据提取结果
/// </summary>
public class GovMetadata
{
    public string? Title { get; set; }
    public string? DocumentNumber { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? PublishDate { get; set; }
    public string? DocumentType { get; set; }
    public List<string> SubjectKeywords { get; set; } = [];
    public double Confidence { get; set; }

    /// <summary>是否识别到公文特征（至少有标题+文号或发文单位）</summary>
    public bool IsGovDocument =>
        !string.IsNullOrEmpty(Title) &&
        (!string.IsNullOrEmpty(DocumentNumber) || !string.IsNullOrEmpty(IssuingAuthority));
}
