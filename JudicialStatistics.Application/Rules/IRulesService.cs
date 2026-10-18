namespace JudicialStatistics.Application.Rules;

// ======================== DTOs ========================

public record CaseTaxonomyNodeDto(
    long   Id,
    long?  ParentId,
    string Code,
    string NameAr,
    short  SortOrder,
    List<CaseTaxonomyNodeDto> Children
);

public record JudgmentTreeNodeDto(
    long   Id,
    long?  ParentId,
    string Code,
    string NameAr,
    short  SortOrder,
    List<JudgmentTreeNodeDto> Children
);

public record CaseTypeDto(
    long   Id,
    long   TaxonomyNodeId,
    string TaxonomyNodeNameAr,
    string Code,
    string NameAr,
    string StatisticalLevelCode,
    string ProcedureCode,
    string AgeCategoryCode
);

public record RuleSetVersionDto(
    long     Id,
    int      VersionNo,
    string   NameAr,
    DateOnly EffectiveFrom,
    string   Status,
    long?    ApprovedByUserId,
    DateTime? ApprovedAt
);

public record CaseYearBandDto(
    long   Id,
    long   CaseYearPolicyId,
    short? FromYear,
    short? ToYear,
    string BandKind,
    string LabelAr,
    short  SortOrder
);

public record CaseYearPolicyDto(
    long   Id,
    long   JudicialYearId,
    long   CaseTypeId,
    string CaseTypeNameAr,
    int    VersionNo,
    string DecisionReference,
    string Status,
    List<CaseYearBandDto> Bands
);

public record JudgmentTypeDto(
    long   Id,
    string Code,
    string NameAr,
    bool   IsActive
);

public record IncomingTypeDto(
    long   Id,
    string Code,
    string NameAr,
    string InputSource,
    short  SortOrder
);

public record DeferralReasonDto(
    long   Id,
    string Code,
    string NameAr,
    string SourceKind,
    short  SortOrder
);

public record ProductionItemTypeDto(
    long   Id,
    string Code,
    string NameAr,
    string ItemKind,
    string EntryScreen,
    long?  DeferralReasonId,
    short  SortOrder
);

public record CaseTypeJudgmentRuleDto(
    long   Id,
    long   RuleSetVersionId,
    long   CaseTypeId,
    string CaseTypeNameAr,
    long   JudgmentTypeId,
    string JudgmentTypeNameAr,
    long   TreeNodeId,
    bool   CountsInJudgmentTotal,
    bool   RequiresAllocation,
    long?  DeferralReasonId
);

// ======================== Commands ========================

public record CaseTypeCreateCommand(
    long   TaxonomyNodeId,
    string Code,
    string NameAr,
    string StatisticalLevelCode,
    string ProcedureCode,
    string AgeCategoryCode
);

public record CaseYearPolicyCreateCommand(
    long   JudicialYearId,
    long   CaseTypeId,
    string DecisionReference,
    List<CaseYearBandDto> Bands
);

public record JudgmentTypeCreateCommand(
    string Code,
    string NameAr
);

public record JudgmentTypeUpdateCommand(
    long Id,
    string NameAr,
    bool IsActive
);

public record DeferralReasonCreateCommand(
    string Code,
    string NameAr,
    string SourceKind,
    short SortOrder
);

public record DeferralReasonUpdateCommand(
    long Id,
    string NameAr,
    string SourceKind,
    short SortOrder
);

public record IncomingTypeCreateCommand(
    string Code,
    string NameAr,
    string InputSource,
    short SortOrder
);

public record IncomingTypeUpdateCommand(
    long Id,
    string NameAr,
    string InputSource,
    short SortOrder
);

public record ProductionItemTypeCreateCommand(
    string Code,
    string NameAr,
    string ItemKind,
    string EntryScreen,
    short SortOrder
);

public record ProductionItemTypeUpdateCommand(
    long Id,
    string NameAr,
    string ItemKind,
    string EntryScreen,
    short SortOrder
);

// ======================== Interface ========================

public interface IRulesService
{
    // Taxonomy & Cases
    Task<List<CaseTaxonomyNodeDto>> GetTaxonomyTreeAsync();
    Task<List<CaseTypeDto>> GetCaseTypesAsync(long? taxonomyNodeId = null);
    Task<CaseTypeDto?> GetCaseTypeByIdAsync(long id);
    Task<long> CreateCaseTypeAsync(CaseTypeCreateCommand cmd);
    Task<List<CaseTypeDto>> GetCaseTypesForSessionVersionAsync(long sessionVersionId);
    Task AssignCaseTypesToSessionVersionAsync(long sessionVersionId, List<long> caseTypeIds);

    // RuleSet & Policies
    Task<List<RuleSetVersionDto>> GetRuleSetVersionsAsync();
    Task<RuleSetVersionDto?> GetActiveRuleSetVersionAsync();
    Task<CaseYearPolicyDto?> GetPolicyForCaseTypeAsync(long judicialYearId, long caseTypeId);
    Task<List<CaseYearPolicyDto>> GetPoliciesForYearAsync(long judicialYearId);
    Task<long> CreateCaseYearPolicyAsync(CaseYearPolicyCreateCommand cmd);
    Task UpdateCaseYearBandsAsync(long policyId, List<CaseYearBandDto> bands);

    // Lookups
    Task<List<IncomingTypeDto>> GetIncomingTypesAsync();
    Task<long> CreateIncomingTypeAsync(IncomingTypeCreateCommand cmd);
    Task UpdateIncomingTypeAsync(IncomingTypeUpdateCommand cmd);

    Task<List<ProductionItemTypeDto>> GetProductionItemTypesAsync();
    Task<long> CreateProductionItemTypeAsync(ProductionItemTypeCreateCommand cmd);
    Task UpdateProductionItemTypeAsync(ProductionItemTypeUpdateCommand cmd);

    Task<List<JudgmentTypeDto>> GetJudgmentTypesAsync(bool includeInactive = true);
    Task<long> CreateJudgmentTypeAsync(JudgmentTypeCreateCommand cmd);
    Task UpdateJudgmentTypeAsync(JudgmentTypeUpdateCommand cmd);
    Task ToggleJudgmentTypeActiveAsync(long id);

    Task<List<JudgmentTreeNodeDto>> GetJudgmentTreeAsync(long? ruleSetVersionId = null);

    Task<List<DeferralReasonDto>> GetDeferralReasonsAsync();
    Task<long> CreateDeferralReasonAsync(DeferralReasonCreateCommand cmd);
    Task UpdateDeferralReasonAsync(DeferralReasonUpdateCommand cmd);

    Task<List<CaseTypeJudgmentRuleDto>> GetJudgmentRulesAsync(long ruleSetVersionId, long? caseTypeId = null);
}
