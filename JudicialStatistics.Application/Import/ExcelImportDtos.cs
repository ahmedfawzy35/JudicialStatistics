using System.Collections.Generic;

namespace JudicialStatistics.Application.Import;

public class ExcelImportTemplateDto
{
    public long Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string DisciplineKind { get; set; } = string.Empty;
    public string StatisticsSheetName { get; set; } = string.Empty;
    public string TashkeelSheetName { get; set; } = "التشكيل";
    public bool IsDefault { get; set; }

    public int TashkeelCircuitStartRow { get; set; } = 5;
    public string TashkeelCircuitNoCol { get; set; } = "A";
    public string TashkeelCircuitNameCol { get; set; } = "B";

    public int TashkeelSessionStartRow { get; set; } = 5;
    public string TashkeelSessionCircuitNoCol { get; set; } = "E";
    public string TashkeelSessionNoCol { get; set; } = "F";
    public string TashkeelSessionNameCol { get; set; } = "G";
    public string TashkeelSessionTypeCol { get; set; } = "H";
    public string TashkeelSessionDayCol { get; set; } = "I";
    public string TashkeelSessionStartBlockCol { get; set; } = "J";
    public string TashkeelSessionCourtCol { get; set; } = "K";

    public int TashkeelJudgeStartRow { get; set; } = 5;
    public string TashkeelJudgeNoCol { get; set; } = "L";
    public string TashkeelJudgeNameCol { get; set; } = "M";
    public string TashkeelJudgeCircuitNoCol { get; set; } = "P";
    public string TashkeelJudgeRoleCol { get; set; } = "R";
    public string TashkeelJudgeStatusCol { get; set; } = "S";

    public int BlockTotalRows { get; set; } = 15;
    public int YearBandsCount { get; set; } = 5;
    public int PrimaryDataRowOffset { get; set; } = 1;
    public int SecondaryJudgesRowOffset { get; set; } = 8;
    public int Judge1HeaderRowOffset { get; set; } = 0;
    public int Judge3HeaderRowOffset { get; set; } = 7;
    public string Judge1HeaderCol { get; set; } = "BD";
    public string Judge2HeaderCol { get; set; } = "CF";
    public string Judge5HeaderCol { get; set; } = "DH";

    public List<ExcelImportColumnMappingDto> ColumnMappings { get; set; } = [];
}

public class ExcelImportColumnMappingDto
{
    public long Id { get; set; }
    public long ExcelImportTemplateId { get; set; }
    public string Section { get; set; } = string.Empty;
    public string ColumnLetter { get; set; } = string.Empty;
    public string TargetItemCode { get; set; } = string.Empty;
    public string TargetItemNameAr { get; set; } = string.Empty;
    public int? JudgeSlotIndex { get; set; }
    public int? TargetMonthOffset { get; set; }
    public int DisplayOrder { get; set; }
}

public class ImportPreviewResultDto
{
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string DisciplineKind { get; set; } = string.Empty;
    public long JudicialYearId { get; set; }
    public long StatisticalPeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;

    public int TotalSessionsFound { get; set; }
    public int MatchedSessionsCount { get; set; }
    public int UnmatchedSessionsCount { get; set; }
    public int BalancedSessionsCount { get; set; }
    public int UnbalancedSessionsCount { get; set; }

    public List<ImportSessionPreviewDto> Sessions { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public class ImportSessionPreviewDto
{
    public string ExcelSessionNo { get; set; } = string.Empty;
    public string ExcelSessionName { get; set; } = string.Empty;
    public string ExcelCircuitName { get; set; } = string.Empty;
    public int BlockStartRow { get; set; }

    public long? MatchedSessionId { get; set; }
    public long? MatchedSessionVersionId { get; set; }
    public string? MatchedSessionCode { get; set; }
    public string? MatchedSessionName { get; set; }
    public string MatchStatus { get; set; } = "NotFound"; // MatchedExact, MatchedByName, NotFound

    public bool IsBalanced { get; set; }
    public int BalanceDifference { get; set; }

    public int TotalIncoming { get; set; }
    public int TotalProduction { get; set; }
    public int TotalPostponed { get; set; }
    public int TotalFinalJudgments { get; set; }

    public List<ImportJudgeMatchDto> JudgesMatches { get; set; } = [];
    public List<ImportIncomingRowDto> IncomingRows { get; set; } = [];
    public List<ImportProductionRowDto> ProductionRows { get; set; } = [];
    public List<ImportJudgeJudgmentDto> JudgeJudgments { get; set; } = [];
    public List<ImportPostponementDto> Postponements { get; set; } = [];
    public List<string> ValidationMessages { get; set; } = [];
}

public class ImportJudgeMatchDto
{
    public string ExcelJudgeNo { get; set; } = string.Empty;
    public string ExcelJudgeName { get; set; } = string.Empty;
    public int SlotIndex { get; set; }
    public long? MatchedJudgeId { get; set; }
    public string? MatchedJudgeName { get; set; }
    public string? MatchedRole { get; set; }
    public bool IsMatched { get; set; }
}

public class ImportIncomingRowDto
{
    public long BandId { get; set; }
    public string BandLabel { get; set; } = string.Empty;
    public string IncomingCode { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ImportProductionRowDto
{
    public long BandId { get; set; }
    public string BandLabel { get; set; } = string.Empty;
    public string ProductionCode { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ImportJudgeJudgmentDto
{
    public long JudgeId { get; set; }
    public long BandId { get; set; }
    public string JudgmentKind { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ImportPostponementDto
{
    public long BandId { get; set; }
    public string PostponementKind { get; set; } = string.Empty;
    public long TargetPeriodId { get; set; }
    public int Count { get; set; }
}

public class ImportCommitResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ImportedSessionsCount { get; set; }
    public int UpdatedHeadersCount { get; set; }
    public List<string> Errors { get; set; } = [];
}
