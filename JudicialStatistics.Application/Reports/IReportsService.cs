namespace JudicialStatistics.Application.Reports;

// ======================== DTOs ========================

public record CaseTypeSummaryDto(
    long   CaseTypeId,
    string CaseTypeNameAr,
    int    OpeningCount,
    int    IncomingCount,
    int    TotalAvailable,
    int    JudgmentCount,
    int    DeferredCount,
    int    ClosingCount,
    double CompletionRatePercent
);

public record PeriodReportSummaryDto(
    string CourtNameAr,
    string JudicialYearNameAr,
    string PeriodNameAr,
    int    TotalOpening,
    int    TotalIncoming,
    int    TotalAvailable,
    int    TotalJudgments,
    int    TotalDeferred,
    int    TotalClosing,
    double OverallCompletionRate,
    List<CaseTypeSummaryDto> CaseTypeSummaries
);

public record JudgePerformanceDto(
    long   JudgeId,
    string JudgeCode,
    string JudgeFullNameAr,
    string JudicialRoleNameAr,
    int    TotalJudgments,
    int    CriminalAllocationsCount,
    int    CircuitsCount
);

public record AnnualReportSummaryDto(
    string JudicialYearNameAr,
    string ReportingScopeNameAr,
    int    TotalSessionsHeld,
    int    TotalJudgmentsIssued,
    double AnnualCompletionRate,
    List<CaseTypeSummaryDto> CaseTypeAnnualTotals,
    List<JudgePerformanceDto> JudgesPerformance
);

public record CircuitRankingFilter(
    long JudicialYearId,
    List<long> StatisticalPeriodIds,
    string ReportType, // CivilFull | PartialCivil | Family | Labor | LaborMisdemeanor | PartialMisdemeanor | AppealMisdemeanor
    long? CourtUnitId = null,
    bool DeductSuspendedInMisdemeanors = true
);

public record CircuitJudgeJudgmentsDto(
    long JudgeId,
    string JudgeNameAr,
    string JudicialRoleNameAr,
    short DisplayOrder,
    int FinalJudgmentsCount
);

public record CircuitRankingRowDto(
    int Rank,
    long CircuitId,
    string CircuitCode,
    string CircuitNameAr,
    string CourtNameAr,
    string PresidentJudgeNameAr,
    string PresidentGradeAr,
    int TotalIncoming,          // جملة المقدم
    int SuspendedCount,         // الموقوف (للجنح الجزئي)
    int EffectiveIncoming,      // المقدم الصافي بعد خصم الموقوف إن طُبق
    int FinalJudgmentsCount,    // جملة الأحكام القطعية
    int EvidenceJudgmentsCount, // جملة أحكام الإثبات والتمهيدي
    int OtherJudgmentsCount,    // أحكام أخرى
    int TotalAllJudgmentsCount, // جملة كافة الأحكام
    int StrikeoutCount,         // الشطب
    int ReportPostponedCount,   // جملة المؤجل للتقرير
    int OtherPostponedCount,    // المؤجل لأسباب أخرى
    int TotalPostponedCount,    // إجمالي المؤجلات
    double CompletionRate,      // نسبة الإنجاز على القطعي %
    double TotalJudgmentsRate,  // نسبة الإنجاز على جملة الأحكام %
    List<CircuitJudgeJudgmentsDto> JudgesAllocations, // أحكام كل قاضٍ (القطعية فقط)
    int TotalJudgesAllocations  // مجموع أحكام القضاة القطعية
);

public record CircuitRankingReportDto(
    string ReportTitleAr,
    string JudicialYearNameAr,
    string PeriodRangeNameAr,
    string CourtNameAr,
    string ReportType,
    bool DeductSuspendedInMisdemeanors,
    List<string> JudgeColumnsHeaders, // ["أ (رئيس الدائرة)", "ب (عضو يمين)", "ج (عضو يسار)", "د (عضو رابع)", "هـ (عضو خامس)"]
    List<CircuitRankingRowDto> Rows,
    CircuitRankingRowDto Totals
);

