namespace JudicialStatistics.Domain.Rules;

/// <summary>
/// شجرة تصنيف الأحكام — هرمية ضمن نسخة قواعد
/// </summary>
public class JudgmentTreeNode : Common.BaseEntity
{
    public long   RuleSetVersionId { get; set; }
    public long?  ParentId         { get; set; }
    public string Code             { get; set; } = string.Empty;
    public string NameAr           { get; set; } = string.Empty;
    public short  SortOrder        { get; set; }

    // Navigation
    public RuleSetVersion             RuleSetVersion { get; set; } = null!;
    public JudgmentTreeNode?          Parent         { get; set; }
    public ICollection<JudgmentTreeNode> Children    { get; set; } = [];
}

/// <summary>
/// نوع الحكم — مثال: عدم قبول، عدم اختصاص، ندب خبير، براءة، حبس
/// </summary>
public class JudgmentType : Common.BaseEntity
{
    public string Code     { get; set; } = string.Empty;
    public string NameAr   { get; set; } = string.Empty;
    public bool   IsActive { get; set; } = true;

    // Navigation
    public ICollection<CaseTypeJudgmentRule> Rules { get; set; } = [];
}

/// <summary>
/// نوع المقدم — السابق، الجديد، المجدد، المعجل، التقصير الوارد
/// InputSource: Manual | Balance | Shortening
/// </summary>
public class IncomingType : Common.BaseEntity
{
    public string Code        { get; set; } = string.Empty;
    public string NameAr      { get; set; } = string.Empty;

    /// <summary>Manual | Balance | Shortening</summary>
    public string InputSource { get; set; } = "Manual";
    public short  SortOrder   { get; set; }

    // Navigation
    public ICollection<CaseTypeInputRule>    InputRules    { get; set; } = [];
    public ICollection<CaseTypeDeferralRule> DeferralRules { get; set; } = [];
}

/// <summary>
/// سبب التأجيل — محجوز، مد الأجل، إعادة للمرافعة، تقرير، أسباب أخرى
/// </summary>
public class DeferralReason : Common.BaseEntity
{
    public string Code       { get; set; } = string.Empty;
    public string NameAr     { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public short  SortOrder  { get; set; }

    // Navigation
    public ICollection<CaseTypeInputRule>    InputRules    { get; set; } = [];
    public ICollection<CaseTypeDeferralRule> DeferralRules { get; set; } = [];
}

/// <summary>
/// بند الإنتاج — يُحدد ما يُسجَّل في إنتاج الجلسة
/// </summary>
public class ProductionItemType : Common.BaseEntity
{
    public string Code     { get; set; } = string.Empty;
    public string NameAr   { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;

    /// <summary>شاشة الإدخال المناسبة</summary>
    public string EntryScreen { get; set; } = string.Empty;

    public long? DeferralReasonId { get; set; }
    public short SortOrder        { get; set; }

    // Navigation
    public DeferralReason?             DeferralReason { get; set; }
    public ICollection<CaseTypeInputRule> InputRules  { get; set; } = [];
}

/// <summary>
/// نوع إجراء القاضي — مد الأجل، إعادة للمرافعة (لا تدخل في إجمالي الأحكام)
/// </summary>
public class JudgeActionType : Common.BaseEntity
{
    public string Code            { get; set; } = string.Empty;
    public string NameAr          { get; set; } = string.Empty;
    public long   DeferralReasonId { get; set; }
    public short  SortOrder        { get; set; }

    // Navigation
    public DeferralReason DeferralReason { get; set; } = null!;
    public ICollection<CaseTypeInputRule> InputRules { get; set; } = [];
}
