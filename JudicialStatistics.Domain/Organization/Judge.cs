namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// القاضي — يُعرَّف بكود فريد، لا يعتمد على الاسم
/// </summary>
public class Judge : Common.BaseEntity
{
    public string Code      { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public bool   IsActive   { get; set; } = true;

    /// <summary>المحكمة التي قامت بتسجيل القاضي في النظام — لا يتاح التعديل إلا لها</summary>
    public long?  CreatedCourtUnitId { get; set; }

    // Navigation
    public Courts.CourtUnit? CreatedCourtUnit { get; set; }
    public ICollection<CircuitJudgeAssignment> Assignments { get; set; } = [];
}

