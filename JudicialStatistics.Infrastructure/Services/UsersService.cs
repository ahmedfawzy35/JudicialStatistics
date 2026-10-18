using JudicialStatistics.Application.Identity;
using JudicialStatistics.Domain.Identity;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

/// <summary>
/// خدمة إدارة المستخدمين الموسعة — تدعم SaaS متعدد المحاكم الابتدائية
/// </summary>
public class UsersService : IUsersService
{
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public UsersService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ──────────────── قراءة المستخدمين ────────────────

    public async Task<List<AppUserDto>> GetUsersAsync(long? primaryCourtId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var grantsQuery = context.UserCourtGrants
            .Include(g => g.CourtUnit)
            .Include(g => g.Role)
            .AsQueryable();

        if (primaryCourtId.HasValue)
        {
            // فلترة بالمحكمة الابتدائية: نجد المحكمة ذاتها وكل وحداتها الفرعية
            var courtIds = await GetCourtAndDescendantsAsync(context, primaryCourtId.Value);
            grantsQuery = grantsQuery.Where(g => courtIds.Contains(g.CourtUnitId));
        }

        var grants = await grantsQuery.Where(g => g.IsActive).ToListAsync();
        var userIds = grants.Select(g => g.UserId).Distinct().ToList();

        var users = await context.AppUsers
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.FullNameAr)
            .ToListAsync();

