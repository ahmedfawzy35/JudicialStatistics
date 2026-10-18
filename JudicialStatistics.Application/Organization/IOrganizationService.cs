namespace JudicialStatistics.Application.Organization;

// ======================== DTOs ========================

public record OrganizationVersionDto(
    long     Id,
    long     JudicialYearId,
    string   JudicialYearNameAr,
    int      VersionNo,
    string   DecisionReference,
    DateOnly DecisionDate,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    string   Status,
    long?    ApprovedByUserId,
    DateTime? ApprovedAt,
    int      CircuitVersionsCount
);

public record CircuitDto(
    long   Id,
    long   JudicialYearId,
    long   CourtUnitId,
    string CourtUnitNameAr,
    string Code,
    string NameAr,
    bool   IsClosed,
    int    SessionsCount,
    string CircuitKind = ""
);

public record CircuitDetailDto(
    long   Id,
    long   JudicialYearId,
    long   CourtUnitId,
    string CourtUnitNameAr,
    string Code,
    string NameAr,
    bool   IsClosed,
    List<CircuitVersionDto> Versions,
    List<SessionDto> Sessions
);

public record CircuitVersionDto(
    long     Id,
    long     CircuitId,
    long     OrganizationVersionId,
    string   OrganizationVersionDecision,
    string   CircuitKind,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    short    MinJudges,
    short    MaxJudges,
    List<CircuitJudgeAssignmentDto> JudgeAssignments
);

public record SessionDto(
    long   Id,
    long   CircuitId,
    string CircuitNameAr,
    string Code,
    string NameAr,
    bool   IsClosed,
    List<string>? JurisdictionNamesAr = null,
    string? ScheduleDescription = null,
    long   SessionVersionId = 0,
    long?  EmployeeId = null,
    string? EmployeeNameAr = null,
    string? EmployeeJobTitle = null,
    string? Description = null,
    bool   ShowInProgressReport = true,
    List<string>? CaseTypeNamesAr = null
);

public record SessionDetailDto(
    long   Id,
    long   CircuitId,
    string CircuitNameAr,
    string Code,
    string NameAr,
    bool   IsClosed,
    List<SessionVersionDto> Versions,
    long?  EmployeeId = null,
    string? EmployeeNameAr = null,
    string? EmployeeJobTitle = null,
    string? Description = null,
    bool   ShowInProgressReport = true
);

public record SessionVersionDto(
    long     Id,
    long     SessionId,
    long     CircuitVersionId,
    string   CircuitNameAr,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    string?  ScheduleDescription,
    List<string> JurisdictionNamesAr,
    List<long>? GeographicAreaIds = null,
    List<long>? CaseTypeIds = null,
    long?    EmployeeId = null,
    string?  EmployeeNameAr = null,
    string?  EmployeeJobTitle = null
);

public record JudgeDto(
    long    Id,
    string  Code,
    string  FullNameAr,
    bool    IsActive,
    long?   CreatedCourtUnitId = null,
    string? CreatedCourtUnitNameAr = null
);


public record JudicialRoleDto(
    long   Id,
    string Code,
    string NameAr,
    short  SortOrder
);

public record CircuitJudgeAssignmentDto(
    long     Id,
    long     CircuitVersionId,
    long     JudgeId,
    string   JudgeNameAr,
    long     JudicialRoleId,
    string   JudicialRoleNameAr,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    short    DisplayOrder,
    long     CircuitId = 0,
    string   CircuitNameAr = "",
    string   StatusThisMonth = "على رأس العمل",
    string?  Notes = null
);

public record CircuitFormationFullViewDto(
    long CircuitId,
    string CircuitCode,
    string CircuitNameAr,
    string CourtUnitNameAr,
    string CircuitKind,
    short MinJudges,
    short MaxJudges,
    bool IsClosed,
    List<SessionVersionDto> Sessions,
    List<CircuitJudgeAssignmentDto> Judges,
    List<CircuitDecisionImpactDto> Decisions
);

public record CircuitDecisionImpactDto(
    string DecisionType, // ندب، نقل، إجازة مرضية، تعديل تشكيل
    string Reference,
    DateOnly DecisionDate,
    string Description,
    string Status
);

