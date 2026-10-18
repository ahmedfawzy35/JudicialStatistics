namespace JudicialStatistics.Domain.Statistics;

/// <summary>
/// الرصيد الافتتاحي السنوي للجلسة — لكل نوع قضية ولكل شريحة سنة قيد
/// يرتبط حصراً بنسخة الجلسة للعام القضائي (SessionVersion) ويُدخل أول كل عام قضائي
/// </summary>
public class OpeningBalance : Common.BaseEntity
{
    public long SessionVersionId   { get; set; }
    public long CaseTypeId         { get; set; }
    public long CaseYearBandId     { get; set; }

    public int  Count { get; set; }

    // Navigation
    public Organization.SessionVersion SessionVersion { get; set; } = null!;
    public Cases.CaseType              CaseType       { get; set; } = null!;
    public Rules.CaseYearBand          CaseYearBand   { get; set; } = null!;
}

/// <summary>
/// سطر المقدم — ما وردت به الجلسة من قضايا في الفترة
/// كل بند مقدم (IncomingType) يصف مصدر القضية: جديد، سابق، مجدد، معجل...
/// SourcePeriodId: يُملأ فقط عند التقصير (Code='SHORT') لتحديد الشهر المقصَّر منه
/// </summary>
public class IncomingRow : Common.BaseEntity
{
    public long  StatisticsHeaderId { get; set; }
    public long  CaseTypeId         { get; set; }
    public long  CaseYearBandId     { get; set; }
    public long  IncomingTypeId     { get; set; }

    /// <summary>الشهر المقصَّر منه — يُملأ فقط للتقصير الوارد (SHORT)</summary>
    public long? SourcePeriodId     { get; set; }

    public int   Count { get; set; }

    // Navigation
    public StatisticsHeader         StatisticsHeader { get; set; } = null!;
    public Cases.CaseType           CaseType         { get; set; } = null!;
    public Rules.CaseYearBand       CaseYearBand     { get; set; } = null!;
    public Rules.IncomingType       IncomingType     { get; set; } = null!;
    public Years.StatisticalPeriod? SourcePeriod     { get; set; }
}

/// <summary>
/// سطر الإنتاج — للجنائي: تفاصيل أنواع الأحكام بدون قاضٍ (CRIM_IMPRIS, CRIM_FINE...)
/// للمدني/الأسرة/العمال: لا يُستخدم — الأحكام في JudgeJudgments
/// </summary>
public class ProductionRow : Common.BaseEntity
{
    public long StatisticsHeaderId   { get; set; }
    public long CaseTypeId           { get; set; }
    public long CaseYearBandId       { get; set; }
    public long ProductionItemTypeId { get; set; }

    public int  Count { get; set; }

    // Navigation
    public StatisticsHeader         StatisticsHeader   { get; set; } = null!;
    public Cases.CaseType           CaseType           { get; set; } = null!;
    public Rules.CaseYearBand       CaseYearBand       { get; set; } = null!;
    public Rules.ProductionItemType ProductionItemType { get; set; } = null!;
}

/// <summary>
/// أحكام القضاة التفصيلية — جدول موحَّد لجميع الأنواع
/// مدني/أسرة/عمال → تفاصيل كاملة مع القاضي (FORMAL_*, SUBST_*, EVID_*, STAY_*...)
/// جنائي          → فئات إجمالية مع القاضي (CRIM_CAT_*)
/// JudgmentKind   → كود نصي من Seed بدلاً من FK للمرونة
/// </summary>
public class JudgeJudgment : Common.BaseEntity
{
    public long   StatisticsHeaderId { get; set; }
    public long   JudgeId            { get; set; }
    public long   CaseYearBandId     { get; set; }

    /// <summary>
    /// كود نوع الحكم — أمثلة:
    /// مدني: FORMAL_REJECT, SUBST_ACCEPT, EVID_EXPERT, STAY_PENAL, STRIKEOUT
    /// جنائي: CRIM_CAT_SUBST, CRIM_CAT_PROC, CRIM_CAT_ORDER
    /// </summary>
    public string JudgmentKind { get; set; } = string.Empty;

    public int Count { get; set; }

    // Navigation
    public StatisticsHeader   StatisticsHeader { get; set; } = null!;
    public Organization.Judge Judge            { get; set; } = null!;
    public Rules.CaseYearBand CaseYearBand     { get; set; } = null!;
}

/// <summary>
/// المؤجلات على الشهور — جدول موحَّد لجميع الأنواع
/// TargetPeriodId → الشهر المرحَّل إليه (الشهر القادم افتراضاً)
/// PostponementKind → كود نصي: DEF_RESERVED, DEF_REPORT, CRIM_DEF_NOTICE...
/// </summary>
public class Postponement : Common.BaseEntity
{
    public long   StatisticsHeaderId { get; set; }
    public long   CaseYearBandId     { get; set; }

    /// <summary>
    /// كود نوع التأجيل — أمثلة:
    /// مدني:   DEF_RESERVED, DEF_REPORT, DEF_OTHER, DEF_EXTEND, DEF_RETURN_PLEADING
    /// جنائي:  CRIM_DEF_NOTICE, CRIM_DEF_REP, CRIM_DEF_DOC, CRIM_DEF_OTHER
    /// </summary>
    public string PostponementKind { get; set; } = string.Empty;

    /// <summary>الشهر المرحَّل إليه</summary>
    public long TargetPeriodId { get; set; }

    public int Count { get; set; }

    // Navigation
    public StatisticsHeader        StatisticsHeader { get; set; } = null!;
    public Rules.CaseYearBand      CaseYearBand     { get; set; } = null!;
    public Years.StatisticalPeriod TargetPeriod     { get; set; } = null!;
}

/// <summary>
/// توزيع الأحكام على القضاة — محتفظ به للتوافق مع البيانات القديمة
/// التصميم الجديد يستخدم JudgeJudgments بدلاً منه
/// </summary>
public class JudgmentAllocation : Common.BaseEntity
{
    public long ProductionRowId { get; set; }
    public long JudgeId         { get; set; }

    public int  Count { get; set; }

    // Navigation
    public ProductionRow      ProductionRow { get; set; } = null!;
    public Organization.Judge Judge         { get; set; } = null!;
}

/// <summary>
/// سجل مراجعة الإحصائية — Append-Only، لا يُحذف أي سطر
/// </summary>
public class AuditEntry : Common.BaseEntity
{
    public long      StatisticsHeaderId { get; set; }
    public long?     UserId             { get; set; }
    public string    Action             { get; set; } = string.Empty;
    public string?   Comment            { get; set; }
    public DateTime  OccurredAt         { get; set; } = DateTime.UtcNow;

    // Navigation
    public StatisticsHeader  StatisticsHeader { get; set; } = null!;
    public Identity.AppUser? User             { get; set; }
}
