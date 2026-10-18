namespace JudicialStatistics.Application.Reports;

/// <summary>
/// DTO لإعدادات عمود تقرير أحكام الدوائر
/// </summary>
public record CircuitReportColumnConfigDto(
    long Id,
    string ColumnKey,
    string DefaultTitleAr,
    string CustomTitleAr,
    string Category,
    bool IsVisible,
    short DisplayOrder,
    string DisplayMode,
    bool IsSystemRequired,
    string? Description
)
{
    public string EffectiveTitleAr =>
        string.IsNullOrWhiteSpace(CustomTitleAr) ? DefaultTitleAr : CustomTitleAr;
}

/// <summary>
/// فلتر تقرير أحكام الدوائر والجلسات
/// </summary>
public record CircuitJudgmentsFilter(
    long JudicialYearId,
    List<long> StatisticalPeriodIds,
    long? CourtUnitId = null,
    string? StatisticalDisciplineCode = null,
    long? MainCaseTaxonomyNodeId = null,
    long? SubCaseTypeId = null
);

/// <summary>
/// بيانات صف الجلسة الواحدة في تقرير أحكام الدوائر
/// </summary>
public record CircuitJudgmentsSessionRowDto(
    long SessionId,
    string SessionCode,
    string SessionNameAr,
    long CircuitId,
    string CircuitCode,
    string CircuitNameAr,
    string CircuitKind,
    string CircuitKindAr,
    long CourtUnitId,
    string CourtNameAr,
    string CaseTypeNamesAr,
    int FinalCount,
    int InterlocutoryCount,
    int ExpertCount,
    int OtherEvidenceCount,
    int TotalEvidenceCount,
    int StayCount,
    int OtherCount,
    int TotalJudgmentsCount,
    Dictionary<string, int> DetailedCounts
);

/// <summary>
/// مجموعة الدائرة وتضم جلساتها والإجمالي الفرعي للدائرة
/// </summary>
public record CircuitJudgmentsCircuitGroupDto(
    long CircuitId,
    string CircuitCode,
    string CircuitNameAr,
    string CircuitKindAr,
    string CourtNameAr,
    List<CircuitJudgmentsSessionRowDto> SessionRows,
    CircuitJudgmentsSessionRowDto SubTotal
);

/// <summary>
/// مجموعة المحكمة وتضم دوائرها والإجمالي الفرعي للمحكمة
/// </summary>
public record CircuitJudgmentsCourtGroupDto(
    long CourtUnitId,
    string CourtNameAr,
    List<CircuitJudgmentsCircuitGroupDto> CircuitGroups,
    CircuitJudgmentsSessionRowDto SubTotal
);

/// <summary>
/// التقرير المتكامل لأحكام الدوائر مع التجميعات والإجمالي العام والأعمدة النشطة
/// </summary>
public record CircuitJudgmentsReportDto(
    string JudicialYearNameAr,
    string PeriodRangeNameAr,
    string CourtFilterNameAr,
    string DisciplineFilterNameAr,
    string CaseTypeFilterNameAr,
    List<CircuitJudgmentsCourtGroupDto> CourtGroups,
    CircuitJudgmentsSessionRowDto GrandTotal,
    List<CircuitReportColumnConfigDto> ActiveColumns
);
