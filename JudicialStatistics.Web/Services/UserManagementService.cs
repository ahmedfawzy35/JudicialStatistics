using JudicialStatistics.Domain.Courts;
using JudicialStatistics.Domain.Identity;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Web.Services;

public record ManagedUserDto(
    long Id,
    string IdentitySubject,
    string FullNameAr,
    string Email,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    long PrimaryCourtId,
    string PrimaryCourtNameAr,
    long RoleId,
    string RoleCode,
    string RoleNameAr,
    string SessionScopeMode,
    int GrantedSessionsCount
);

public class UserCreateModel
{
    public string FullNameAr { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public long PrimaryCourtId { get; set; }
    public long RoleId { get; set; }
    public string SessionScopeMode { get; set; } = "AllSessions";
    public List<long> SelectedSessionIds { get; set; } = [];
}

public class UserUpdateModel
{
    public long Id { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long PrimaryCourtId { get; set; }
    public long RoleId { get; set; }
}

public record SessionAssignmentItemDto(
    long SessionId,
    string SessionNameAr,
    string ScheduleDescription,
    string CircuitNameAr,
    string CourtUnitNameAr,
    bool IsSelected
);

public class UserManagementService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserManagementService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// قراءة المستخدمين مع الفلترة حسب المحكمة الابتدائية وصلاحية المدير
    /// </summary>
    public async Task<List<ManagedUserDto>> GetUsersAsync(long? primaryCourtId, bool isSystemAdmin, long currentAppUserId)
    {
        var grantsQuery = _db.UserCourtGrants
            .AsNoTracking()
            .Include(g => g.CourtUnit)
            .Include(g => g.Role)
            .Include(g => g.SessionGrants)
            .Include(g => g.User)
            .AsQueryable();

        if (primaryCourtId.HasValue && primaryCourtId.Value > 0)
        {
            // نأتي بكافة الوحدات التابعة لهذه المحكمة الابتدائية
            var courtAndDescendants = await GetCourtAndDescendantsIdsAsync(primaryCourtId.Value);
            grantsQuery = grantsQuery.Where(g => courtAndDescendants.Contains(g.CourtUnitId));
        }
        else if (!isSystemAdmin)
        {
            // إذا لم يكن SystemAdmin، نحصر المستخدمين فقط في المحكمة الابتدائية التي ينتمي لها
            var userPrimaryCourtId = await GetUserPrimaryCourtIdAsync(currentAppUserId);
            if (userPrimaryCourtId.HasValue)
            {
                var courtAndDescendants = await GetCourtAndDescendantsIdsAsync(userPrimaryCourtId.Value);
                grantsQuery = grantsQuery.Where(g => courtAndDescendants.Contains(g.CourtUnitId));
            }
        }

        var grants = await grantsQuery.ToListAsync();

        // تجميع السجلات حسب المستخدم
        var grouped = grants
            .GroupBy(g => g.UserId)
            .ToList();

        var result = new List<ManagedUserDto>();

        foreach (var group in grouped)
        {
            var user = group.First().User;
            if (user == null) continue;

            // اختيار المنح الأساسي (أعلى دور)
            var activeGrants = group.Where(g => g.IsActive).ToList();
            var primaryGrant = activeGrants.OrderBy(g => GetRoleWeight(g.Role?.Code)).FirstOrDefault()
                               ?? group.First();

            var court = primaryGrant.CourtUnit;
            string courtName = court?.NameAr ?? "غير محدد";
            long courtId = court?.Id ?? 0;

            // إذا كانت المحكمة ليست ابتدائية، نبحث عن الابتدائية التابعة لها
            if (court != null && court.ParentCourtUnitId != null)
            {
                var rootCourt = await FindRootCourtAsync(court.Id);
                if (rootCourt != null)
                {
                    courtName = rootCourt.NameAr;
                    courtId = rootCourt.Id;
                }
            }

            result.Add(new ManagedUserDto(
                user.Id,
                user.IdentitySubject,
                user.FullNameAr,
                user.Email ?? string.Empty,
                user.IsEnabled,
                user.CreatedAt,
                user.LastLoginAt,
                courtId,
                courtName,
                primaryGrant.RoleId,
                primaryGrant.Role?.Code ?? string.Empty,
                primaryGrant.Role?.NameAr ?? "مستخدم",
                primaryGrant.SessionScopeMode ?? "AllSessions",
                primaryGrant.SessionGrants.Count
            ));
        }

        return result.OrderBy(u => u.FullNameAr).ToList();
    }

