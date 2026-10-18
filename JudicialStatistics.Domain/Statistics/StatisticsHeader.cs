namespace JudicialStatistics.Domain.Statistics;

/// <summary>
/// رأس الإحصائية — لكل جلسة ولكل فترة شهرية
/// UNIQUE(SessionVersionId, StatisticalPeriodId)
/// الإحصائية لا تُحذف — تُلغى أو تُعاد للمسودة
/// </summary>
public class StatisticsHeader : Common.BaseEntity
{
    public long SessionVersionId    { get; set; }
    public long StatisticalPeriodId { get; set; }
    public long RuleSetVersionId    { get; set; }
    public long CaseYearPolicyId    { get; set; }

    /// <summary>Draft | Submitted | UnderReview | Approved | Reopened</summary>
    public string Status { get; set; } = "Draft";

    public long?     CreatedByUserId    { get; set; }
    public DateTime  CreatedAt          { get; set; } = DateTime.UtcNow;
    public long?     SubmittedByUserId  { get; set; }
    public DateTime? SubmittedAt        { get; set; }
    public long?     ReviewedByUserId   { get; set; }
    public DateTime? ReviewedAt         { get; set; }
    public long?     ApprovedByUserId   { get; set; }
    public DateTime? ApprovedAt         { get; set; }
    public string?   ReviewComment      { get; set; }
    public string?   RejectionReason    { get; set; }

    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Organization.SessionVersion    SessionVersion    { get; set; } = null!;
    public Years.StatisticalPeriod        StatisticalPeriod { get; set; } = null!;
    public Rules.RuleSetVersion           RuleSetVersion    { get; set; } = null!;
    public Rules.CaseYearPolicy           CaseYearPolicy    { get; set; } = null!;
    public Identity.AppUser?              CreatedBy         { get; set; }
    public Identity.AppUser?              SubmittedBy       { get; set; }
    public Identity.AppUser?              ReviewedBy        { get; set; }
    public Identity.AppUser?              ApprovedBy        { get; set; }
    public ICollection<IncomingRow>     IncomingRows     { get; set; } = [];
    public ICollection<ProductionRow>   ProductionRows   { get; set; } = [];
    public ICollection<JudgeJudgment>   JudgeJudgments   { get; set; } = [];
    public ICollection<Postponement>    Postponements    { get; set; } = [];
    public ICollection<AuditEntry>      AuditEntries     { get; set; } = [];
}

