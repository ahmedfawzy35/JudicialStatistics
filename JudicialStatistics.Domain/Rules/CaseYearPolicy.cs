namespace JudicialStatistics.Domain.Rules;

/// <summary>
/// سياسة شرائح سنوات القيد — تحدد كيفية تقسيم القضايا حسب سنة قيدها
/// UNIQUE(JudicialYearId, CaseTypeId, VersionNo)
/// </summary>
public class CaseYearPolicy : Common.BaseEntity
{
    public long   JudicialYearId    { get; set; }
    public long   CaseTypeId        { get; set; }
    public int    VersionNo         { get; set; }
    public string DecisionReference { get; set; } = string.Empty;

    /// <summary>Draft | Approved</summary>
    public string Status { get; set; } = "Draft";

    // Navigation
    public Years.JudicialYear    JudicialYear { get; set; } = null!;
    public Cases.CaseType        CaseType     { get; set; } = null!;
    public ICollection<CaseYearBand> Bands    { get; set; } = [];
}

/// <summary>
/// شريحة سنة قيد ضمن السياسة — FromYear إلى ToYear أو AllYears (للجنح)
/// BandKind: Exact | UpTo | AllYears
/// في الجنح: شريحة AllYears واحدة — تُخزَّن داخلياً ولا تظهر للمستخدم كسنة قيد
/// </summary>
public class CaseYearBand : Common.BaseEntity
{
    public long   CaseYearPolicyId { get; set; }
    public short? FromYear         { get; set; }
    public short? ToYear           { get; set; }

    /// <summary>Exact | UpTo | AllYears</summary>
    public string BandKind  { get; set; } = string.Empty;
    public string LabelAr   { get; set; } = string.Empty;
    public short  SortOrder { get; set; }

    // Navigation
    public CaseYearPolicy Policy { get; set; } = null!;
}
