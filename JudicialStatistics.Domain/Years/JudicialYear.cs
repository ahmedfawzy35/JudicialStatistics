namespace JudicialStatistics.Domain.Years;

/// <summary>
/// العام القضائي — من 1 أكتوبر إلى 30 سبتمبر من العام التالي
/// مثال: 2025/2026 = 2025-10-01 إلى 2026-09-30
/// لا تتداخل الأعوام داخل نطاق التقرير
/// </summary>
public class JudicialYear : Common.BaseEntity
{
    public long ReportingScopeId { get; set; }

    /// <summary>مثال: 2025/2026</summary>
    public string NameAr { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate   { get; set; }

    /// <summary>Draft | Active | Closed</summary>
    public string Status { get; set; } = "Draft";

    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Courts.ReportingScope ReportingScope { get; set; } = null!;
    public ICollection<StatisticalPeriod> Periods { get; set; } = [];
    public ICollection<Organization.OrganizationVersion> OrganizationVersions { get; set; } = [];
}
