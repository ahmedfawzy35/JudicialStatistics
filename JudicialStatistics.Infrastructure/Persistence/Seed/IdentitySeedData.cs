using JudicialStatistics.Domain.Identity;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// بيانات الـ Seed الأولية — الأدوار والصلاحيات
/// آمن للتنفيذ المتكرر (Idempotent)
/// </summary>
public static class IdentitySeedData
{
    public static async Task SeedAsync(JudicialDbContext context)
    {
        await SeedPermissionsAsync(context);
        await SeedRolesAsync(context);
        await SeedRolePermissionsAsync(context);
        await context.SaveChangesAsync();
    }

    private static async Task SeedPermissionsAsync(JudicialDbContext context)
    {
        var permissions = new[]
        {
            // المحاكم
            new { Code = PermissionCodes.CourtsView,   NameAr = "عرض المحاكم",          GroupAr = "المحاكم" },
            new { Code = PermissionCodes.CourtsManage, NameAr = "إدارة المحاكم",         GroupAr = "المحاكم" },
            // الأعوام القضائية
            new { Code = PermissionCodes.JudicialYearsView,   NameAr = "عرض الأعوام القضائية",  GroupAr = "الأعوام القضائية" },
            new { Code = PermissionCodes.JudicialYearsManage, NameAr = "إدارة الأعوام القضائية", GroupAr = "الأعوام القضائية" },
            // التشكيل
            new { Code = PermissionCodes.OrganizationView,   NameAr = "عرض التشكيل",   GroupAr = "التشكيل" },
            new { Code = PermissionCodes.OrganizationManage, NameAr = "إدارة التشكيل", GroupAr = "التشكيل" },
            // القضاة
            new { Code = PermissionCodes.JudgesManage, NameAr = "إدارة القضاة", GroupAr = "القضاة" },
            // الإحصائية
            new { Code = PermissionCodes.StatisticsView,        NameAr = "عرض الإحصائيات",      GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsCreate,      NameAr = "إنشاء إحصائية",       GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsEditDraft,   NameAr = "تعديل مسودة إحصائية", GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsDeleteDraft, NameAr = "حذف مسودة إحصائية",   GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsSubmit,      NameAr = "تقديم إحصائية للمراجعة", GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsReview,      NameAr = "مراجعة الإحصائية",    GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsApprove,     NameAr = "اعتماد الإحصائية",    GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticsReopen,      NameAr = "إعادة فتح الإحصائية", GroupAr = "الإحصائية" },
            new { Code = PermissionCodes.StatisticalPeriodsManageStatus, NameAr = "فتح وإغلاق إحصائيات الشهور", GroupAr = "الإحصائية" },
            // الأرصدة
            new { Code = PermissionCodes.BalancesView,   NameAr = "عرض الأرصدة",  GroupAr = "الأرصدة" },
            new { Code = PermissionCodes.BalancesManage, NameAr = "إدارة الأرصدة", GroupAr = "الأرصدة" },
            // التقارير
            new { Code = PermissionCodes.ReportsView,   NameAr = "عرض التقارير",   GroupAr = "التقارير" },
            new { Code = PermissionCodes.ReportsExport, NameAr = "تصدير التقارير", GroupAr = "التقارير" },
            // المستخدمون
            new { Code = PermissionCodes.UsersManage, NameAr = "إدارة المستخدمين", GroupAr = "المستخدمون" },
            // سجل المراجعة
            new { Code = PermissionCodes.AuditView, NameAr = "عرض سجل المراجعة", GroupAr = "سجل المراجعة" },
        };

        foreach (var p in permissions)
        {
            if (!await context.Permissions.AnyAsync(x => x.Code == p.Code))
            {
                context.Permissions.Add(new Permission
                {
                    Code    = p.Code,
                    NameAr  = p.NameAr,
                    GroupAr = p.GroupAr,
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(JudicialDbContext context)
    {
        var roles = new[]
        {
            new { Code = RoleCodes.SystemAdmin,  NameAr = "مدير النظام",        DescriptionAr = "صلاحيات كاملة على النظام" },
            new { Code = RoleCodes.ScopeAdmin,   NameAr = "مدير النطاق",        DescriptionAr = "إدارة محكمة أو نطاق محدد" },
            new { Code = RoleCodes.DataEntry,    NameAr = "مدخل البيانات",      DescriptionAr = "إدخال وتعديل الإحصائيات في مرحلة المسودة" },
            new { Code = RoleCodes.Reviewer,     NameAr = "مراجع",              DescriptionAr = "مراجعة الإحصائيات المقدمة وإرجاعها أو رفعها" },
            new { Code = RoleCodes.Approver,     NameAr = "معتمد",              DescriptionAr = "اعتماد الإحصائيات النهائية" },
            new { Code = RoleCodes.ReportViewer, NameAr = "مستعرض التقارير",   DescriptionAr = "عرض التقارير والإحصائيات المعتمدة فقط" },
            new { Code = RoleCodes.Auditor,      NameAr = "مدقق",               DescriptionAr = "عرض سجل المراجعة والتدقيق" },
        };

        foreach (var r in roles)
        {
            if (!await context.AppRoles.AnyAsync(x => x.Code == r.Code))
            {
                context.AppRoles.Add(new Role
                {
                    Code          = r.Code,
                    NameAr        = r.NameAr,
                    DescriptionAr = r.DescriptionAr,
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRolePermissionsAsync(JudicialDbContext context)
    {
        // تحميل الأدوار والصلاحيات من قاعدة البيانات
        var roles       = await context.AppRoles.ToDictionaryAsync(r => r.Code, r => r.Id);
        var permissions = await context.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id);

        // صلاحيات SystemAdmin — كل شيء
        var allPerms = permissions.Values.ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.SystemAdmin], allPerms);

        // ScopeAdmin — إدارة كل شيء إلا المستخدمين
        var scopeAdminPerms = permissions
            .Where(p => p.Key != PermissionCodes.UsersManage)
            .Select(p => p.Value).ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.ScopeAdmin], scopeAdminPerms);

        // DataEntry
        var dataEntryPerms = new[]
        {
            PermissionCodes.CourtsView, PermissionCodes.JudicialYearsView,
            PermissionCodes.OrganizationView, PermissionCodes.StatisticsView,
            PermissionCodes.StatisticsCreate, PermissionCodes.StatisticsEditDraft,
            PermissionCodes.StatisticsDeleteDraft, PermissionCodes.StatisticsSubmit,
            PermissionCodes.BalancesView,
        }.Select(c => permissions[c]).ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.DataEntry], dataEntryPerms);

        // Reviewer
        var reviewerPerms = new[]
        {
            PermissionCodes.CourtsView, PermissionCodes.JudicialYearsView,
            PermissionCodes.OrganizationView, PermissionCodes.StatisticsView,
            PermissionCodes.StatisticsReview, PermissionCodes.BalancesView,
            PermissionCodes.ReportsView,
        }.Select(c => permissions[c]).ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.Reviewer], reviewerPerms);

        // Approver
        var approverPerms = new[]
        {
            PermissionCodes.CourtsView, PermissionCodes.JudicialYearsView,
            PermissionCodes.OrganizationView, PermissionCodes.StatisticsView,
            PermissionCodes.StatisticsReview, PermissionCodes.StatisticsApprove,
            PermissionCodes.StatisticsReopen, PermissionCodes.BalancesView,
            PermissionCodes.ReportsView, PermissionCodes.ReportsExport,
        }.Select(c => permissions[c]).ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.Approver], approverPerms);

        // ReportViewer
        var reportViewerPerms = new[]
        {
            PermissionCodes.CourtsView, PermissionCodes.StatisticsView,
            PermissionCodes.ReportsView, PermissionCodes.ReportsExport,
        }.Select(c => permissions[c]).ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.ReportViewer], reportViewerPerms);

        // Auditor
        var auditorPerms = new[]
        {
            PermissionCodes.CourtsView, PermissionCodes.StatisticsView,
            PermissionCodes.ReportsView, PermissionCodes.AuditView,
        }.Select(c => permissions[c]).ToList();
        await AddRolePermissionsIfMissing(context, roles[RoleCodes.Auditor], auditorPerms);

        await context.SaveChangesAsync();
    }

    private static async Task AddRolePermissionsIfMissing(
        JudicialDbContext context, long roleId, List<long> permissionIds)
    {
        var existing = await context.AppRolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        foreach (var permId in permissionIds.Except(existing))
        {
            context.AppRolePermissions.Add(new RolePermission
            {
                RoleId       = roleId,
                PermissionId = permId,
            });
        }
    }
}
