namespace JudicialStatistics.Domain.Courts;

/// <summary>
/// وحدة المحكمة — يمكن أن تكون محكمة رئيسية أو وحدة تابعة
/// العلاقة ذاتية: وحدة رئيسية ← وحدات تابعة
/// </summary>
public class CourtUnit : Common.BaseEntity
{
    public long? ParentCourtUnitId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// نوع الوحدة: Court, Department, Section, ...
    /// </summary>
    public string UnitKind { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigation
    public CourtUnit? ParentCourtUnit { get; set; }
    public ICollection<CourtUnit> ChildUnits { get; set; } = [];
    public ICollection<ReportingScope> ReportingScopes { get; set; } = [];
    public ICollection<Identity.UserCourtGrant> UserGrants { get; set; } = [];
}
