namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// مستخدم التطبيق — يربط Identity بجداول التطبيق عبر IdentitySubject
/// لا تُخزَّن كلمة المرور أو التوكنات هنا
/// </summary>
public class AppUser : Common.BaseEntity
{
    /// <summary>
    /// معرف المستخدم في جانب Identity (يساوي IdentityUser.Id)
    /// </summary>
    public string IdentitySubject { get; set; } = string.Empty;

    public string FullNameAr { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public ICollection<UserCourtGrant> CourtGrants { get; set; } = [];
}
