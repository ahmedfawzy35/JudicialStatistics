using System.Collections.Generic;

namespace JudicialStatistics.Domain.Import;

/// <summary>
/// قالب استيراد الإحصائيات من ملف الإكسل
/// يحدد إحداثيات ورقة التشكيل، وتركيبة بلوك الجلسة في ورقة الإحصائية
/// </summary>
public class ExcelImportTemplate : Common.BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string DisciplineKind { get; set; } = string.Empty; // CivilFull, Family, Labor, CriminalAppeal, PartialMisdemeanor, PartialCivil
    public string StatisticsSheetName { get; set; } = string.Empty; // "مدني كلي", "اسرة", etc.
    public string TashkeelSheetName { get; set; } = "التشكيل";
    public bool IsDefault { get; set; } = true;

    // إحداثيات ورقة التشكيل
    public int TashkeelCircuitStartRow { get; set; } = 5;
    public string TashkeelCircuitNoCol { get; set; } = "A";
    public string TashkeelCircuitNameCol { get; set; } = "B";

    public int TashkeelSessionStartRow { get; set; } = 5;
    public string TashkeelSessionCircuitNoCol { get; set; } = "E";
    public string TashkeelSessionNoCol { get; set; } = "F";
    public string TashkeelSessionNameCol { get; set; } = "G";
    public string TashkeelSessionTypeCol { get; set; } = "H";
    public string TashkeelSessionDayCol { get; set; } = "I";
    public string TashkeelSessionStartBlockCol { get; set; } = "J"; // بداية الرنج
    public string TashkeelSessionCourtCol { get; set; } = "K";

    public int TashkeelJudgeStartRow { get; set; } = 5;
    public string TashkeelJudgeNoCol { get; set; } = "L";
    public string TashkeelJudgeNameCol { get; set; } = "M";
    public string TashkeelJudgeCircuitNoCol { get; set; } = "P";
    public string TashkeelJudgeRoleCol { get; set; } = "R";
    public string TashkeelJudgeStatusCol { get; set; } = "S";

    // تركيبة البلوك في ورقة الإحصائية
    public int BlockTotalRows { get; set; } = 15;
    public int YearBandsCount { get; set; } = 5;
    public int PrimaryDataRowOffset { get; set; } = 1; // بداية المقدم والإنتاج والقضاة (صف البلوك + 1)
    public int SecondaryJudgesRowOffset { get; set; } = 8; // بداية القضاة الثانويين (صف البلوك + 8)
    public int Judge1HeaderRowOffset { get; set; } = 0; // صف ترويسة القاضي 1 و 2 و 5
    public int Judge3HeaderRowOffset { get; set; } = 7; // صف ترويسة القاضي 3 و 4 و 6
    public string Judge1HeaderCol { get; set; } = "BD"; // خلية رقم القاضي الأول
    public string Judge2HeaderCol { get; set; } = "CF"; // خلية رقم القاضي الثاني
    public string Judge5HeaderCol { get; set; } = "DH"; // خلية رقم القاضي الخامس

    // خرائط الأعمدة
    public ICollection<ExcelImportColumnMapping> ColumnMappings { get; set; } = [];
}