public record GeographicAreaDto(
    long    Id,
    long?   ParentAreaId,
    string  Code,
    string  NameAr,
    string  AreaKind,
    bool    IsActive = true,
    string? ParentAreaNameAr = null
);

public record GeographicAreaCreateCommand(
    long?  ParentAreaId,
    string Code,
    string NameAr,
    string AreaKind
);

public record GeographicAreaUpdateCommand(
    long   Id,
    string NameAr,
    bool   IsActive
);

public record CircuitJudgeOrderItem(
    long   JudgeId,
    string JudgeNameAr,
    long   JudicialRoleId,
    string JudicialRoleNameAr,
    short  DisplayOrder
);

public record JudgeMovementDto(
    long      Id,
    long      JudgeId,
    string    JudgeNameAr,
    long?     CircuitId,
    string?   CircuitNameAr,
    long?     FromCircuitId,
    string?   FromCircuitNameAr,
    long?     ToCircuitId,
    string?   ToCircuitNameAr,
    long?     JudicialRoleId,
    string?   JudicialRoleNameAr,
    string?   FromCircuitOrderJson,
    string?   ToCircuitOrderJson,
    string    MovementType,
    string    DecisionNumber,
    DateOnly  DecisionDate,
    DateOnly  EffectiveFrom,
    DateOnly? EffectiveTo,
    string    Reason,
    string?   Notes,
    DateTime  CreatedAt
);

public record JudgeMovementCreateCommand(
    long      JudgeId,
    long?     CircuitId,
    long?     FromCircuitId,
    long?     ToCircuitId,
    long?     JudicialRoleId,
    string    MovementType,
    string    DecisionNumber,
    DateOnly  DecisionDate,
    DateOnly  EffectiveFrom,
    DateOnly? EffectiveTo,
    string    Reason,
    string?   Notes,
    List<CircuitJudgeOrderItem>? FromCircuitJudges = null,
    List<CircuitJudgeOrderItem>? ToCircuitJudges = null
);

// ======================== Commands ========================

public record OrganizationVersionCreateCommand(
    long     JudicialYearId,
    int      VersionNo,
    string   DecisionReference,
    DateOnly DecisionDate,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo
);

public record CircuitCreateCommand(
    long     JudicialYearId,
    long     CourtUnitId,
    string   Code,
    string   NameAr,
    string   CircuitKind,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    short    MinJudges = 1,
    short    MaxJudges = 1,
    long?    OrganizationVersionId = null
);

public record SessionCreateCommand(
    long     CircuitId,
    string   Code,
    string   NameAr,
    long?    CircuitVersionId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    string?  ScheduleDescription,
    List<long>? GeographicAreaIds,
    List<long>? CaseTypeIds = null,
    long?    EmployeeId = null,
    string?  Description = null,
    bool     ShowInProgressReport = true
);

public record SessionUpdateCommand(
    long   Id,
    string NameAr,
    bool   IsClosed,
    string? ScheduleDescription = null,
    List<long>? GeographicAreaIds = null,
    List<long>? CaseTypeIds = null,
    long?  EmployeeId = null,
    string? Description = null,
    bool   ShowInProgressReport = true
);

public record JudgeCreateCommand(
    string Code,
    string FullNameAr,
    long?  CreatedCourtUnitId = null
);

public record JudgeUpdateCommand(
    long   Id,
    string Code,
    string FullNameAr,
    bool   IsActive,
    long?  RequestingCourtUnitId = null,
    bool   IsSuperAdmin = false
);

public record CircuitJudgeAssignCommand(
    long     CircuitVersionId,
    long     JudgeId,
    long     JudicialRoleId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    short    DisplayOrder
);

// ======================== Statistical Discipline DTOs ========================

public record StatisticalDisciplineDto(
    long   Id,
    string Code,
    string NameAr,
    string Icon,
    string CircuitKinds,
    string EntryFormKind,
    short  SortOrder,
    bool   IsActive
);

public record StatisticalDisciplineCreateCommand(
    string Code,
    string NameAr,
    string Icon,
    string CircuitKinds,
    string EntryFormKind,
    short  SortOrder
);

