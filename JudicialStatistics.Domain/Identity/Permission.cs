namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// صلاحية ضمنية — مثل Courts.View، Statistics.Approve
/// </summary>
public class Permission : Common.BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? GroupAr { get; set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
