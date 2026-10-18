namespace JudicialStatistics.Domain.Rules;

/// <summary>
/// ربط نوع القضية بنسخة الجلسة — يُحدد القضايا التي تنظرها جلسة معينة
/// قضايا المدني الجزئي (ومنها المستعجل) تختص بالدوائر الجزئية فقط ولا تسند لدوائر مدني كلي
/// </summary>
public class SessionCaseType : Common.BaseEntity
{
    public long SessionVersionId { get; set; }
    public long CaseTypeId       { get; set; }

    // Navigation
    public Organization.SessionVersion SessionVersion { get; set; } = null!;
    public Cases.CaseType              CaseType       { get; set; } = null!;
}
