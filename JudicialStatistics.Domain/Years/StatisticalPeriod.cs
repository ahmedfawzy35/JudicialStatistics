namespace JudicialStatistics.Domain.Years;

/// <summary>
/// الفترة الإحصائية الشهرية — 12 فترة لكل عام قضائي
/// الترتيب: أكتوبر، نوفمبر، ديسمبر، يناير، فبراير، مارس، أبريل، مايو، يونيو، يوليو، أغسطس، سبتمبر
/// </summary>
public class StatisticalPeriod : Common.BaseEntity
{
    public long JudicialYearId { get; set; }

    /// <summary>أول يوم في الشهر — يحدد الشهر والعام</summary>
    public DateOnly MonthStartDate { get; set; }

    /// <summary>هل الفترة مفتوحة لإدخال وتعديل الإحصائيات</summary>
    public bool IsOpenForEntry { get; set; } = true;

    // Navigation
    public JudicialYear JudicialYear { get; set; } = null!;
}
