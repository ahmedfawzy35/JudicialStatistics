namespace JudicialStatistics.Domain.Cases;

/// <summary>
/// نوع القضية — مرتبط بعقدة في شجرة التصنيف
/// StatisticalLevelCode: Full | Partial | Criminal | Family | Labor | Juvenile
/// ProcedureCode: Civil | Criminal | Family | Labor
/// AgeCategoryCode: Adult | Juvenile | All
/// </summary>
public class CaseType : Common.BaseEntity
{
    public long   TaxonomyNodeId      { get; set; }
    public string Code                { get; set; } = string.Empty;
    public string NameAr              { get; set; } = string.Empty;
    public string StatisticalLevelCode { get; set; } = string.Empty;
    public string ProcedureCode        { get; set; } = string.Empty;
    public string AgeCategoryCode      { get; set; } = string.Empty;

    // Navigation
    public CaseTaxonomyNode TaxonomyNode { get; set; } = null!;
    public ICollection<Rules.SessionCaseType> SessionCaseTypes { get; set; } = [];
}
