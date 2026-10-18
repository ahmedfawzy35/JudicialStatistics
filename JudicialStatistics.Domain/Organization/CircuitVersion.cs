namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// نسخة الدائرة — تصدر ضمن نسخة تشكيل وتسري لفترة معينة
/// لا تتداخل نسخ الدائرة الفعالة
/// الدوائر الجزئية: قاضٍ واحد | الكلي والأسرة والعمال: 3-5 قضاة
/// </summary>
public class CircuitVersion : Common.BaseEntity
{
    public long CircuitId              { get; set; }
    public long OrganizationVersionId  { get; set; }

    /// <summary>Partial | CivilFull | CriminalAppeal | Family | Labor</summary>
    public string CircuitKind  { get; set; } = string.Empty;

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo   { get; set; }

    public short MinJudges { get; set; } = 1;
    public short MaxJudges { get; set; } = 1;

    // Navigation
    public Circuit              Circuit              { get; set; } = null!;
    public OrganizationVersion  OrganizationVersion  { get; set; } = null!;
    public ICollection<SessionVersion>          SessionVersions       { get; set; } = [];
    public ICollection<CircuitJudgeAssignment>  JudgeAssignments      { get; set; } = [];
}
