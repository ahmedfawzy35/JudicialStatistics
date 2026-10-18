using System.Security.Claims;
using JudicialStatistics.Domain.Identity;
using JudicialStatistics.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JudicialStatistics.Web.Services;

public record PrimaryCourtInfo(long Id, string Code, string NameAr);

/// <summary>
/// حالة المحكمة الابتدائية الحالية للجلسة (Scoped) — تدعم نظام SaaS متعدد المحاكم الابتدائية
/// تعتمد على IServiceScopeFactory لعزل استعلامات قاعدة البيانات وتجنب أي تعارض خيوط مع DbContext في Blazor Server
/// </summary>
public class CurrentCourtState
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Task? _initTask;
    private readonly object _lock = new();

    public CurrentCourtState(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public long? SelectedPrimaryCourtId { get; private set; }
    public string SelectedPrimaryCourtName { get; private set; } = "المحكمة الابتدائية";
    public List<PrimaryCourtInfo> AllowedPrimaryCourts { get; private set; } = [];
    public bool IsSystemAdmin { get; private set; }
    public bool IsScopeAdmin { get; private set; }
    public bool CanSwitchCourt => IsSystemAdmin && AllowedPrimaryCourts.Count > 1;
    public HashSet<string> Permissions { get; private set; } = [];
    public bool HasPermission(string permissionCode) => IsSystemAdmin || Permissions.Contains(permissionCode);
    public bool CanManagePeriodStatus => IsSystemAdmin || IsScopeAdmin || HasPermission(PermissionCodes.StatisticalPeriodsManageStatus);
    public long CurrentAppUserId { get; private set; }
    public string CurrentUserFullName { get; private set; } = string.Empty;
    public string CurrentUserEmail { get; private set; } = string.Empty;
    public string CurrentUserRoleCode { get; private set; } = string.Empty;
    public string CurrentUserRoleName { get; private set; } = string.Empty;
    public bool IsInitialized { get; private set; }

    public event Action? OnChange;

    public void SelectPrimaryCourt(long courtId)
    {
        var court = AllowedPrimaryCourts.FirstOrDefault(c => c.Id == courtId);
        if (court != null && SelectedPrimaryCourtId != court.Id)
        {
            SelectedPrimaryCourtId = court.Id;
            SelectedPrimaryCourtName = court.NameAr;
            NotifyStateChanged();
        }
    }

    public Task EnsureInitializedAsync(ClaimsPrincipal principal)
    {
        lock (_lock)
        {
            if (_initTask != null) return _initTask;
            _initTask = InitializeInternalAsync(principal);
            return _initTask;
        }
    }

    public Task InitializeAsync(ClaimsPrincipal principal, ApplicationDbContext? _ = null)
    {
        return EnsureInitializedAsync(principal);
    }

    private async Task InitializeInternalAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            Reset();
            return;
        }

        var identitySubject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? principal.Identity.Name
                              ?? string.Empty;
        var email = principal.Identity.Name;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var appUser = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdentitySubject == identitySubject || u.Email == email);

        if (appUser == null)
        {
            Reset();
            return;
        }

        CurrentAppUserId = appUser.Id;
        CurrentUserFullName = appUser.FullNameAr;
        CurrentUserEmail = appUser.Email ?? string.Empty;

        // قراءة المنح النشطة مع الأدوار ووحدات المحاكم
        var grants = await dbContext.UserCourtGrants
            .AsNoTracking()
            .Include(g => g.Role)
            .Include(g => g.CourtUnit)
            .Where(g => g.UserId == appUser.Id && g.IsActive)
            .ToListAsync();

        IsSystemAdmin = grants.Any(g => g.Role != null && g.Role.Code == RoleCodes.SystemAdmin);
        IsScopeAdmin = grants.Any(g => g.Role != null && g.Role.Code == RoleCodes.ScopeAdmin);

        var roleIds = grants.Where(g => g.Role != null).Select(g => g.Role!.Id).Distinct().ToList();
        var perms = await dbContext.AppRolePermissions
            .AsNoTracking()
            .Include(rp => rp.Permission)
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();
        Permissions = new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase);

        var topRole = grants.OrderBy(g => GetRolePriority(g.Role?.Code)).FirstOrDefault()?.Role;
        CurrentUserRoleCode = topRole?.Code ?? (IsSystemAdmin ? RoleCodes.SystemAdmin : RoleCodes.DataEntry);
        CurrentUserRoleName = topRole?.NameAr ?? (IsSystemAdmin ? "مدير النظام" : "مدخل بيانات");

        // تحديد المحاكم الابتدائية المسموحة للمستخدم
        if (IsSystemAdmin)
        {
            // مدير النظام: المحاكم الابتدائية التي لديه عليها منح، أو كافة المحاكم الابتدائية في النظام إذا كان مدير النظام العام
            var grantedCourtIds = grants.Select(g => g.CourtUnitId).Distinct().ToList();
            var allPrimaryCourts = await dbContext.CourtUnits
                .AsNoTracking()
                .Where(c => c.ParentCourtUnitId == null && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new PrimaryCourtInfo(c.Id, c.Code, c.NameAr))
                .ToListAsync();

            if (grantedCourtIds.Count > 0 && allPrimaryCourts.Any(c => grantedCourtIds.Contains(c.Id)))
            {
                AllowedPrimaryCourts = allPrimaryCourts.Where(c => grantedCourtIds.Contains(c.Id)).ToList();
            }
            else
            {
                AllowedPrimaryCourts = allPrimaryCourts;
            }
        }
        else
        {
            // غير مدير النظام: يحصل على المحكمة الابتدائية التابع لها فقط
            // نتحقق من المحكمة المرتبطة بمنح المستخدم: إما هي المحكمة الابتدائية مباشرة أو محكمة تابعة لها
            var grantedCourtIds = grants.Select(g => g.CourtUnitId).Distinct().ToList();
            var courts = await dbContext.CourtUnits
                .AsNoTracking()
                .Where(c => grantedCourtIds.Contains(c.Id) && c.IsActive)
                .ToListAsync();

            var primaryCourtIds = new HashSet<long>();
            foreach (var c in courts)
            {
                if (c.ParentCourtUnitId == null)
                {
                    primaryCourtIds.Add(c.Id);
                }
                else
                {
                    // العثور على المحكمة الابتدائية الجذر
                    var rootId = await FindRootCourtIdAsync(dbContext, c.Id);
                    if (rootId.HasValue) primaryCourtIds.Add(rootId.Value);
                }
            }

            AllowedPrimaryCourts = await dbContext.CourtUnits
                .AsNoTracking()
                .Where(c => primaryCourtIds.Contains(c.Id) && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new PrimaryCourtInfo(c.Id, c.Code, c.NameAr))
                .ToListAsync();

            // إذا لم يتم العثور على محكمة محددة، نربطه بأول محكمة ابتدائية كافتراضي
            if (AllowedPrimaryCourts.Count == 0)
            {
                var defaultPrimary = await dbContext.CourtUnits
                    .AsNoTracking()
                    .Where(c => c.ParentCourtUnitId == null && c.IsActive)
                    .OrderBy(c => c.Id)
                    .Select(c => new PrimaryCourtInfo(c.Id, c.Code, c.NameAr))
                    .FirstOrDefaultAsync();

                if (defaultPrimary != null)
                {
                    AllowedPrimaryCourts.Add(defaultPrimary);
                }
            }
        }

        // تحديد المحكمة المحددة حالياً
        if (!SelectedPrimaryCourtId.HasValue || !AllowedPrimaryCourts.Any(c => c.Id == SelectedPrimaryCourtId.Value))
        {
            var first = AllowedPrimaryCourts.FirstOrDefault();
            SelectedPrimaryCourtId = first?.Id;
            SelectedPrimaryCourtName = first?.NameAr ?? "المحكمة الابتدائية";
        }
        else
        {
            SelectedPrimaryCourtName = AllowedPrimaryCourts.First(c => c.Id == SelectedPrimaryCourtId.Value).NameAr;
        }

        IsInitialized = true;
        NotifyStateChanged();
    }

    private void Reset()
    {
        _initTask = null;
        SelectedPrimaryCourtId = null;
        SelectedPrimaryCourtName = "المحكمة الابتدائية";
        AllowedPrimaryCourts.Clear();
        Permissions.Clear();
        IsSystemAdmin = false;
        IsScopeAdmin = false;
        CurrentAppUserId = 0;
        CurrentUserFullName = string.Empty;
        CurrentUserEmail = string.Empty;
        CurrentUserRoleCode = string.Empty;
        CurrentUserRoleName = string.Empty;
        IsInitialized = false;
        NotifyStateChanged();
    }

    private static int GetRolePriority(string? code) => code switch
    {
        RoleCodes.SystemAdmin => 1,
        RoleCodes.ScopeAdmin => 2,
        RoleCodes.Approver => 3,
        RoleCodes.Reviewer => 4,
        RoleCodes.DataEntry => 5,
        _ => 10
    };

    private static async Task<long?> FindRootCourtIdAsync(ApplicationDbContext dbContext, long courtId)
    {
        var current = await dbContext.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courtId);
        while (current != null)
        {
            if (current.ParentCourtUnitId == null) return current.Id;
            current = await dbContext.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == current.ParentCourtUnitId);
        }
        return null;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
