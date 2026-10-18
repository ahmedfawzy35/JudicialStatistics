namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// ربط الأدوار بالصلاحيات — علاقة M:N
/// </summary>
public class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }

    // Navigation
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
