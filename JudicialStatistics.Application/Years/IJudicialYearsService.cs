namespace JudicialStatistics.Application.Years;

// ======================== DTOs ========================

public record JudicialYearDto(
    long     Id,
    long     ReportingScopeId,
    string   ReportingScopeNameAr,
    string   NameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    string   Status,
    int      PeriodsCount
);

public record StatisticalPeriodDto(
    long     Id,
    long     JudicialYearId,
    DateOnly MonthStartDate,
    string   MonthNameAr,
    string   YearNameAr,
    bool     IsOpenForEntry = true
);

public record CourtPeriodStatusDto(
    long      CourtUnitId,
    string    CourtNameAr,
    string    CourtKind,
    long?     ParentCourtUnitId,
    long      StatisticalPeriodId,
    bool      IsOpenForEntry,
    DateTime? UpdatedAt,
    string?   UpdatedByUserName
);

public record JudicialYearCreateCommand(
    long   ReportingScopeId,
    int    StartYear // e.g. 2025 -> 2025/2026 (2025-10-01 to 2026-09-30)
);

public record JudicialYearUpdateStatusCommand(
    long   Id,
    string Status // "Draft", "Active", "Closed"
);

// ======================== Interface ========================

public interface IJudicialYearsService
{
    Task<List<JudicialYearDto>> GetYearsAsync(long? reportingScopeId = null);
    Task<JudicialYearDto?> GetByIdAsync(long id);
    Task<JudicialYearDto?> GetActiveYearAsync(long? reportingScopeId = null);
    Task<List<StatisticalPeriodDto>> GetPeriodsForYearAsync(long judicialYearId, long? courtUnitId = null);
    Task<bool> IsPeriodOpenForCourtAsync(long periodId, long courtUnitId);
    Task<List<CourtPeriodStatusDto>> GetCourtPeriodStatusesAsync(long periodId, long? primaryCourtId = null);
    Task<long> CreateYearAsync(JudicialYearCreateCommand cmd);
    Task UpdateStatusAsync(JudicialYearUpdateStatusCommand cmd);
    Task SetPeriodEntryStatusAsync(long periodId, bool isOpen);
    Task SetCourtPeriodEntryStatusAsync(long periodId, long courtUnitId, bool isOpen, long? currentUserId = null);
    Task SetAllCourtsPeriodEntryStatusAsync(long periodId, long primaryCourtId, bool isOpen, long? currentUserId = null);
}
