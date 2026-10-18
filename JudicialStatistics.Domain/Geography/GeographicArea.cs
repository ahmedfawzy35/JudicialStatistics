namespace JudicialStatistics.Domain.Geography;

/// <summary>
/// المنطقة الجغرافية — علاقة ذاتية: محافظة ← مراكز ← بنادر
/// يستخدم Seed أولي قابل للتعديل
/// </summary>
public class GeographicArea : Common.BaseEntity
{
    public long?  ParentAreaId { get; set; }
    public string Code         { get; set; } = string.Empty;
    public string NameAr       { get; set; } = string.Empty;

    /// <summary>Governorate | Center | City | District</summary>
    public string AreaKind { get; set; } = string.Empty;
    public bool   IsActive { get; set; } = true;

    // Navigation
    public GeographicArea?              ParentArea   { get; set; }
    public ICollection<GeographicArea>  ChildAreas   { get; set; } = [];
    public ICollection<SessionJurisdiction> Jurisdictions { get; set; } = [];
}
