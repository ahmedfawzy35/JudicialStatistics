namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// التخصص القضائي الإحصائي (مثل: مدني، جنائي، أسرة، عمال، أو أي تخصص مستقبلي مضاف)
/// يربط التخصص بأنواع الدوائر القضائية التابعة وشكل نموذج الإدخال (مدني تفصيلي / جنائي)
/// </summary>
public class StatisticalDiscipline : Common.BaseEntity
{
    public string Code          { get; set; } = string.Empty;
    public string NameAr        { get; set; } = string.Empty;
    public string Icon          { get; set; } = "⚖️";
    public string CircuitKinds  { get; set; } = string.Empty;
    public string EntryFormKind { get; set; } = "Civil"; // Civil | Criminal
    public short  SortOrder     { get; set; } = 10;
    public bool   IsActive      { get; set; } = true;
}
