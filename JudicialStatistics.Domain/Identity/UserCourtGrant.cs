namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// منح مستخدم دورًا على محكمة محددة — النطاق إما كل الجلسات أو جلسات مختارة
/// </summary>
public class UserCourtGrant : Common.BaseEntity
{
    public long UserId { get; set; }
    public long CourtUnitId { get; set; }
    public long RoleId { get; set; }

    /// <summary>
    /// AllSessions أو SelectedSessions
    /// </summary>
    public string SessionScopeMode { get; set; } = "AllSessions";

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public AppUser User { get; set; } = null!;
    public Courts.CourtUnit CourtUnit { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public ICollection<UserSessionGrant> SessionGrants { get; set; } = [];
}
