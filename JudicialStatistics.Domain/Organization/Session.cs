namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// الجلسة — جلسة متكررة داخل الدائرة، وليست تاريخ انعقاد مفرداً
/// UNIQUE(CircuitId, Code)
/// </summary>
public class Session : Common.BaseEntity
{
    public long   CircuitId { get; set; }
    public string Code      { get; set; } = string.Empty;
    public string NameAr    { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool   IsClosed  { get; set; } = false;

    /// <summary>تحديد ظهور الجلسة في تقرير نسبة إدخال الإحصائية</summary>
    public bool   ShowInProgressReport { get; set; } = true;

    /// <summary>سكرتير الجلسة (الموظف المسؤول)</summary>
    public long? EmployeeId { get; set; }

    // Navigation
    public Circuit                    Circuit         { get; set; } = null!;
    public Employee?                  Employee        { get; set; }
    public ICollection<SessionVersion> Versions       { get; set; } = [];
    public ICollection<Identity.UserSessionGrant> UserSessionGrants { get; set; } = [];
}