public record StatisticalDisciplineUpdateCommand(
    long   Id,
    string NameAr,
    string Icon,
    string CircuitKinds,
    string EntryFormKind,
    short  SortOrder,
    bool   IsActive
);

// ======================== Interface ========================

public interface IOrganizationService
{
    // Versions
    Task<List<OrganizationVersionDto>> GetVersionsAsync(long judicialYearId);
    Task<OrganizationVersionDto?> GetVersionByIdAsync(long id);
    Task<long> CreateVersionAsync(OrganizationVersionCreateCommand cmd);
    Task ApproveVersionAsync(long id, long userId);

    // Circuits
    Task<List<CircuitDto>> GetCircuitsAsync(long judicialYearId, long? courtUnitId = null, long? userId = null);
    Task<CircuitDetailDto?> GetCircuitDetailAsync(long circuitId);
    Task<long> CreateCircuitAsync(CircuitCreateCommand cmd);

    // Sessions
    Task<List<SessionDto>> GetSessionsAsync(long? circuitId = null, long? judicialYearId = null, long? userId = null);
    Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId);
    Task<List<SessionVersionDto>> GetSessionVersionsForYearAsync(long judicialYearId);
    Task<long> CreateSessionAsync(SessionCreateCommand cmd);
    Task UpdateSessionAsync(SessionUpdateCommand cmd);
    Task ToggleSessionClosedAsync(long sessionId);
    Task<List<Statistics.OpeningBalanceDto>> GetSessionOpeningBalancesAsync(long sessionVersionId);
    Task SaveSessionOpeningBalancesAsync(long sessionVersionId, List<Statistics.OpeningBalanceValue> balances, long currentUserId);

    // Judges & Roles
    Task<List<JudgeDto>> GetJudgesAsync(bool activeOnly = true, long? judicialYearId = null, long? userId = null);
    Task<long> CreateJudgeAsync(JudgeCreateCommand cmd);
    Task UpdateJudgeAsync(JudgeUpdateCommand cmd);
    Task<List<JudicialRoleDto>> GetJudicialRolesAsync();
    Task AssignJudgeAsync(CircuitJudgeAssignCommand cmd);
    Task<List<CircuitJudgeAssignmentDto>> GetAssignmentsForYearAsync(long judicialYearId, long? userId = null);
    Task<List<CircuitJudgeAssignmentDto>> GetJudgeAssignmentsForSessionVersionAsync(long sessionVersionId);

    // Geography
    Task<List<GeographicAreaDto>> GetGeographicAreasAsync(bool activeOnly = false);
    Task<long> CreateGeographicAreaAsync(GeographicAreaCreateCommand cmd);
    Task UpdateGeographicAreaAsync(GeographicAreaUpdateCommand cmd);
    Task ToggleGeographicAreaActiveAsync(long id);

    // Judge Movements (Transfers, Leaves, Delegations - Immutable)
    Task<List<JudgeMovementDto>> GetJudgeMovementsAsync(long? judgeId = null, long? circuitId = null, long? judicialYearId = null, long? userId = null);
    Task<long> CreateJudgeMovementAsync(JudgeMovementCreateCommand cmd);
    Task<List<CircuitJudgeOrderItem>> GetCircuitCurrentJudgesForMovementAsync(long circuitId);
    Task<List<CircuitJudgeAssignmentDto>> GetActiveJudgesForSessionMonthAsync(long sessionVersionId, int year, int month);

    // Disciplines Management
    Task<List<StatisticalDisciplineDto>> GetDisciplinesAsync(bool activeOnly = false);
    Task<long> CreateDisciplineAsync(StatisticalDisciplineCreateCommand cmd);
    Task UpdateDisciplineAsync(StatisticalDisciplineUpdateCommand cmd);
    Task ToggleDisciplineActiveAsync(long id);

    // Cascading & Scoped Selection for Statistics Entry
    Task<List<JudicialStatistics.Application.Courts.CourtUnitDto>> GetAuthorizedCourtsForDisciplineAsync(long judicialYearId, string disciplineCode, long userId, long? primaryCourtId = null);
    Task<List<CircuitDto>> GetAuthorizedCircuitsForDisciplineAsync(long judicialYearId, long courtUnitId, string disciplineCode, long userId);
    Task<List<SessionVersionDto>> GetAuthorizedSessionVersionsForDisciplineAsync(long judicialYearId, long circuitId, string disciplineCode, long userId);
    // Employees (Court Staff / Session Clerks)
    Task<List<EmployeeDto>> GetEmployeesAsync(string? searchTerm = null, long? courtUnitId = null, bool activeOnly = true);
    Task<EmployeeDto?> GetEmployeeByIdAsync(long id);
    Task<long> CreateEmployeeAsync(EmployeeCreateCommand cmd);
    Task UpdateEmployeeAsync(long id, EmployeeCreateCommand cmd);
    Task DeleteEmployeeAsync(long id);

    Task<CircuitFormationFullViewDto?> GetCircuitFormationFullViewAsync(long circuitId, long? periodId);
}