// ======================== تقرير نسبة إدخال الإحصائية (الشاشة الكاملة) ========================

public enum SessionEntryState
{
    NotEntered,         // لم يتم الإدخال
    EnteredUnbalanced,  // تم الإدخال وبها ملاحظات عدم اتزان
    EnteredBalanced     // تم الإدخال وصحيحة ومتزنة
}

public record SessionEntryStatusDto(
    long SessionId,
    long SessionVersionId,
    string SessionCode,
    string SessionNameAr,
    string? ScheduleDescription,
    long CircuitId,
    string CircuitCode,
    string CircuitNameAr,
    string CircuitKind,
    string DisciplineKey,
    string DisciplineNameAr,
    long CourtUnitId,
    string CourtUnitNameAr,
    List<string> JudgesNames,
    long? HeaderId,
    string? HeaderStatus,
    bool IsEntered,
    bool IsBalanced,
    SessionEntryState State,
    int TotalIncoming,
    int TotalJudgments,
    int TotalPostponed,
    DateTime? CreatedAt,
    string? BalanceNotes
);

public record CourtEntryProgressDto(
    long CourtUnitId,
    string CourtUnitNameAr,
    int TotalSessionsCount,
    int EnteredSessionsCount,
    int BalancedSessionsCount,
    int UnbalancedSessionsCount,
    int NotEnteredSessionsCount,
    double EntryRatePercent,
    double BalancedRatePercent
);

public record DisciplineEntryGroupDto(
    string DisciplineKey,
    string DisciplineNameAr,
    string Icon,
    int TotalSessionsCount,
    int EnteredSessionsCount,
    int BalancedSessionsCount,
    double EntryRatePercent,
    List<SessionEntryStatusDto> Sessions
);

public record StatisticsEntryProgressReportDto(
    long JudicialYearId,
    string JudicialYearNameAr,
    long StatisticalPeriodId,
    string PeriodMonthNameAr,
    DateOnly MonthStartDate,
    bool IsPeriodOpenForEntry,
    int TotalSessionsCount,
    int TotalEnteredCount,
    int TotalBalancedCount,
    int TotalUnbalancedCount,
    int TotalNotEnteredCount,
    double OverallEntryRatePercent,
    double OverallBalancedRatePercent,
    List<CourtEntryProgressDto> CourtsProgress,
    List<DisciplineEntryGroupDto> DisciplineGroups,
    List<SessionEntryStatusDto> AllSessions
);

// ======================== تقرير الجرد الشهري (الرصيد المتاح من المؤجلات على الشهور) ========================

public record MonthlyInventoryFilter(
    long JudicialYearId,
    long BaseStatisticalPeriodId,
    long? CourtUnitId = null,
    long? CircuitId = null,
    string? DisciplineCode = null
);

public record MonthlyInventoryBandRowDto(
    long CaseYearBandId,
    string BandLabelAr,
    int SubJudgmentsCount,      // أحكام فرعية وإثبات
    int ReservedCount,          // محجوز للحكم
    int ReportGrossCount,       // مؤجل للتقرير (إجمالي)
    int ShortenedCount,         // المقصر من الشهور التالية (خصم)
    int ReportNetCount,         // مؤجل للتقرير (الصافي = إجمالي - المقصر)
    int OtherPostponedCount,    // مؤجل لأسباب أخرى (يشمل أسباب أخرى + إعادة للمرافعة + مد أجل)
    int TotalAvailableBalance   // جملة الرصيد المتاح = فرعي وإثبات + محجوز للحكم + صافي التقرير + مؤجل لأسباب أخرى
);

public record MonthlyInventoryMonthSectionDto(
    long TargetPeriodId,
    string TargetMonthNameAr,
    DateOnly TargetMonthStartDate,
    List<MonthlyInventoryBandRowDto> BandRows,
    MonthlyInventoryBandRowDto MonthTotals
);

