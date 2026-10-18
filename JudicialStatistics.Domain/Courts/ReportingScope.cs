namespace JudicialStatistics.Domain.Courts;

/// <summary>
/// نطاق التقرير — يجمع وحدات محاكم تحت نطاق تقارير واحد
/// يبدأ التطبيق بنطاق واحد للمحكمة المطلوبة
/// </summary>
public class ReportingScope : Common.BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// الوحدة الجذرية لهذا النطاق
    /// </summary>
    public long RootCourtUnitId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public CourtUnit RootCourtUnit { get; set; } = null!;
    public ICollection<ReportingScopeUnit> ScopeUnits { get; set; } = [];
}
