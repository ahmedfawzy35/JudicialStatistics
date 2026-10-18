namespace JudicialStatistics.Application.Statistics;

// ======================== DTOs ========================

public record StatisticsHeaderDto(
    long      Id,
    long      SessionVersionId,
    string    SessionNameAr,
    string    CircuitNameAr,
    string    CircuitKind,
    string    DisciplineCode,
    long      StatisticalPeriodId,
    string    PeriodMonthNameAr,
    long      JudicialYearId,
    string    JudicialYearNameAr,
    long      RuleSetVersionId,
    long      CaseYearPolicyId,
    string    Status,
    long?     CreatedByUserId,
    string?   CreatedByNameAr,
    DateTime  CreatedAt,
    long?     SubmittedByUserId,
    string?   SubmittedByNameAr,
    DateTime? SubmittedAt,
    long?     ReviewedByUserId,
    string?   ReviewedByNameAr,
    DateTime? ReviewedAt,
    long?     ApprovedByUserId,
    string?   ApprovedByNameAr,
    DateTime? ApprovedAt,
    string?   ReviewComment,
    string?   RejectionReason,
    byte[]    RowVersion,
    bool      IsOpenForEntry = true
);

public record OpeningBalanceDto(
    long   Id,
    long   CaseTypeId,
    string CaseTypeNameAr,
    long   CaseYearBandId,
    string CaseYearBandLabelAr,
    int    Count
);

public record IncomingRowDto(
    long    Id,
    long    CaseTypeId,
    string  CaseTypeNameAr,
    long    CaseYearBandId,
    string  CaseYearBandLabelAr,
    long    IncomingTypeId,
    string  IncomingTypeCode,
    string  IncomingTypeNameAr,
    /// <summary>الشهر المقصَّر منه — يُملأ فقط للتقصير (SHORT)</summary>
    long?   SourcePeriodId,
    string? SourcePeriodNameAr,
    int     Count
);

public record ProductionRowDto(
    long   Id,
    long   CaseTypeId,
    string CaseTypeNameAr,
    long   CaseYearBandId,
    string CaseYearBandLabelAr,
    long   ProductionItemTypeId,
    string ProductionItemTypeCode,
    string ProductionItemTypeNameAr,
    bool   CountsInJudgmentTotal,
    bool   RequiresAllocation,
    int    Count,
    List<JudgmentAllocationDto> Allocations
);

public record JudgmentAllocationDto(
    long   Id,
    long   ProductionRowId,
    long   JudgeId,
    string JudgeNameAr,
    int    Count
);

/// <summary>
/// حكم قاضٍ تفصيلي — مدني/أسرة/عمال: كل أنواع الأحكام
/// جنائي: الفئات الإجمالية فقط (CRIM_CAT_*)
/// </summary>
public record JudgeJudgmentDto(
    long   Id,
    long   JudgeId,
    string JudgeNameAr,
    long   CaseYearBandId,
    string CaseYearBandLabelAr,
    string JudgmentKind,
    int    Count
);

/// <summary>
/// مؤجل على شهر محدد — موحَّد لجميع الأنواع
/// </summary>
public record PostponementDto(
    long   Id,
    long   CaseYearBandId,
    string CaseYearBandLabelAr,
    string PostponementKind,
    long   TargetPeriodId,
    string TargetPeriodNameAr,
    int    Count
);

public record ClosingBalanceDto(
    long   Id,
    long   CaseTypeId,
    string CaseTypeNameAr,
    long   CaseYearBandId,
    string CaseYearBandLabelAr,
    int    Count,
    bool   CarriedForward
);

public record AuditEntryDto(
    long     Id,
    long?    UserId,
    string?  UserNameAr,
    string   Action,
    string?  Comment,
    DateTime OccurredAt
);

/// <summary>
/// نموذج البيانات الكامل لجلسة الإحصائية
/// </summary>
public record StatisticsMatrixDto(
    StatisticsHeaderDto       Header,
    List<OpeningBalanceDto>   OpeningBalances,
    List<IncomingRowDto>      IncomingRows,
    List<ProductionRowDto>    ProductionRows,
    List<JudgeJudgmentDto>    JudgeJudgments,
    List<PostponementDto>     Postponements,
    List<ClosingBalanceDto>   ClosingBalances,
    List<AuditEntryDto>       AuditEntries,
    bool                      CanEdit,
    bool                      CanSubmit,
    bool                      CanReview,
    bool                      CanApprove,
    bool                      CanReopen
);

// ======================== Commands ========================

public record OpeningBalanceValue(
    long CaseTypeId,
    long CaseYearBandId,
    int  Count
);

public record IncomingRowValue(
    long  CaseTypeId,
    long  CaseYearBandId,
    long  IncomingTypeId,
    /// <summary>الشهر المقصَّر منه — يُملأ فقط للتقصير (SHORT)</summary>
    long? SourcePeriodId,
    int   Count
);

public record AllocationValue(
    long JudgeId,
    int  Count
);

public record ProductionRowValue(
    long CaseTypeId,
    long CaseYearBandId,
    long ProductionItemTypeId,
    int  Count,
    List<AllocationValue>? Allocations
);

/// <summary>
/// حكم قاضٍ للحفظ — مدني: تفاصيل كاملة | جنائي: فئات إجمالية
/// </summary>
public record JudgeJudgmentValue(
    long   JudgeId,
    long   CaseYearBandId,
    string JudgmentKind,
    int    Count
);

/// <summary>
/// مؤجل للحفظ — مع تحديد الشهر المرحَّل إليه
/// </summary>
public record PostponementValue(
    long   CaseYearBandId,
    string PostponementKind,
    long   TargetPeriodId,
    int    Count
);

public record SaveStatisticsDataCommand(
    long                       HeaderId,
    byte[]                     RowVersion,
    List<OpeningBalanceValue>? OpeningBalances,
    List<IncomingRowValue>     IncomingRows,
    List<ProductionRowValue>   ProductionRows,
    List<JudgeJudgmentValue>   JudgeJudgments,
    List<PostponementValue>    Postponements
);

// ======================== Interface ========================

public interface IStatisticsService
{
    Task<List<StatisticsHeaderDto>> GetHeadersAsync(
        long? judicialYearId = null,
        long? statisticalPeriodId = null,
        long? sessionVersionId = null,
        string? status = null,
        long? currentUserId = null,
        long? primaryCourtId = null);

    Task<StatisticsMatrixDto> GetOrCreateMatrixAsync(
        long sessionVersionId,
        long statisticalPeriodId,
        long currentUserId);

    Task<StatisticsMatrixDto> GetMatrixByIdAsync(long headerId, long currentUserId);

    Task SaveDraftAsync(SaveStatisticsDataCommand cmd, long currentUserId);
    Task SubmitAsync(long headerId, byte[] rowVersion, long currentUserId);
    Task ReviewAsync(long headerId, byte[] rowVersion, bool approved, string? comment, long currentUserId);
    Task ApproveAsync(long headerId, byte[] rowVersion, long currentUserId);
    Task ReopenAsync(long headerId, byte[] rowVersion, string reason, long currentUserId);
}
