namespace JudicialStatistics.Domain.Cases;

/// <summary>
/// عقدة في شجرة تصنيف القضايا — علاقة ذاتية هرمية
/// مثال: مدني ← مدني كلي ← حكومة
/// </summary>
public class CaseTaxonomyNode : Common.BaseEntity
{
    public long?  ParentId  { get; set; }
    public string Code      { get; set; } = string.Empty;
    public string NameAr    { get; set; } = string.Empty;
    public short  SortOrder { get; set; }

    // Navigation
    public CaseTaxonomyNode?             Parent   { get; set; }
    public ICollection<CaseTaxonomyNode> Children { get; set; } = [];
    public ICollection<CaseType>         CaseTypes { get; set; } = [];
}
