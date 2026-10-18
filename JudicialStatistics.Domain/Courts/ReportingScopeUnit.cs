namespace JudicialStatistics.Domain.Courts;

/// <summary>
/// وحدة محكمة ضمن نطاق تقرير — منع الحساب المزدوج بالـ UNIQUE constraint
/// </summary>
public class ReportingScopeUnit : Common.BaseEntity
{
    public long ReportingScopeId { get; set; }
    public long CourtUnitId { get; set; }

    // Navigation
    public ReportingScope ReportingScope { get; set; } = null!;
    public CourtUnit CourtUnit { get; set; } = null!;
}
