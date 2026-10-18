namespace JudicialStatistics.Domain.Rules;

/// <summary>
/// نسخة مجموعة القواعد — المعتمدة لا تُعدَّل، ينشأ إصدار جديد عند الحاجة
/// </summary>
public class RuleSetVersion : Common.BaseEntity
{
    public int     VersionNo { get; set; }
    public string  NameAr   { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Draft | Approved</summary>
    public string Status { get; set; } = "Draft";

    public long?     ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt       { get; set; }
    public byte[]    RowVersion       { get; set; } = [];

    // Navigation
    public Identity.AppUser? ApprovedBy { get; set; }
    public ICollection<CaseYearPolicy>       CaseYearPolicies      { get; set; } = [];
    public ICollection<JudgmentTreeNode>     JudgmentTreeNodes     { get; set; } = [];
    public ICollection<CaseTypeJudgmentRule> CaseTypeJudgmentRules { get; set; } = [];
    public ICollection<CaseTypeInputRule>    CaseTypeInputRules    { get; set; } = [];
    public ICollection<CaseTypeDeferralRule> CaseTypeDeferralRules { get; set; } = [];
}
