namespace JudicialStatistics.Application.Organization;

public record EmployeeDto(
    long Id,
    string Code,
    string FullNameAr,
    string? JobTitle,
    string? PhoneNumber,
    string? NationalId,
    long? CourtUnitId,
    string? CourtUnitNameAr,
    bool IsActive
);

public record EmployeeCreateCommand(
    string Code,
    string FullNameAr,
    string? JobTitle = null,
    string? PhoneNumber = null,
    string? NationalId = null,
    long? CourtUnitId = null,
    bool IsActive = true
);

