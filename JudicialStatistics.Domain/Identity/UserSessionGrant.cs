namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// منح مستخدم على جلسة محددة — يُستخدم عندما يكون SessionScopeMode = SelectedSessions
/// </summary>
public class UserSessionGrant : Common.BaseEntity
{
    public long CourtGrantId { get; set; }
    public long SessionId { get; set; }

    // Navigation
    public UserCourtGrant CourtGrant { get; set; } = null!;
    public Organization.Session Session { get; set; } = null!;
}
