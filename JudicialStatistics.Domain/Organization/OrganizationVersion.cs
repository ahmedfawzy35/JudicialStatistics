namespace JudicialStatistics.Domain.Organization;

/// <summary>
/// نسخة التشكيل — تصدر بقرار وتسري من تاريخ معين
/// لا تُعدَّل نسخة التشكيل المستخدمة في إحصائية معتمدة
/// أي تعديل جوهري يُنشئ نسخة جديدة
/// </summary>
public class OrganizationVersion : Common.BaseEntity
{
    public long JudicialYearId { get; set; }
    public int  VersionNo      { get; set; }

    public string   DecisionReference { get; set; } = string.Empty;
    public DateOnly DecisionDate      { get; set; }
    public DateOnly EffectiveFrom     { get; set; }
    public DateOnly EffectiveTo       { get; set; }

    /// <summary>Draft | Approved</summary>
    public string Status { get; set; } = "Draft";

    public long?     ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt       { get; set; }

    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Years.JudicialYear             JudicialYear      { get; set; } = null!;
    public Identity.AppUser?              ApprovedBy        { get; set; }
    public ICollection<CircuitVersion>    CircuitVersions   { get; set; } = [];
}