public record MonthlyInventoryReportDto(
    string CourtNameAr,
    string JudicialYearNameAr,
    string BasePeriodNameAr,
    string ScopeDescriptionAr,
    List<MonthlyInventoryMonthSectionDto> TargetMonths,
    List<MonthlyInventoryBandRowDto> CumulativeBandTotals,
    MonthlyInventoryBandRowDto GrandTotal
);

// ======================== تقرير أحكام قاضٍ محدد منسوبة للجلسات والدوائر خلال فترة ========================

public record JudgeSessionJudgmentsFilter(
    long JudgeId,
    long JudicialYearId,
    List<long> StatisticalPeriodIds,
    long? CourtUnitId = null
);

public record JudgeSessionRowDto(
    long SessionId,
    long CircuitId,
    string CircuitCode,
    string CircuitNameAr,
    string SessionCode,
    string SessionNameAr,
    string ScheduleDescription,
    string CourtNameAr,
    string JudicialRoleNameAr,
    string JudgeFullNameAr,
    string CaseTypesDescriptionAr,
    int FinalAcceptCount,                  // قبول
    int FinalRejectCount,                  // رفض
    int FinalFormalRejectCount,            // عدم قبول وشكل وعدم اختصاص
    int OtherFinalCount,                   // أحكام قطعية أخرى (براءة، حبس، غرامة، صلح، إلخ)
    int TotalFinalJudgmentsCount,          // جملة القطعي
    int EvidenceJudgmentsCount,            // الإثبات والتمهيدي (ندب خبير، إعادة، تحقيق، استجواب، حكمين...)
    int StayJudgmentsCount,                // الوقف والانقطاع
    int OtherJudgmentsCount,               // أخرى
    int GrandTotalJudgmentsCount           // جملة الأحكام
);

public record JudgeSessionJudgmentsReportDto(
    long JudgeId,
    string JudgeCode,
    string JudgeFullNameAr,
    string JudicialRoleNameAr,
    string JudicialYearNameAr,
    string PeriodRangeNameAr,
    string CourtNameAr,
    List<JudgeSessionRowDto> SessionRows,
    JudgeSessionRowDto GrandTotals
);

// ======================== Interface ========================

public interface IReportsService
{
    Task<PeriodReportSummaryDto> GetPeriodReportAsync(
        long? courtUnitId,
        long? statisticalPeriodId,
        long? circuitId = null);

    Task<AnnualReportSummaryDto> GetAnnualReportAsync(
        long judicialYearId,
        long? courtUnitId = null);

    Task<List<JudgePerformanceDto>> GetJudgesPerformanceAsync(
        long judicialYearId,
        long? statisticalPeriodId = null);

    Task<CircuitRankingReportDto> GetCircuitRankingReportAsync(CircuitRankingFilter filter);

    Task<byte[]> ExportCircuitRankingToExcelAsync(CircuitRankingFilter filter);

    Task<StatisticsEntryProgressReportDto> GetStatisticsEntryProgressReportAsync(
        long judicialYearId,
        long statisticalPeriodId,
        long? courtUnitId = null);

    Task<MonthlyInventoryReportDto> GetMonthlyInventoryReportAsync(MonthlyInventoryFilter filter);

    Task<byte[]> ExportMonthlyInventoryToExcelAsync(MonthlyInventoryFilter filter);

    Task<JudgeSessionJudgmentsReportDto> GetJudgeSessionJudgmentsReportAsync(JudgeSessionJudgmentsFilter filter);

    Task<byte[]> ExportJudgeSessionJudgmentsToExcelAsync(JudgeSessionJudgmentsFilter filter);

    // ======================== تقرير أحكام الدوائر والجلسات ========================
    Task<CircuitJudgmentsReportDto> GetCircuitJudgmentsReportAsync(CircuitJudgmentsFilter filter);

    Task<byte[]> ExportCircuitJudgmentsToExcelAsync(CircuitJudgmentsFilter filter);

    Task<List<CircuitReportColumnConfigDto>> GetCircuitReportColumnConfigsAsync();

    Task SaveCircuitReportColumnConfigsAsync(List<CircuitReportColumnConfigDto> configs);

    Task ResetCircuitReportColumnConfigsToDefaultAsync();
}
