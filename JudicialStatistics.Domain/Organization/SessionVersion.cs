namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// نسخة الجلسة — تربط الجلسة بنسخة دائرة وتحدد جدول انعقادها
/// </summary>
public class SessionVersion : Common.BaseEntity
{
    public long SessionId        { get; set; }
    public long CircuitVersionId { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo   { get; set; }

    public string? ScheduleDescription { get; set; }

    // Navigation
    public Session        Session        { get; set; } = null!;
    public CircuitVersion CircuitVersion { get; set; } = null!;
    public ICollection<Geography.SessionJurisdiction> Jurisdictions { get; set; } = [];
    public ICollection<Rules.SessionCaseType>         SessionCaseTypes { get; set; } = [];
    public ICollection<Statistics.OpeningBalance>     OpeningBalances { get; set; } = [];
}
