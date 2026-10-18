using JudicialStatistics.Application.Rules;
using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

public class RulesService : IRulesService
{
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public RulesService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<CaseTaxonomyNodeDto>> GetTaxonomyTreeAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var allNodes = await context.CaseTaxonomyNodes
            .OrderBy(n => n.SortOrder)
            .ToListAsync();

        return BuildTree(allNodes, null);
    }

    private static List<CaseTaxonomyNodeDto> BuildTree(List<CaseTaxonomyNode> all, long? parentId)
    {
        return all
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder)
            .Select(n => new CaseTaxonomyNodeDto(
                n.Id,
                n.ParentId,
                n.Code,
                n.NameAr,
                n.SortOrder,
                BuildTree(all, n.Id)
            ))
            .ToList();
    }

    public async Task<List<CaseTypeDto>> GetCaseTypesAsync(long? taxonomyNodeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.CaseTypes
            .Include(c => c.TaxonomyNode)
            .AsQueryable();

        if (taxonomyNodeId.HasValue)
        {
            query = query.Where(c => c.TaxonomyNodeId == taxonomyNodeId.Value);
        }

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new CaseTypeDto(
                c.Id,
                c.TaxonomyNodeId,
                c.TaxonomyNode.NameAr,
                c.Code,
                c.NameAr,
                c.StatisticalLevelCode,
                c.ProcedureCode,
                c.AgeCategoryCode
            ))
            .ToListAsync();
    }

    public async Task<CaseTypeDto?> GetCaseTypeByIdAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var c = await context.CaseTypes
            .Include(x => x.TaxonomyNode)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c is null) return null;

        return new CaseTypeDto(
            c.Id,
            c.TaxonomyNodeId,
            c.TaxonomyNode.NameAr,
            c.Code,
            c.NameAr,
            c.StatisticalLevelCode,
            c.ProcedureCode,
            c.AgeCategoryCode
        );
    }

    public async Task<long> CreateCaseTypeAsync(CaseTypeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        if (await context.CaseTypes.AnyAsync(c => c.Code == cmd.Code))
        {
            throw new InvalidOperationException($"كود نوع القضية '{cmd.Code}' مسجل مسبقاً.");
        }

        var entity = new CaseType
        {
            TaxonomyNodeId = cmd.TaxonomyNodeId,
            Code = cmd.Code.Trim(),
            NameAr = cmd.NameAr.Trim(),
            StatisticalLevelCode = cmd.StatisticalLevelCode,
            ProcedureCode = cmd.ProcedureCode,
            AgeCategoryCode = cmd.AgeCategoryCode
        };

        context.CaseTypes.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<List<CaseTypeDto>> GetCaseTypesForSessionVersionAsync(long sessionVersionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var types = await context.SessionCaseTypes
            .Include(sct => sct.CaseType)
                .ThenInclude(ct => ct.TaxonomyNode)
            .Where(sct => sct.SessionVersionId == sessionVersionId)
            .Select(sct => sct.CaseType)
            .ToListAsync();

        // إذا لم تكن هناك قضايا محددة للجلسة، نرجع جميع أنواع القضايا لتسهيل الإدخال
        if (types.Count == 0)
        {
            types = await context.CaseTypes
                .Include(ct => ct.TaxonomyNode)
                .ToListAsync();
        }

        return types.Select(c => new CaseTypeDto(
            c.Id,
            c.TaxonomyNodeId,
            c.TaxonomyNode?.NameAr ?? string.Empty,
            c.Code,
            c.NameAr,
            c.StatisticalLevelCode,
            c.ProcedureCode,
            c.AgeCategoryCode
        )).ToList();
    }

    public async Task AssignCaseTypesToSessionVersionAsync(long sessionVersionId, List<long> caseTypeIds)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var sessionVersion = await context.SessionVersions
            .Include(sv => sv.CircuitVersion)
            .FirstOrDefaultAsync(sv => sv.Id == sessionVersionId);

        if (sessionVersion != null && sessionVersion.CircuitVersion.CircuitKind == "CivilFull")
        {
            var requestedTypes = await context.CaseTypes
                .Where(ct => caseTypeIds.Contains(ct.Id))
                .ToListAsync();

            if (requestedTypes.Any(ct => ct.StatisticalLevelCode == "Partial" || ct.Code.Contains("PART")))
            {
                throw new InvalidOperationException("قاعدة نظام صارمة: لا يجوز إسناد قضايا المدني الجزئي أو المستعجل لدائرة مدني كلي؛ قضايا المستعجل والمدني الجزئي تختص بالدوائر الجزئية فقط.");
            }
        }

        var existing = await context.SessionCaseTypes
            .Where(sct => sct.SessionVersionId == sessionVersionId)
            .ToListAsync();

        context.SessionCaseTypes.RemoveRange(existing);

        foreach (var id in caseTypeIds)
        {
            context.SessionCaseTypes.Add(new SessionCaseType
            {
                SessionVersionId = sessionVersionId,
                CaseTypeId = id
            });
        }

        await context.SaveChangesAsync();
    }

    // ======================== RuleSet & Policies ========================

    public async Task<List<RuleSetVersionDto>> GetRuleSetVersionsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.RuleSetVersions
            .OrderByDescending(r => r.VersionNo)
            .Select(r => new RuleSetVersionDto(
                r.Id,
                r.VersionNo,
                r.NameAr,
                r.EffectiveFrom,
                r.Status,
                r.ApprovedByUserId,
                r.ApprovedAt
            ))
            .ToListAsync();
    }

    public async Task<RuleSetVersionDto?> GetActiveRuleSetVersionAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var active = await context.RuleSetVersions
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (active is null)
        {
            // إنشاء نسخة أولية افتراضية إن لم توجد
            var defaultRuleSet = new RuleSetVersion
            {
                VersionNo = 1,
                NameAr = "قواعد التصنيف الأساسية المعتمدة",
                EffectiveFrom = new DateOnly(2025, 10, 1),
                Status = "Approved"
            };
            context.RuleSetVersions.Add(defaultRuleSet);
            await context.SaveChangesAsync();

            return new RuleSetVersionDto(
                defaultRuleSet.Id,
                defaultRuleSet.VersionNo,
                defaultRuleSet.NameAr,
                defaultRuleSet.EffectiveFrom,
                defaultRuleSet.Status,
                null,
                null
            );
        }

        return new RuleSetVersionDto(
            active.Id,
            active.VersionNo,
            active.NameAr,
            active.EffectiveFrom,
            active.Status,
            active.ApprovedByUserId,
            active.ApprovedAt
        );
    }

    public async Task<CaseYearPolicyDto?> GetPolicyForCaseTypeAsync(long judicialYearId, long caseTypeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var policy = await context.CaseYearPolicies
            .Include(p => p.CaseType)
            .Include(p => p.Bands)
            .FirstOrDefaultAsync(p => p.JudicialYearId == judicialYearId && p.CaseTypeId == caseTypeId);

        if (policy is null) return null;

        return new CaseYearPolicyDto(
            policy.Id,
            policy.JudicialYearId,
            policy.CaseTypeId,
            policy.CaseType.NameAr,
            policy.VersionNo,
            policy.DecisionReference,
            policy.Status,
            policy.Bands.OrderBy(b => b.SortOrder).Select(b => new CaseYearBandDto(
                b.Id,
                b.CaseYearPolicyId,
                b.FromYear,
                b.ToYear,
                b.BandKind,
                b.LabelAr,
                b.SortOrder
            )).ToList()
        );
    }

    public async Task<List<CaseYearPolicyDto>> GetPoliciesForYearAsync(long judicialYearId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var policies = await context.CaseYearPolicies
            .Include(p => p.CaseType)
            .Include(p => p.Bands)
            .Where(p => p.JudicialYearId == judicialYearId)
            .OrderBy(p => p.CaseType.Code)
            .ToListAsync();

        return policies.Select(policy => new CaseYearPolicyDto(
            policy.Id,
            policy.JudicialYearId,
            policy.CaseTypeId,
            policy.CaseType.NameAr,
            policy.VersionNo,
            policy.DecisionReference,
            policy.Status,
            policy.Bands.OrderBy(b => b.SortOrder).Select(b => new CaseYearBandDto(
                b.Id,
                b.CaseYearPolicyId,
                b.FromYear,
                b.ToYear,
                b.BandKind,
                b.LabelAr,
                b.SortOrder
            )).ToList()
        )).ToList();
    }

    public async Task UpdateCaseYearBandsAsync(long policyId, List<CaseYearBandDto> bands)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var policy = await context.CaseYearPolicies
            .Include(p => p.Bands)
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy is null) throw new InvalidOperationException("السياسة غير موجودة.");

        var existingBands = policy.Bands.OrderBy(b => b.SortOrder).ToList();
        int i = 0;
        for (; i < Math.Min(existingBands.Count, bands.Count); i++)
        {
            var existing = existingBands[i];
            var updated = bands[i];
            existing.BandKind = updated.BandKind;
            existing.LabelAr = updated.LabelAr;
            existing.SortOrder = (short)(i + 1);
            existing.FromYear = updated.FromYear;
            existing.ToYear = updated.ToYear;
        }

        for (; i < bands.Count; i++)
        {
            var updated = bands[i];
            policy.Bands.Add(new CaseYearBand
            {
                CaseYearPolicyId = policy.Id,
                BandKind = updated.BandKind,
                LabelAr = updated.LabelAr,
                SortOrder = (short)(i + 1),
                FromYear = updated.FromYear,
                ToYear = updated.ToYear
            });
        }

        for (; i < existingBands.Count; i++)
        {
            var surplus = existingBands[i];
            bool isReferenced = await context.IncomingRows.AnyAsync(r => r.CaseYearBandId == surplus.Id)
                || await context.OpeningBalances.AnyAsync(r => r.CaseYearBandId == surplus.Id)
                || await context.ProductionRows.AnyAsync(r => r.CaseYearBandId == surplus.Id)
                || await context.ClosingBalances.AnyAsync(r => r.CaseYearBandId == surplus.Id);

            if (!isReferenced)
            {
                policy.Bands.Remove(surplus);
                context.CaseYearBands.Remove(surplus);
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<long> CreateCaseYearPolicyAsync(CaseYearPolicyCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var versionNo = (await context.CaseYearPolicies
            .Where(p => p.JudicialYearId == cmd.JudicialYearId && p.CaseTypeId == cmd.CaseTypeId)
            .MaxAsync(p => (int?)p.VersionNo) ?? 0) + 1;

        var policy = new CaseYearPolicy
        {
            JudicialYearId = cmd.JudicialYearId,
            CaseTypeId = cmd.CaseTypeId,
            VersionNo = versionNo,
            DecisionReference = cmd.DecisionReference,
            Status = "Approved"
        };

        short order = 1;
        foreach (var b in cmd.Bands)
        {
            policy.Bands.Add(new CaseYearBand
            {
                FromYear = b.FromYear,
                ToYear = b.ToYear,
                BandKind = b.BandKind,
                LabelAr = b.LabelAr,
                SortOrder = order++
            });
        }

        context.CaseYearPolicies.Add(policy);
        await context.SaveChangesAsync();
        return policy.Id;
    }

    // ======================== Lookups ========================

    public async Task<List<IncomingTypeDto>> GetIncomingTypesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var types = await context.IncomingTypes
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        if (types.Count == 0)
        {
            // Seed افتراضي لبنود المقدم
            var defaults = new[]
            {
                new IncomingType { Code = "PREV",   NameAr = "سابق (رصيد مدور)", InputSource = "Balance", SortOrder = 1 },
                new IncomingType { Code = "NEW",    NameAr = "جديد",              InputSource = "Manual",  SortOrder = 2 },
                new IncomingType { Code = "RENEW",  NameAr = "مجدد من الشطب",     InputSource = "Manual",  SortOrder = 3 },
                new IncomingType { Code = "ACCEL",  NameAr = "معجل من الوقف",     InputSource = "Manual",  SortOrder = 4 },
                new IncomingType { Code = "SHORT",  NameAr = "تقصير وارد",        InputSource = "Shortening", SortOrder = 5 },
            };
            context.IncomingTypes.AddRange(defaults);
            await context.SaveChangesAsync();
            types = defaults.ToList();
        }

        return types.Select(t => new IncomingTypeDto(
            t.Id,
            t.Code,
            t.NameAr,
            t.InputSource,
            t.SortOrder
        )).ToList();
    }

    public async Task<long> CreateIncomingTypeAsync(IncomingTypeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var code = cmd.Code.Trim().ToUpperInvariant();
        if (await context.IncomingTypes.AnyAsync(i => i.Code == code))
        {
            throw new InvalidOperationException($"كود بند المقدم '{code}' مسجل مسبقاً.");
        }

        var entity = new IncomingType
        {
            Code = code,
            NameAr = cmd.NameAr.Trim(),
            InputSource = string.IsNullOrWhiteSpace(cmd.InputSource) ? "Manual" : cmd.InputSource.Trim(),
            SortOrder = cmd.SortOrder
        };
        context.IncomingTypes.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateIncomingTypeAsync(IncomingTypeUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.IncomingTypes.FindAsync(cmd.Id)
            ?? throw new KeyNotFoundException("بند المقدم غير موجود.");

        entity.NameAr = cmd.NameAr.Trim();
        entity.InputSource = cmd.InputSource.Trim();
        entity.SortOrder = cmd.SortOrder;
        await context.SaveChangesAsync();
    }

    public async Task<List<ProductionItemTypeDto>> GetProductionItemTypesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var types = await context.ProductionItemTypes
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        if (types.Count == 0)
        {
            var defaults = new[]
            {
                new ProductionItemType { Code = "JUDGMENT_FINAL", NameAr = "أحكام قطعية / منهية للخصومة", ItemKind = "Judgment", EntryScreen = "Judgments", SortOrder = 1 },
                new ProductionItemType { Code = "JUDGMENT_PREP",  NameAr = "أحكام تمهيدية / خبير",           ItemKind = "Judgment", EntryScreen = "Judgments", SortOrder = 2 },
                new ProductionItemType { Code = "DEF_RESERVED",   NameAr = "محجوز للحكم",                    ItemKind = "Deferral", EntryScreen = "Deferrals", SortOrder = 3 },
                new ProductionItemType { Code = "DEF_EXTEND",     NameAr = "مد أجل النطق بالحكم",           ItemKind = "Action",   EntryScreen = "Actions",   SortOrder = 4 },
                new ProductionItemType { Code = "DEF_REOPEN",     NameAr = "إعادة للمرافعة",                 ItemKind = "Action",   EntryScreen = "Actions",   SortOrder = 5 },
                new ProductionItemType { Code = "DEF_OTHER",      NameAr = "تأجيل لأسباب أخرى",              ItemKind = "Deferral", EntryScreen = "Deferrals", SortOrder = 6 },
            };
            context.ProductionItemTypes.AddRange(defaults);
            await context.SaveChangesAsync();
            types = defaults.ToList();
        }

        return types.Select(t => new ProductionItemTypeDto(
            t.Id,
            t.Code,
            t.NameAr,
            t.ItemKind,
            t.EntryScreen,
            t.DeferralReasonId,
            t.SortOrder
        )).ToList();
    }

    public async Task<long> CreateProductionItemTypeAsync(ProductionItemTypeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var code = cmd.Code.Trim().ToUpperInvariant();
        if (await context.ProductionItemTypes.AnyAsync(p => p.Code == code))
        {
            throw new InvalidOperationException($"كود بند الإنتاج '{code}' مسجل مسبقاً.");
        }

        var entity = new ProductionItemType
        {
            Code = code,
            NameAr = cmd.NameAr.Trim(),
            ItemKind = cmd.ItemKind.Trim(),
            EntryScreen = string.IsNullOrWhiteSpace(cmd.EntryScreen) ? "Judgments" : cmd.EntryScreen.Trim(),
            SortOrder = cmd.SortOrder
        };
        context.ProductionItemTypes.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateProductionItemTypeAsync(ProductionItemTypeUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.ProductionItemTypes.FindAsync(cmd.Id)
            ?? throw new KeyNotFoundException("بند الإنتاج غير موجود.");

        entity.NameAr = cmd.NameAr.Trim();
        entity.ItemKind = cmd.ItemKind.Trim();
        entity.EntryScreen = cmd.EntryScreen.Trim();
        entity.SortOrder = cmd.SortOrder;
        await context.SaveChangesAsync();
    }

    public async Task<List<JudgmentTypeDto>> GetJudgmentTypesAsync(bool includeInactive = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JudgmentTypes.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(j => j.IsActive);
        }
        var types = await query
            .OrderBy(j => j.NameAr)
            .ToListAsync();

        if (types.Count == 0 && includeInactive)
        {
            var defaults = new[]
            {
                new JudgmentType { Code = "ACCEPT_REJECT", NameAr = "رفض الدعوى",         IsActive = true },
                new JudgmentType { Code = "ACCEPT_GRANT",  NameAr = "إلزام / قبول الطلبات", IsActive = true },
                new JudgmentType { Code = "NO_JURIS",      NameAr = "عدم اختصاص",         IsActive = true },
                new JudgmentType { Code = "NO_ADMIT",      NameAr = "عدم قبول",           IsActive = true },
                new JudgmentType { Code = "DROP",          NameAr = "سقوط / ترك الخصومة",   IsActive = true },
                new JudgmentType { Code = "EXPERT",        NameAr = "ندب خبير",           IsActive = true },
                new JudgmentType { Code = "ACQUIT",        NameAr = "براءة (جنح)",         IsActive = true },
                new JudgmentType { Code = "CONVICT",       NameAr = "إدانة / حبس (جنح)",   IsActive = true },
                new JudgmentType { Code = "FINE",          NameAr = "غرامة (جنح)",         IsActive = true },
            };
            context.JudgmentTypes.AddRange(defaults);
            await context.SaveChangesAsync();
            types = defaults.ToList();
        }

        return types.Select(j => new JudgmentTypeDto(
            j.Id,
            j.Code,
            j.NameAr,
            j.IsActive
        )).ToList();
    }

    public async Task<long> CreateJudgmentTypeAsync(JudgmentTypeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var code = cmd.Code.Trim().ToUpperInvariant();
        if (await context.JudgmentTypes.AnyAsync(j => j.Code == code))
        {
            throw new InvalidOperationException($"كود نوع الحكم '{code}' مسجل مسبقاً.");
        }

        var entity = new JudgmentType
        {
            Code = code,
            NameAr = cmd.NameAr.Trim(),
            IsActive = true
        };
        context.JudgmentTypes.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateJudgmentTypeAsync(JudgmentTypeUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.JudgmentTypes.FindAsync(cmd.Id)
            ?? throw new KeyNotFoundException("نوع الحكم غير موجود.");

        entity.NameAr = cmd.NameAr.Trim();
        entity.IsActive = cmd.IsActive;
        await context.SaveChangesAsync();
    }

    public async Task ToggleJudgmentTypeActiveAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.JudgmentTypes.FindAsync(id)
            ?? throw new KeyNotFoundException("نوع الحكم غير موجود.");

        entity.IsActive = !entity.IsActive;
        await context.SaveChangesAsync();
    }

    public async Task<List<JudgmentTreeNodeDto>> GetJudgmentTreeAsync(long? ruleSetVersionId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JudgmentTreeNodes.AsQueryable();
        if (ruleSetVersionId.HasValue)
        {
            query = query.Where(n => n.RuleSetVersionId == ruleSetVersionId.Value);
        }

        var allNodes = await query
            .OrderBy(n => n.SortOrder)
            .ToListAsync();

        return BuildJudgmentTree(allNodes, null);
    }

    private static List<JudgmentTreeNodeDto> BuildJudgmentTree(List<JudgmentTreeNode> all, long? parentId)
    {
        return all
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder)
            .Select(n => new JudgmentTreeNodeDto(
                n.Id,
                n.ParentId,
                n.Code,
                n.NameAr,
                n.SortOrder,
                BuildJudgmentTree(all, n.Id)
            ))
            .ToList();
    }

    public async Task<List<DeferralReasonDto>> GetDeferralReasonsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var reasons = await context.DeferralReasons
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        if (reasons.Count == 0)
        {
            var defaults = new[]
            {
                new DeferralReason { Code = "RESERVE", NameAr = "محجوز للحكم",           SourceKind = "Hearing", SortOrder = 1 },
                new DeferralReason { Code = "EXTEND",  NameAr = "مد أجل النطق بالحكم",  SourceKind = "Judge",   SortOrder = 2 },
                new DeferralReason { Code = "REPLEAD", NameAr = "إعادة للمرافعة",         SourceKind = "Judge",   SortOrder = 3 },
                new DeferralReason { Code = "REPORT",  NameAr = "لورود التقرير",         SourceKind = "Hearing", SortOrder = 4 },
                new DeferralReason { Code = "NOTIFY",  NameAr = "للإعلان بالدعوى",       SourceKind = "Hearing", SortOrder = 5 },
                new DeferralReason { Code = "DOCS",    NameAr = "لتقديم المستندات",       SourceKind = "Hearing", SortOrder = 6 },
            };
            context.DeferralReasons.AddRange(defaults);
            await context.SaveChangesAsync();
            reasons = defaults.ToList();
        }

        return reasons.Select(r => new DeferralReasonDto(
            r.Id,
            r.Code,
            r.NameAr,
            r.SourceKind,
            r.SortOrder
        )).ToList();
    }

    public async Task<long> CreateDeferralReasonAsync(DeferralReasonCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var code = cmd.Code.Trim().ToUpperInvariant();
        if (await context.DeferralReasons.AnyAsync(d => d.Code == code))
        {
            throw new InvalidOperationException($"كود سبب التأجيل '{code}' مسجل مسبقاً.");
        }

        var entity = new DeferralReason
        {
            Code = code,
            NameAr = cmd.NameAr.Trim(),
            SourceKind = string.IsNullOrWhiteSpace(cmd.SourceKind) ? "Hearing" : cmd.SourceKind.Trim(),
            SortOrder = cmd.SortOrder
        };
        context.DeferralReasons.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateDeferralReasonAsync(DeferralReasonUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.DeferralReasons.FindAsync(cmd.Id)
            ?? throw new KeyNotFoundException("سبب التأجيل غير موجود.");

        entity.NameAr = cmd.NameAr.Trim();
        entity.SourceKind = cmd.SourceKind.Trim();
        entity.SortOrder = cmd.SortOrder;
        await context.SaveChangesAsync();
    }

    public async Task<List<CaseTypeJudgmentRuleDto>> GetJudgmentRulesAsync(long ruleSetVersionId, long? caseTypeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.CaseTypeJudgmentRules
            .Include(r => r.CaseType)
            .Include(r => r.JudgmentType)
            .Where(r => r.RuleSetVersionId == ruleSetVersionId);

        if (caseTypeId.HasValue)
        {
            query = query.Where(r => r.CaseTypeId == caseTypeId.Value);
        }

        return await query
            .OrderBy(r => r.CaseType.NameAr)
            .Select(r => new CaseTypeJudgmentRuleDto(
                r.Id,
                r.RuleSetVersionId,
                r.CaseTypeId,
                r.CaseType.NameAr,
                r.JudgmentTypeId,
                r.JudgmentType.NameAr,
                r.TreeNodeId,
                r.CountsInJudgmentTotal,
                r.RequiresAllocation,
                r.DeferralReasonId
            ))
            .ToListAsync();
    }
}
