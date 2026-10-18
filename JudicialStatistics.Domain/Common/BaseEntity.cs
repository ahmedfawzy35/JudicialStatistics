namespace JudicialStatistics.Domain.Common;

/// <summary>
/// الكيان الأساسي لجميع جداول التطبيق بمفتاح رئيسي bigint
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }
}
