namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// تكليف قاضٍ في دائرة — يرتبط بنسخة الدائرة وليس بالدائرة مباشرة
/// لا تُعدَّل التكليفات التاريخية — التعديل يكون بتكليف جديد
/// يُستخدم في توزيع أحكام الجنح
/// </summary>
public class CircuitJudgeAssignment : Common.BaseEntity
{
    public long CircuitVersionId { get; set; }
    public long JudgeId          { get; set; }
    public long JudicialRoleId   { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo   { get; set; }

    /// <summary>ترتيب العرض داخل نسخة الدائرة</summary>
    public short DisplayOrder { get; set; }

    // Navigation
    public CircuitVersion CircuitVersion { get; set; } = null!;
    public Judge          Judge          { get; set; } = null!;
    public JudicialRole   JudicialRole   { get; set; } = null!;
}