public static class StatisticalDisciplines
{
    public const string Civil = "Civil";
    public const string Criminal = "Criminal";
    public const string Family = "Family";
    public const string Labor = "Labor";

    private static readonly object _syncLock = new();
    private static readonly Dictionary<string, (string NameAr, string Icon, string[] CircuitKinds, string EntryFormKind, short SortOrder, bool IsActive)> _disciplines = new(StringComparer.OrdinalIgnoreCase)
    {
        [Civil] = ("مدني", "🏛️", ["CivilFull", "Partial", "CivilPartial"], "Civil", 1, true),
        [Criminal] = ("جنائي", "⚖️", ["CriminalAppeal", "Partial", "CriminalPartial"], "Criminal", 2, true),
        [Family] = ("أسرة", "👨‍👩‍👧", ["Family"], "Civil", 3, true),
        [Labor] = ("عمال", "👷", ["Labor"], "Civil", 4, true)
    };

    public static void Sync(IEnumerable<StatisticalDisciplineDto> dtos)
    {
        lock (_syncLock)
        {
            foreach (var d in dtos)
            {
                var kinds = d.CircuitKinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _disciplines[d.Code] = (d.NameAr, d.Icon, kinds.Length > 0 ? kinds : [d.Code], d.EntryFormKind, d.SortOrder, d.IsActive);
            }
        }
    }

    public static IReadOnlyList<(string Code, string NameAr)> All
    {
        get
        {
            lock (_syncLock)
            {
                return _disciplines
                    .Where(kv => kv.Value.IsActive)
                    .OrderBy(kv => kv.Value.SortOrder)
                    .Select(kv => (kv.Key, kv.Value.NameAr))
                    .ToList();
            }
        }
    }

    public static string GetIcon(string disciplineCode)
    {
        lock (_syncLock)
        {
            return _disciplines.TryGetValue(disciplineCode, out var info) ? info.Icon : "⚖️";
        }
    }

    public static string GetEntryFormKind(string disciplineCode)
    {
        lock (_syncLock)
        {
            return _disciplines.TryGetValue(disciplineCode, out var info) ? info.EntryFormKind : "Civil";
        }
    }

    public static string[] GetCircuitKinds(string disciplineCode)
    {
        lock (_syncLock)
        {
            if (_disciplines.TryGetValue(disciplineCode, out var info))
                return info.CircuitKinds;
            return [];
        }
    }

    public static string GetNameAr(string disciplineCode)
    {
        lock (_syncLock)
        {
            if (_disciplines.TryGetValue(disciplineCode, out var info))
                return info.NameAr;
            return disciplineCode;
        }
    }

    public static string FromCircuitKind(string circuitKind)
    {
        lock (_syncLock)
        {
            foreach (var (code, info) in _disciplines)
            {
                if (info.CircuitKinds.Contains(circuitKind, StringComparer.OrdinalIgnoreCase))
                    return code;
            }
        }
        return circuitKind switch
        {
            "CivilFull" or "Civil" or "CivilPartial" => Civil,
            "CriminalAppeal" or "Criminal" or "Partial" or "CriminalPartial" => Criminal,
            "Family" => Family,
            "Labor" => Labor,
            _ => Civil
        };
    }
}
