namespace JudicialStatistics.Domain.Geography;

/// <summary>
/// الاختصاص المكاني للجلسة — UNIQUE(SessionVersionId, GeographicAreaId)
/// تعدد الأماكن لا يضاعف أرقام الجلسة
/// </summary>
public class SessionJurisdiction : Common.BaseEntity
{
    public long SessionVersionId  { get; set; }
    public long GeographicAreaId  { get; set; }

    // Navigation
    public Organization.SessionVersion SessionVersion { get; set; } = null!;
    public GeographicArea               GeographicArea { get; set; } = null!;
}
