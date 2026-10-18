namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// الصفة القضائية — رئيس الدائرة، عضو يمين، عضو يسار، إلخ
/// قابلة للتعديل من البيانات لا من الكود
/// ملاحظة: التمييز بين "رئيس المحكمة" و"رئيس الدائرة" قرار يحتاج تأكيداً
/// </summary>
public class JudicialRole : Common.BaseEntity
{
    public string Code    { get; set; } = string.Empty;
    public string NameAr  { get; set; } = string.Empty;

    /// <summary>
    /// ترتيب الأولوية في توزيع الأحكام الجنائية — الأصغر يحصل أولاً على الباقي
    /// </summary>
    public short SortOrder { get; set; }

    // Navigation
    public ICollection<CircuitJudgeAssignment> Assignments { get; set; } = [];
}