    /// <summary>
    /// إنشاء مستخدم جديد في Identity وفي جدول مستخدمي التطبيق وإسناد المحكمة والدور له
    /// </summary>
    public async Task<(bool Success, string Message, long NewUserId)> CreateUserAsync(
        UserCreateModel model, 
        bool isSystemAdmin, 
        long currentAppUserId)
    {
        // 1. التحقق من المدخلات
        if (string.IsNullOrWhiteSpace(model.FullNameAr))
            return (false, "الاسم الكامل مطلوب.", 0);

        if (string.IsNullOrWhiteSpace(model.Email))
            return (false, "البريد الإلكتروني مطلوب.", 0);

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            return (false, "كلمة المرور يجب ألا تقل عن 6 أحرف.", 0);

        if (model.Password != model.ConfirmPassword)
            return (false, "كلمة المرور وتأكيدها غير متطابقين.", 0);

        if (model.PrimaryCourtId <= 0)
            return (false, "يجب تحديد المحكمة الابتدائية.", 0);

        if (model.RoleId <= 0)
            return (false, "يجب تحديد الدور الوظيفي.", 0);

        // 2. التحقق من صلاحيات المنشئ (مدير قسم الإحصاء لا ينشئ SystemAdmin)
        var role = await _db.AppRoles.FindAsync(model.RoleId);
        if (role == null) return (false, "الدور المحدد غير موجود.", 0);

        if (!isSystemAdmin && role.Code == RoleCodes.SystemAdmin)
        {
            return (false, "لا يمكنك إنشاء مستخدم بدور مدير نظام.", 0);
        }

        // إذا لم يكن المنشئ SystemAdmin، نضمن أنه ينشئ المستخدم في محكمته فقط
        if (!isSystemAdmin)
        {
            var userPrimaryCourtId = await GetUserPrimaryCourtIdAsync(currentAppUserId);
            if (userPrimaryCourtId.HasValue && userPrimaryCourtId.Value != model.PrimaryCourtId)
            {
                return (false, "لا يمكنك إنشاء مستخدمين خارج نطاق محكمتك الابتدائية.", 0);
            }
        }

        // 3. التحقق من عدم تكرار البريد الإلكتروني
        var existingIdentity = await _userManager.FindByEmailAsync(model.Email);
        if (existingIdentity != null)
        {
            return (false, "البريد الإلكتروني مسجل مسبقاً في النظام.", 0);
        }

        // 4. إنشاء حساب Identity
        var identityUser = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(identityUser, model.Password);
        if (!createResult.Succeeded)
        {
            var err = string.Join(" | ", createResult.Errors.Select(e => e.Description));
            return (false, $"فشل إنشاء الحساب: {err}", 0);
        }

        // 5. إنشاء سجل AppUser
        var appUser = new AppUser
        {
            IdentitySubject = identityUser.Id,
            FullNameAr = model.FullNameAr.Trim(),
            Email = model.Email.Trim(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.AppUsers.Add(appUser);
        await _db.SaveChangesAsync();

        // 6. إنشاء منح المحكمة (UserCourtGrant)
        var grant = new UserCourtGrant
        {
            UserId = appUser.Id,
            CourtUnitId = model.PrimaryCourtId,
            RoleId = model.RoleId,
            SessionScopeMode = model.SessionScopeMode,
            ValidFrom = DateOnly.FromDateTime(DateTime.Today),
            IsActive = true
        };
        _db.UserCourtGrants.Add(grant);
        await _db.SaveChangesAsync();

        // 7. إذا كان النطاق جلسات محددة، نضيف منح الجلسات
        if (model.SessionScopeMode == "SelectedSessions" && model.SelectedSessionIds.Count > 0)
        {
            foreach (var sId in model.SelectedSessionIds)
            {
                _db.UserSessionGrants.Add(new UserSessionGrant
                {
                    CourtGrantId = grant.Id,
                    SessionId = sId
                });
            }
            await _db.SaveChangesAsync();
        }

        return (true, $"تم إنشاء حساب المستخدم {appUser.FullNameAr} بنجاح.", appUser.Id);
    }

    /// <summary>
    /// تحديث بيانات المستخدم (الاسم، البريد، المحكمة، الدور)
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateUserAsync(
        UserUpdateModel model, 
        bool isSystemAdmin, 
        long currentAppUserId)
    {
        var appUser = await _db.AppUsers.FindAsync(model.Id);
        if (appUser == null) return (false, "المستخدم غير موجود.");

        // تحديث الاسم
        appUser.FullNameAr = model.FullNameAr.Trim();

        // تحديث البريد في AppUser و Identity
        if (!string.Equals(appUser.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var identityUser = await _userManager.FindByIdAsync(appUser.IdentitySubject)
                               ?? await _userManager.FindByEmailAsync(appUser.Email ?? "");

            if (identityUser != null)
            {
                identityUser.Email = model.Email.Trim();
                identityUser.UserName = model.Email.Trim();
                var idResult = await _userManager.UpdateAsync(identityUser);
                if (!idResult.Succeeded)
                {
                    return (false, "فشل تحديث البريد في نظام المصادقة: " + string.Join(" | ", idResult.Errors.Select(e => e.Description)));
                }
            }
            appUser.Email = model.Email.Trim();
        }

        // تحديث المنح (المحكمة والدور)
        var grant = await _db.UserCourtGrants.FirstOrDefaultAsync(g => g.UserId == appUser.Id && g.IsActive);
        if (grant != null)
        {
            if (isSystemAdmin || model.PrimaryCourtId == grant.CourtUnitId)
            {
                grant.CourtUnitId = model.PrimaryCourtId;
            }
            grant.RoleId = model.RoleId;
        }

        await _db.SaveChangesAsync();
        return (true, "تم تحديث بيانات المستخدم بنجاح.");
    }

    /// <summary>
    /// تغيير أو إعادة تعيين كلمة المرور للمستخدم بواسطة المدير أو مدير الإحصاء في النطاق المسموح
    /// </summary>
    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        long appUserId, 
        string newPassword, 
        bool isSystemAdmin, 
        long currentAppUserId)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "كلمة المرور يجب ألا تقل عن 6 أحرف.");

        var appUser = await _db.AppUsers.FindAsync(appUserId);
        if (appUser == null) return (false, "المستخدم غير موجود.");

        // التحقق من نطاق المدير إذا لم يكن SystemAdmin
        if (!isSystemAdmin)
        {
            var userPrimaryCourtId = await GetUserPrimaryCourtIdAsync(appUserId);
            var managerPrimaryCourtId = await GetUserPrimaryCourtIdAsync(currentAppUserId);
            if (userPrimaryCourtId != managerPrimaryCourtId)
            {
                return (false, "لا يمكنك تغيير كلمة المرور لمستخدم خارج نطاق محكمتك الابتدائية.");
            }
        }

        var identityUser = await _userManager.FindByIdAsync(appUser.IdentitySubject)
                           ?? await _userManager.FindByEmailAsync(appUser.Email ?? "");

        if (identityUser == null)
            return (false, "لم يتم العثور على حساب المستخدم في نظام المصادقة.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(identityUser);
        var resetResult = await _userManager.ResetPasswordAsync(identityUser, token, newPassword);

        if (!resetResult.Succeeded)
        {
            var err = string.Join(" | ", resetResult.Errors.Select(e => e.Description));
            return (false, $"فشل إعادة تعيين كلمة المرور: {err}");
        }

        return (true, $"تم تغيير كلمة المرور للمستخدم {appUser.FullNameAr} بنجاح.");
    }

    /// <summary>
    /// إيقاف أو تفعيل المستخدم
    /// </summary>
    public async Task<(bool Success, string Message)> ToggleUserActiveAsync(long appUserId)
    {
        var appUser = await _db.AppUsers.FindAsync(appUserId);
        if (appUser == null) return (false, "المستخدم غير موجود.");

        appUser.IsEnabled = !appUser.IsEnabled;

        // إقفال أو فتح حساب Identity المقابل
        var identityUser = await _userManager.FindByIdAsync(appUser.IdentitySubject)
                           ?? await _userManager.FindByEmailAsync(appUser.Email ?? "");

        if (identityUser != null)
        {
            if (!appUser.IsEnabled)
            {
                await _userManager.SetLockoutEndDateAsync(identityUser, DateTimeOffset.UtcNow.AddYears(50));
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(identityUser, null);
            }
        }

        await _db.SaveChangesAsync();
        string statusText = appUser.IsEnabled ? "تفعيل" : "إيقاف";
        return (true, $"تم {statusText} حساب المستخدم بنجاح.");
    }

    /// <summary>
    /// حذف المستخدم (حذف ناعم وتعطيل كافة الصلاحيات والحساب)
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteUserAsync(long appUserId)
    {
        var appUser = await _db.AppUsers.FindAsync(appUserId);
        if (appUser == null) return (false, "المستخدم غير موجود.");

        // حذف ناعم
        appUser.IsEnabled = false;

        var grants = await _db.UserCourtGrants.Where(g => g.UserId == appUserId).ToListAsync();
        foreach (var g in grants) g.IsActive = false;

        var identityUser = await _userManager.FindByIdAsync(appUser.IdentitySubject)
                           ?? await _userManager.FindByEmailAsync(appUser.Email ?? "");

        if (identityUser != null)
        {
            await _userManager.SetLockoutEndDateAsync(identityUser, DateTimeOffset.UtcNow.AddYears(100));
        }

        await _db.SaveChangesAsync();
        return (true, $"تم حذف وتعطيل حساب {appUser.FullNameAr} بنجاح.");
    }

    /// <summary>
    /// استرجاع قائمة كافة الجلسات المتاحة تحت المحكمة الابتدائية (لتحديد الإسناد للمستخدم)
    /// </summary>
    public async Task<List<SessionAssignmentItemDto>> GetAvailableSessionsForScopeAsync(
        long primaryCourtId, 
        long appUserId)
    {
        var courtIds = await GetCourtAndDescendantsIdsAsync(primaryCourtId);

        // الجلسات الممنوحة حالياً لهذا المستخدم
        var userGrant = await _db.UserCourtGrants
            .Include(g => g.SessionGrants)
            .FirstOrDefaultAsync(g => g.UserId == appUserId && g.IsActive);

        var grantedSessionIds = userGrant?.SessionGrants.Select(sg => sg.SessionId).ToHashSet()
                                ?? new HashSet<long>();

        // استخراج الجلسات النشطة في هذه المحاكم
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Circuit).ThenInclude(c => c.CourtUnit)
            .Include(s => s.Versions)
            .Where(s => courtIds.Contains(s.Circuit.CourtUnitId) && !s.IsClosed)
            .OrderBy(s => s.Circuit.CourtUnit.NameAr)
            .ThenBy(s => s.Circuit.NameAr)
            .ThenBy(s => s.NameAr)
            .ToListAsync();

        return sessions.Select(s =>
        {
            var latestVersion = s.Versions.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault();
            return new SessionAssignmentItemDto(
                s.Id,
                s.NameAr,
                latestVersion?.ScheduleDescription ?? s.NameAr,
                s.Circuit.NameAr,
                s.Circuit.CourtUnit.NameAr,
                grantedSessionIds.Contains(s.Id)
            );
        }).ToList();
    }

