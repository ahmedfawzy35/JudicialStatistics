namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// حركات القضاة — الانتدابات، التنقلات، والإجازات
/// سجل تاريخي صارم غير قابل للتعديل أو الحذف
/// </summary>
public class JudgeMovement : Common.BaseEntity
{
    public long JudgeId { get; set; }
    public long? CircuitId { get; set; }

    /// <summary>
    /// الدائرة المنقول / المنتدب منها
    /// </summary>
    public long? FromCircuitId { get; set; }

    /// <summary>
    /// الدائرة المنقول / المنتدب إليها
    /// </summary>
    public long? ToCircuitId { get; set; }

    /// <summary>
    /// عمل القاضي بالجلسة / الصفة القضائية بالدائرة الجديدة (رئيس محكمة/دائرة، عضو يمين، عضو يسار، إلخ)
    /// </summary>
    public long? JudicialRoleId { get; set; }

    /// <summary>
    /// تشكيل وترتيب قضاة الدائرة المصدر المعتمد مع القرار بتنسيق JSON
    /// </summary>
    public string? FromCircuitOrderJson { get; set; }

    /// <summary>
    /// تشكيل وترتيب قضاة الدائرة الهدف المعتمد مع القرار بتنسيق JSON
    /// </summary>
    public string? ToCircuitOrderJson { get; set; }

    /// <summary>
    /// نوع الحركة: "نقل" | "ندب" | "إجازة اعتيادية" | "إجازة مرضية" | "إجازة خاصة" | "إنهاء ندب"
    /// </summary>
    public string MovementType { get; set; } = string.Empty;

    /// <summary>
    /// رقم القرار كقيمة نصية (مثال: "قرار وزير العدل رقم 123 لسنة 2025" أو "قرار رئيس المحكمة 45")
    /// </summary>
    public string DecisionNumber { get; set; } = string.Empty;

    public DateOnly DecisionDate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// بند وسبب الإجازة أو النقل
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Judge Judge { get; set; } = null!;
    public Circuit? Circuit { get; set; }
    public Circuit? FromCircuit { get; set; }
    public Circuit? ToCircuit { get; set; }
    public JudicialRole? JudicialRole { get; set; }
}
