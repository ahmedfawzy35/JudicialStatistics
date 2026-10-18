namespace JudicialStatistics.Application.Identity;

// ──────────────── DTOs ────────────────

public record AppUserDto(
    long      Id,
    string    IdentitySubject,
    string    FullNameAr,
    string    Email,
    bool      IsEnabled,
    DateTime  CreatedAt,
    DateTime? LastLoginAt,
    List<string> Roles,
    List<string> CourtGrants,
    List<GrantSummary> Grants
);

public record GrantSummary(
    long   GrantId,
    long   CourtUnitId,
    string CourtUnitNameAr,
    long   RoleId,
    string RoleNameAr,
    string RoleCode,
    bool   IsActive
);

public record UserCourtGrantDto(
    long   Id,
    long   UserId,
    string UserNameAr,
    long   CourtUnitId,
    string CourtUnitNameAr,
    long   RoleId,
    string RoleNameAr,
    string SessionScopeMode,
    bool   IsActive
);

public record UserCourtGrantCreateCommand(
    long   UserId,
    long   CourtUnitId,
    long   RoleId,
    string SessionScopeMode
);

public record RoleDto(
    long   Id,
    string Code,
    string NameAr
);

public record PrimaryCourtDto(
    long   Id,
    string Code,
    string NameAr
);

// ──────────────── Interface ────────────────

public interface IUsersService
{
    // قراءة
    Task<List<AppUserDto>> GetUsersAsync(long? primaryCourtId = null);
    Task<AppUserDto?> GetByIdAsync(long id);
    Task<List<RoleDto>> GetRolesAsync();
    Task<List<PrimaryCourtDto>> GetPrimaryCourtsAsync();
    Task<List<PrimaryCourtDto>> GetAllowedPrimaryCourtsAsync(long appUserId);

    // تعديل
    Task<bool> UpdateUserAsync(long id, string fullNameAr, string email);
    Task ToggleUserActiveAsync(long id);
    Task<bool> DeleteUserAsync(long id);

    // المنح
    Task<List<UserCourtGrantDto>> GetGrantsAsync(long? userId = null);
    Task AssignCourtGrantAsync(UserCourtGrantCreateCommand cmd);
    Task RevokeCourtGrantAsync(long grantId);
}