    /// <summary>
    /// حفظ إسناد الجلسات والمحاكم الجزئية والدوائر لمستخدم تابع للمحكمة
    /// </summary>
    public async Task<(bool Success, string Message)> SaveUserSessionScopeAsync(
        long appUserId, 
        long primaryCourtId, 
        string scopeMode, 
        List<long> selectedSessionIds)
    {
        var grant = await _db.UserCourtGrants
            .Include(g => g.SessionGrants)
            .FirstOrDefaultAsync(g => g.UserId == appUserId && g.IsActive);

        if (grant == null)
        {
            return (false, "لم يتم العثور على منح سارٍ لهذا المستخدم.");
        }

        grant.SessionScopeMode = scopeMode;

        // إزالة المنح السابقة
        _db.UserSessionGrants.RemoveRange(grant.SessionGrants);

        if (scopeMode == "SelectedSessions" && selectedSessionIds.Count > 0)
        {
            foreach (var sId in selectedSessionIds)
            {
                _db.UserSessionGrants.Add(new UserSessionGrant
                {
                    CourtGrantId = grant.Id,
                    SessionId = sId
                });
            }
        }

        await _db.SaveChangesAsync();
        return (true, "تم تحديث إسناد ونطاق جلسات المستخدم بنجاح.");
    }

