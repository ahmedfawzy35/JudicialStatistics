namespace JudicialStatistics.Domain.Import;

/// <summary>
/// خريطة عمود في شيت الإحصائية
/// يربط حرف العمود (مثل L, M, AA, BD) بكود الحقل في النظام
/// </summary>
public class ExcelImportColumnMapping : Common.BaseEntity
{
    public long ExcelImportTemplateId { get; set; }

    /// <summary>
    /// القسم:
    /// Incoming (المقدم) | Production (الإنتاج) | JudgeJudgment (أحكام القضاة) | Postponement (المؤجلات)
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// حرف العمود في الإكسل (مثال: L, M, AA, BD...)
    /// </summary>
    public string ColumnLetter { get; set; } = string.Empty;

    /// <summary>
    /// كود البند المقابل في النظام (مثال: YEAR, PREV, NEW, RENEW, STRIKE, FORMAL_REJECT, DEF_REPORT...)
    /// </summary>
    public string TargetItemCode { get; set; } = string.Empty;

    /// <summary>
    /// اسم البند بالعربية للعرض والتوثيق
    /// </summary>
    public string TargetItemNameAr { get; set; } = string.Empty;

    /// <summary>
    /// موقع القاضي إن كان البند يخص أحكام القضاة:
    /// 1 = القاضي الأول والثالث (Slot 1)
    /// 2 = القاضي الثاني والرابع (Slot 2)
    /// 3 = القاضي الخامس والسادس (Slot 3)
    /// </summary>
    public int? JudgeSlotIndex { get; set; }

    /// <summary>
    /// الشهر المرحل إليه في كشف المؤجلات (1 للشهر التالي، 2 للذي بعده، إلخ)
    /// </summary>
    public int? TargetMonthOffset { get; set; }

    /// <summary>
    /// ترتيب العرض في واجهة التعديل
    /// </summary>
    public int DisplayOrder { get; set; }

    // Navigation
    public ExcelImportTemplate ExcelImportTemplate { get; set; } = null!;
}
