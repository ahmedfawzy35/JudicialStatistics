namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// جدول الموظفين — يشمل سكرتيري الجلسات وموظفي المحكمة
/// </summary>
public class Employee : Common.BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? NationalId { get; set; }
    public string? PhoneNumber { get; set; }
    public long? CourtUnitId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Courts.CourtUnit? CourtUnit { get; set; }
    public ICollection<Session> Sessions { get; set; } = [];
}