    // ──────────────── دوال مساعدة ────────────────

    public async Task<List<long>> GetCourtAndDescendantsIdsAsync(long rootCourtId)
    {
        var allCourts = await _db.CourtUnits.AsNoTracking().Select(c => new { c.Id, c.ParentCourtUnitId }).ToListAsync();
        var result = new List<long>();
        var queue = new Queue<long>();
        queue.Enqueue(rootCourtId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var child in allCourts.Where(c => c.ParentCourtUnitId == current))
            {
                queue.Enqueue(child.Id);
            }
        }
        return result;
    }

    private async Task<long?> GetUserPrimaryCourtIdAsync(long appUserId)
    {
        var grant = await _db.UserCourtGrants
            .AsNoTracking()
            .Include(g => g.CourtUnit)
            .FirstOrDefaultAsync(g => g.UserId == appUserId && g.IsActive);

        if (grant?.CourtUnit == null) return null;

        if (grant.CourtUnit.ParentCourtUnitId == null) return grant.CourtUnitId;

        var root = await FindRootCourtAsync(grant.CourtUnitId);
        return root?.Id ?? grant.CourtUnitId;
    }

    private async Task<CourtUnit?> FindRootCourtAsync(long courtId)
    {
        var current = await _db.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courtId);
        while (current != null)
        {
            if (current.ParentCourtUnitId == null) return current;
            current = await _db.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == current.ParentCourtUnitId);
        }
        return null;
    }

    private static int GetRoleWeight(string? code) => code switch
    {
        RoleCodes.SystemAdmin => 1,
        RoleCodes.ScopeAdmin => 2,
        RoleCodes.Approver => 3,
        RoleCodes.Reviewer => 4,
        RoleCodes.DataEntry => 5,
        _ => 10
    };
}
