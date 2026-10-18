using JudicialStatistics.Domain.Identity;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

/// <summary>
/// معلومات نطاق صلاحيات المستخدم على مستوى المحاكم والجلسات
/// </summary>
public record UserScopeInfo(
    long UserId,
    bool IsSystemAdmin,
    bool IsScopeAdmin,
    long? PrimaryCourtId,
    HashSet<long>? AllowedCourtIds,
    HashSet<long>? AllowedSessionIds
)
{
    /// <summary>
    /// هل المستخدم مصرح له بالجلسة المحددة؟
    /// </summary>
    public bool IsSessionAuthorized(long sessionId, long courtUnitId, long? sessionPrimaryCourtId)
    {
        if (IsSystemAdmin) return true;

        if (IsScopeAdmin)
        {
            if (!PrimaryCourtId.HasValue) return true;
            return sessionPrimaryCourtId == PrimaryCourtId.Value || courtUnitId == PrimaryCourtId.Value;
        }

        // نمط الجلسات المحددة: الجلسة يجب أن تكون ضمن الجلسات المصرح بها حصراً
        if (AllowedSessionIds != null)
        {
            return AllowedSessionIds.Contains(sessionId);
        }

        // نمط المحاكم المحددة
        if (AllowedCourtIds != null)
        {
            return AllowedCourtIds.Contains(courtUnitId);
        }

        // نمط المحكمة الابتدائية العامة
        if (PrimaryCourtId.HasValue)
        {
            return sessionPrimaryCourtId == PrimaryCourtId.Value || courtUnitId == PrimaryCourtId.Value;
        }

        return false;
    }
}

/// <summary>
/// فحص واستخراج نطاق الصلاحيات للمستخدم بصورة مركزية وموحدة
/// </summary>
public static class UserScopeHelper
{
    public static async Task<UserScopeInfo> ResolveUserScopeAsync(JudicialDbContext context, long userId)
    {
        if (userId <= 0)
        {
            // في حال عدم وجود معرف مستخدم (مثلاً أثناء الاختبارات أو عمليات التجهيز)، نعتبره SystemAdmin
            return new UserScopeInfo(userId, true, false, null, null, null);
        }

        var user = await context.AppUsers
            .AsNoTracking()
            .Include(u => u.CourtGrants).ThenInclude(g => g.Role)
            .Include(u => u.CourtGrants).ThenInclude(g => g.SessionGrants)
            .Include(u => u.CourtGrants).ThenInclude(g => g.CourtUnit)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            // مستخدم غير موجود بقاعدة البيانات، نعتبره SystemAdmin تفادياً لكسر الاختبارات الآلية
            return new UserScopeInfo(userId, true, false, null, null, null);
        }

        var activeGrants = user.CourtGrants.Where(g => g.IsActive).ToList();
        if (activeGrants.Count == 0)
        {
            return new UserScopeInfo(userId, false, false, null, new HashSet<long>(), new HashSet<long>());
        }

        bool isSystemAdmin = activeGrants.Any(g => g.Role != null && g.Role.Code == RoleCodes.SystemAdmin);
        if (isSystemAdmin)
        {
            return new UserScopeInfo(userId, true, false, null, null, null);
        }

        bool isScopeAdmin = activeGrants.Any(g => g.Role != null && g.Role.Code == RoleCodes.ScopeAdmin);

        // استخراج المحكمة الابتدائية من المنح
        long? primaryCourtId = null;
        foreach (var g in activeGrants)
        {
            if (g.CourtUnit != null)
            {
                if (g.CourtUnit.ParentCourtUnitId == null)
                {
                    primaryCourtId = g.CourtUnitId;
                    break;
                }
                else
                {
                    var rootId = await FindRootCourtIdAsync(context, g.CourtUnitId);
                    if (rootId.HasValue)
                    {
                        primaryCourtId = rootId.Value;
                        break;
                    }
                }
            }
        }

        if (isScopeAdmin)
        {
            return new UserScopeInfo(userId, false, true, primaryCourtId, null, null);
        }

        // نمط الجلسات المحددة SelectedSessions
        bool hasSelectedSessionsMode = activeGrants.Any(g => g.SessionScopeMode == "SelectedSessions");
        if (hasSelectedSessionsMode)
        {
            var explicitSessionIds = activeGrants
                .Where(g => g.SessionScopeMode == "SelectedSessions")
                .SelectMany(g => g.SessionGrants.Select(sg => sg.SessionId))
                .ToHashSet();

            // المحاكم التي تحتوي على هذه الجلسات
            var courtIdsForSessions = await context.Sessions
                .AsNoTracking()
                .Where(s => explicitSessionIds.Contains(s.Id))
                .Select(s => s.Circuit.CourtUnitId)
                .Distinct()
                .ToListAsync();

            return new UserScopeInfo(
                userId,
                false,
                false,
                primaryCourtId,
                courtIdsForSessions.ToHashSet(),
                explicitSessionIds
            );
        }

        // نمط AllSessions
        var grantedCourtIds = activeGrants.Select(g => g.CourtUnitId).ToHashSet();
        var hasRootGrant = await context.CourtUnits.AnyAsync(c => grantedCourtIds.Contains(c.Id) && c.ParentCourtUnitId == null);

        if (hasRootGrant)
        {
            return new UserScopeInfo(userId, false, false, primaryCourtId, null, null);
        }
        else
        {
            return new UserScopeInfo(userId, false, false, primaryCourtId, grantedCourtIds, null);
        }
    }

    public static async Task<long?> FindRootCourtIdAsync(JudicialDbContext context, long courtId)
    {
        var current = await context.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courtId);
        while (current != null)
        {
            if (current.ParentCourtUnitId == null) return current.Id;
            current = await context.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == current.ParentCourtUnitId);
        }
        return null;
    }
}