        return users.Select(u =>
        {
            var userGrants = grants.Where(g => g.UserId == u.Id).ToList();
            return new AppUserDto(
                u.Id,
                u.IdentitySubject,
                u.FullNameAr,
                u.Email ?? string.Empty,
                u.IsEnabled,
                u.CreatedAt,
                u.LastLoginAt,
                userGrants.Select(g => g.Role.NameAr).Distinct().ToList(),
                userGrants.Select(g => g.CourtUnit.NameAr).Distinct().ToList(),
                userGrants.Select(g => new GrantSummary(g.Id, g.CourtUnitId, g.CourtUnit.NameAr, g.RoleId, g.Role.NameAr, g.Role.Code, g.IsActive)).ToList()
            );
        }).ToList();
    }

    public async Task<AppUserDto?> GetByIdAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var u = await context.AppUsers.FindAsync(id);
        if (u is null) return null;

        var userGrants = await context.UserCourtGrants
            .Include(g => g.CourtUnit)
            .Include(g => g.Role)
            .Where(g => g.UserId == id)
            .ToListAsync();

        return new AppUserDto(
            u.Id,
            u.IdentitySubject,
            u.FullNameAr,
            u.Email ?? string.Empty,
            u.IsEnabled,
            u.CreatedAt,
            u.LastLoginAt,
            userGrants.Where(g => g.IsActive).Select(g => g.Role.NameAr).Distinct().ToList(),
            userGrants.Where(g => g.IsActive).Select(g => g.CourtUnit.NameAr).Distinct().ToList(),
            userGrants.Select(g => new GrantSummary(g.Id, g.CourtUnitId, g.CourtUnit.NameAr, g.RoleId, g.Role.NameAr, g.Role.Code, g.IsActive)).ToList()
        );
    }

    // ──────────────── تعديل المستخدمين ────────────────

    public async Task<bool> UpdateUserAsync(long id, string fullNameAr, string email)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.AppUsers.FindAsync(id);
        if (user is null) return false;

        user.FullNameAr = fullNameAr;
        user.Email = email;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task ToggleUserActiveAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.AppUsers.FindAsync(id);
        if (user is not null)
        {
            user.IsEnabled = !user.IsEnabled;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> DeleteUserAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var user = await context.AppUsers.FindAsync(id);
        if (user is null) return false;

        // حذف ناعم: تعطيل المستخدم فقط
        user.IsEnabled = false;
        var grants = await context.UserCourtGrants.Where(g => g.UserId == id).ToListAsync();
        foreach (var g in grants) g.IsActive = false;
        await context.SaveChangesAsync();
        return true;
    }

    // ──────────────── المنح (Grants) ────────────────

    public async Task<List<UserCourtGrantDto>> GetGrantsAsync(long? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.UserCourtGrants
            .Include(g => g.User)
            .Include(g => g.CourtUnit)
            .Include(g => g.Role)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(g => g.UserId == userId.Value);

        return await query
            .OrderBy(g => g.User.FullNameAr)
            .Select(g => new UserCourtGrantDto(
                g.Id,
                g.UserId,
                g.User.FullNameAr,
                g.CourtUnitId,
                g.CourtUnit.NameAr,
                g.RoleId,
                g.Role.NameAr,
                g.SessionScopeMode,
                g.IsActive
            ))
            .ToListAsync();
    }

    public async Task AssignCourtGrantAsync(UserCourtGrantCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // هل يوجد منح نشط على نفس المحكمة؟
        var existing = await context.UserCourtGrants.FirstOrDefaultAsync(g =>
            g.UserId == cmd.UserId && g.CourtUnitId == cmd.CourtUnitId && g.IsActive);

        if (existing is not null)
        {
            // تحديث الدور فقط
            existing.RoleId = cmd.RoleId;
            existing.SessionScopeMode = cmd.SessionScopeMode;
        }
        else
        {
            context.UserCourtGrants.Add(new UserCourtGrant
            {
                UserId = cmd.UserId,
                CourtUnitId = cmd.CourtUnitId,
                RoleId = cmd.RoleId,
                SessionScopeMode = cmd.SessionScopeMode,
                ValidFrom = DateOnly.FromDateTime(DateTime.Today),
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task RevokeCourtGrantAsync(long grantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var grant = await context.UserCourtGrants.FindAsync(grantId);
        if (grant is not null)
        {
            grant.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    // ──────────────── قوائم المساعدة ────────────────

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AppRoles
            .OrderBy(r => r.Id)
            .Select(r => new RoleDto(r.Id, r.Code, r.NameAr))
            .ToListAsync();
    }

    public async Task<List<PrimaryCourtDto>> GetPrimaryCourtsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.CourtUnits
            .Where(c => c.ParentCourtUnitId == null && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new PrimaryCourtDto(c.Id, c.Code, c.NameAr))
            .ToListAsync();
    }

    public async Task<List<PrimaryCourtDto>> GetAllowedPrimaryCourtsAsync(long appUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var grants = await context.UserCourtGrants
            .Include(g => g.Role)
            .Include(g => g.CourtUnit)
            .Where(g => g.UserId == appUserId && g.IsActive)
            .ToListAsync();

        bool isSystemAdmin = grants.Any(g => g.Role.Code == RoleCodes.SystemAdmin);

        if (isSystemAdmin)
        {
            // SystemAdmin يرى المحاكم التي لديه عليها منح — أو كل المحاكم الابتدائية
            var grantedCourtIds = grants.Select(g => g.CourtUnitId).Distinct().ToList();
            return await context.CourtUnits
                .Where(c => c.ParentCourtUnitId == null && c.IsActive && (grantedCourtIds.Contains(c.Id)))
                .OrderBy(c => c.NameAr)
                .Select(c => new PrimaryCourtDto(c.Id, c.Code, c.NameAr))
                .ToListAsync();
        }
        else
        {
            // غير SystemAdmin: فقط المحكمة الابتدائية المرتبط بها
            var courtIds = grants.Select(g => g.CourtUnitId).Distinct().ToList();
            return await context.CourtUnits
                .Where(c => c.ParentCourtUnitId == null && c.IsActive && courtIds.Contains(c.Id))
                .OrderBy(c => c.NameAr)
                .Select(c => new PrimaryCourtDto(c.Id, c.Code, c.NameAr))
                .ToListAsync();
        }
    }

    // ──────────────── مساعد: الوحدات الفرعية ────────────────

    private static async Task<List<long>> GetCourtAndDescendantsAsync(JudicialDbContext context, long rootId)
    {
        var allCourts = await context.CourtUnits.Select(c => new { c.Id, c.ParentCourtUnitId }).ToListAsync();
        var result = new List<long>();
        var queue = new Queue<long>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var child in allCourts.Where(c => c.ParentCourtUnitId == current))
                queue.Enqueue(child.Id);
        }
        return result;
    }
}
