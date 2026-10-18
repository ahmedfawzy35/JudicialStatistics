namespace JudicialStatistics.Domain.Statistics;

/// <summary>
/// الرصيد الختامي المحسوب — يُنشأ تلقائياً عند الاعتماد، لا يُدخَل يدوياً
/// ClosingCount = OpeningBalance.Count + IncomingRows.Sum - ProductionRows.Sum (الأحكام فقط)
/// يُستخدم كرصيد افتتاحي للفترة التالية
/// </summary>
public class ClosingBalance : Common.BaseEntity
{
    public long StatisticsHeaderId { get; set; }
    public long CaseTypeId         { get; set; }
    public long CaseYearBandId     { get; set; }

    public int  Count           { get; set; }
    public bool CarriedForward  { get; set; } = false;

    // Navigation
    public StatisticsHeader   StatisticsHeader { get; set; } = null!;
    public Cases.CaseType     CaseType         { get; set; } = null!;
    public Rules.CaseYearBand CaseYearBand     { get; set; } = null!;
}
