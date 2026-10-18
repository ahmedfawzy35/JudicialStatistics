namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// الأدوار الوظيفية داخل التطبيق — مستقلة عن IdentityRole
/// </summary>
public class Role : Common.BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserCourtGrant> UserCourtGrants { get; set; } = [];
}
