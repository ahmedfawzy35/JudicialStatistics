namespace JudicialStatistics.Domain.Rules;

/// <summary>
/// قاعدة الحكم لنوع قضية ضمن نسخة قواعد
/// UNIQUE(RuleSetVersionId, CaseTypeId, JudgmentTypeId)
/// لا يكفي تشابه اسم الحكم — يجب أن تكون القاعدة معرّفة صراحةً
/// </summary>
public class CaseTypeJudgmentRule : Common.BaseEntity
{
    public long RuleSetVersionId { get; set; }
    public long CaseTypeId       { get; set; }
    public long JudgmentTypeId   { get; set; }
    public long TreeNodeId        { get; set; }

    /// <summary>هل يُحتسب في إجمالي الأحكام؟</summary>
    public bool CountsInJudgmentTotal { get; set; } = true;

    /// <summary>هل يتطلب تخصيص على القضاة؟</summary>
    public bool RequiresAllocation    { get; set; } = false;

    public long? DeferralReasonId     { get; set; }

    // Navigation
    public RuleSetVersion  RuleSetVersion  { get; set; } = null!;
    public Cases.CaseType  CaseType        { get; set; } = null!;
    public JudgmentType    JudgmentType    { get; set; } = null!;
    public JudgmentTreeNode TreeNode       { get; set; } = null!;
    public DeferralReason?  DeferralReason { get; set; }
}

/// <summary>
/// قاعدة الإدخال — تُحدد البنود المطلوبة أو الاختيارية لنوع قضية
/// يجب أن يكون أحد المراجع الثلاثة فقط غير فارغ:
///   IncomingTypeId | ProductionItemTypeId | JudgeActionTypeId
/// </summary>
public class CaseTypeInputRule : Common.BaseEntity
{
    public long  RuleSetVersionId      { get; set; }
    public long  CaseTypeId            { get; set; }
    public long? IncomingTypeId        { get; set; }
    public long? ProductionItemTypeId  { get; set; }
    public long? JudgeActionTypeId     { get; set; }
    public bool  IsRequired            { get; set; } = true;
    public short SortOrder             { get; set; }

    // Navigation
    public RuleSetVersion      RuleSetVersion     { get; set; } = null!;
    public Cases.CaseType      CaseType           { get; set; } = null!;
    public IncomingType?       IncomingType       { get; set; }
    public ProductionItemType? ProductionItemType { get; set; }
    public JudgeActionType?    JudgeActionType    { get; set; }
}

/// <summary>
/// قاعدة التأجيل — تُحدد إلى أي بند مقدم يعود كل سبب تأجيل
/// UNIQUE(RuleSetVersionId, CaseTypeId, DeferralReasonId)
/// لا يسمح بإرجاع الرصيد إلى بندين في الوقت نفسه
/// </summary>
public class CaseTypeDeferralRule : Common.BaseEntity
{
    public long RuleSetVersionId      { get; set; }
    public long CaseTypeId            { get; set; }
    public long DeferralReasonId      { get; set; }
    public long ReturnIncomingTypeId  { get; set; }

    // Navigation
    public RuleSetVersion RuleSetVersion    { get; set; } = null!;
    public Cases.CaseType CaseType         { get; set; } = null!;
    public DeferralReason DeferralReason   { get; set; } = null!;
    public IncomingType   ReturnIncomingType { get; set; } = null!;
}
