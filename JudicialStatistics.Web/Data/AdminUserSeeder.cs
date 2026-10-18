using JudicialStatistics.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Web.Data;

/// <summary>
/// بذر الحساب الافتراضي لمدير النظام وربطه بجدول مستخدمي التطبيق AppUsers
/// </summary>
public static class AdminUserSeeder
{
    public const string DefaultAdminEmail = "admin@court.gov.eg";
    public const string DefaultAdminPassword = "Admin@123456";

    public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminUserSeeder");

        var existingUser = await userManager.FindByEmailAsync(DefaultAdminEmail);
        if (existingUser is null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = DefaultAdminEmail,
                Email = DefaultAdminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, DefaultAdminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                logger.LogError("فشل إنشاء حساب مدير النظام الافتراضي: {Errors}", errors);
                return;
            }

            logger.LogInformation("تم إنشاء حساب مدير النظام الافتراضي بنجاح ({Email})", DefaultAdminEmail);
            existingUser = adminUser;
        }

        // التأكد من وجود سجل AppUser المرتبط
        var appUser = await dbContext.AppUsers.FirstOrDefaultAsync(u => u.IdentitySubject == existingUser.Id);
        if (appUser is null)
        {
            appUser = new AppUser
            {
                IdentitySubject = existingUser.Id,
                FullNameAr = "مدير النظام",
                Email = DefaultAdminEmail,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.AppUsers.Add(appUser);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("تم إنشاء سجل مستخدم التطبيق (AppUser) لمدير النظام.");
        }

        // منح دور مدير النظام (SystemAdmin) على كافة المحاكم الابتدائية (SaaS)
        var adminRole = await dbContext.AppRoles.FirstOrDefaultAsync(r => r.Code == RoleCodes.SystemAdmin);
        var primaryCourts = await dbContext.CourtUnits.Where(c => c.ParentCourtUnitId == null && c.IsActive).ToListAsync();

        if (adminRole is not null && primaryCourts.Count > 0)
        {
            foreach (var court in primaryCourts)
            {
                var hasGrant = await dbContext.UserCourtGrants.AnyAsync(g =>
                    g.UserId == appUser.Id && g.CourtUnitId == court.Id && g.RoleId == adminRole.Id);

                if (!hasGrant)
                {
                    dbContext.UserCourtGrants.Add(new UserCourtGrant
                    {
                        UserId = appUser.Id,
                        CourtUnitId = court.Id,
                        RoleId = adminRole.Id,
                        SessionScopeMode = "AllSessions",
                        ValidFrom = DateOnly.FromDateTime(DateTime.Today),
                        IsActive = true
                    });
                }
            }
            await dbContext.SaveChangesAsync();
            logger.LogInformation("تم منح صلاحيات مدير النظام الكاملة على كافة المحاكم الابتدائية.");
        }
    }
}
