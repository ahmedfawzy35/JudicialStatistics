namespace JudicialStatistics.Domain.Years;

/// <summary>
/// حالة فتح/إغلاق الفترة الإحصائية لمستوى محكمة محددة (ابتدائية أو تابعة)
/// يتيح لكل محكمة فتح أو إغلاق الشهر لنفسها بشكل مستقل تحت سلطة مدير النطاق / رئيس قسم الإحصاء
/// </summary>
public class CourtPeriodStatus : Common.BaseEntity
{
    public long CourtUnitId { get; set; }
    public long StatisticalPeriodId { get; set; }
    public bool IsOpenForEntry { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedByUserId { get; set; }

    // Navigation
    public Courts.CourtUnit CourtUnit { get; set; } = null!;
    public StatisticalPeriod StatisticalPeriod { get; set; } = null!;
    public Identity.AppUser? UpdatedByUser { get; set; }
}
