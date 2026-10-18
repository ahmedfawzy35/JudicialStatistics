namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// الدائرة — تنتمي إلى عام قضائي ووحدة محكمة
/// الدائرة في 2025/2026 لها معرف مستقل عن الدائرة في 2026/2027 حتى لو تطابق الاسم
/// </summary>
public class Circuit : Common.BaseEntity
{
    public long JudicialYearId  { get; set; }
    public long CourtUnitId     { get; set; }
    public string Code          { get; set; } = string.Empty;
    public string NameAr        { get; set; } = string.Empty;
    public bool IsClosed        { get; set; } = false;

    // Navigation
    public Years.JudicialYear       JudicialYear     { get; set; } = null!;
    public Courts.CourtUnit         CourtUnit        { get; set; } = null!;
    public ICollection<CircuitVersion> Versions      { get; set; } = [];
    public ICollection<Session>     Sessions         { get; set; } = [];
}
