namespace JudicialStatistics.Application.Courts;

// ======================== DTOs ========================

public record CourtUnitDto(
    long    Id,
    long?   ParentCourtUnitId,
    string  Code,
    string  NameAr,
    string  UnitKind,
    bool    IsActive,
    List<CourtUnitDto> Children
);

public record ReportingScopeDto(
    long   Id,
    string Code,
    string NameAr,
    long   RootCourtUnitId,
    string RootCourtNameAr,
    bool   IsActive
);

public record CourtUnitCreateCommand(
    long?  ParentCourtUnitId,
    string Code,
    string NameAr,
    string UnitKind
);

public record CourtUnitUpdateCommand(
    long   Id,
    string Code,
    string NameAr,
    string UnitKind,
    bool   IsActive
);

// ======================== Interface ========================

public interface ICourtsService
{
    Task<List<CourtUnitDto>>      GetTreeAsync();
    Task<List<CourtUnitDto>>      GetFlatAsync(bool activeOnly = true);
    Task<CourtUnitDto?>           GetByIdAsync(long id);
    Task<long>                    CreateCourtUnitAsync(CourtUnitCreateCommand cmd);
    Task                          UpdateCourtUnitAsync(CourtUnitUpdateCommand cmd);
    Task                          ToggleActiveAsync(long id);

    Task<List<ReportingScopeDto>> GetReportingScopesAsync();
}
