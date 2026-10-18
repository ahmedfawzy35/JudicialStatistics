namespace JudicialStatistics.Domain.Reports;

/// <summary>
/// إعدادات أعمدة تقرير أحكام الدوائر على مستوى النظام
/// يتيح تخصيص الأعمدة الظاهرة في التقرير، النمط (مجمع أو تفصيلي)، وعناوين الأعمدة
/// </summary>
public class CircuitReportColumnConfig : Common.BaseEntity
{
    /// <summary>كود نوع الحكم / العمود: FINAL, INTERLOC, EVID_EXPERT, EVID_OTHER, EVID_TOTAL, STAY, OTHER, TOTAL_JUDGMENTS...</summary>
    public string ColumnKey { get; set; } = string.Empty;

    /// <summary>الاسم الافتراضي للعمود</summary>
    public string DefaultTitleAr { get; set; } = string.Empty;

    /// <summary>الاسم المخصص القابل للتعديل من المستخدم</summary>
    public string CustomTitleAr { get; set; } = string.Empty;

    /// <summary>فئة الحكم: Final | Interlocutory | Evidence | Stay | Other | Total</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>هل العمود ظاهر في التقرير</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>ترتيب عرض العمود</summary>
    public short DisplayOrder { get; set; } = 10;

    /// <summary>نمط العرض: Consolidated (مجمع) | Detailed (تفصيلي) | TotalOnly (الإجمالي فقط)</summary>
    public string DisplayMode { get; set; } = "Consolidated";

    /// <summary>هل العمود إلزامي في التقرير</summary>
    public bool IsSystemRequired { get; set; } = false;

    /// <summary>وصف توضيحي للعمود والأحكام الداخلة تحته</summary>
    public string? Description { get; set; }
}
