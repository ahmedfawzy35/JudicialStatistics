using JudicialStatistics.Application.Statistics;
using JudicialStatistics.Domain.Statistics;
using JudicialStatistics.Domain.Years;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public StatisticsService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<StatisticsHeaderDto>> GetHeadersAsync(
        long? judicialYearId = null,
        long? statisticalPeriodId = null,
        long? sessionVersionId = null,
        string? status = null,
        long? currentUserId = null,
        long? primaryCourtId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.StatisticsHeaders
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.Session)
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.CircuitVersion)
                    .ThenInclude(cv => cv.Circuit)
                        .ThenInclude(c => c.CourtUnit)
            .Include(h => h.StatisticalPeriod)
                .ThenInclude(p => p.JudicialYear)
            .Include(h => h.CreatedBy)
            .Include(h => h.SubmittedBy)
            .Include(h => h.ReviewedBy)
            .Include(h => h.ApprovedBy)
            .AsQueryable();

        if (currentUserId.HasValue && currentUserId.Value > 0)
        {
            var scope = await UserScopeHelper.ResolveUserScopeAsync(context, currentUserId.Value);
            if (!scope.IsSystemAdmin && !scope.IsScopeAdmin)
            {
                if (scope.AllowedSessionIds != null)
                {
                    query = query.Where(h => scope.AllowedSessionIds.Contains(h.SessionVersion.SessionId));
                }
                else if (scope.AllowedCourtIds != null)
                {
                    query = query.Where(h => scope.AllowedCourtIds.Contains(h.SessionVersion.CircuitVersion.Circuit.CourtUnitId));
                }
            }
            else if (scope.IsScopeAdmin && scope.PrimaryCourtId.HasValue)
            {
                var pId = scope.PrimaryCourtId.Value;
                query = query.Where(h => h.SessionVersion.CircuitVersion.Circuit.CourtUnit.ParentCourtUnitId == pId ||
                                         h.SessionVersion.CircuitVersion.Circuit.CourtUnitId == pId);
            }
        }

        if (primaryCourtId.HasValue && primaryCourtId.Value > 0)
        {
            query = query.Where(h => h.SessionVersion.CircuitVersion.Circuit.CourtUnit.ParentCourtUnitId == primaryCourtId.Value ||
                                     h.SessionVersion.CircuitVersion.Circuit.CourtUnitId == primaryCourtId.Value);
        }

        if (judicialYearId.HasValue)
            query = query.Where(h => h.StatisticalPeriod.JudicialYearId == judicialYearId.Value);

        if (statisticalPeriodId.HasValue)
            query = query.Where(h => h.StatisticalPeriodId == statisticalPeriodId.Value);

        if (sessionVersionId.HasValue)
            query = query.Where(h => h.SessionVersionId == sessionVersionId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(h => h.Status == status);

        return await query
            .OrderByDescending(h => h.StatisticalPeriod.MonthStartDate)
            .ThenBy(h => h.SessionVersion.Session.Code)
            .Select(h => MapHeaderToDto(h))
            .ToListAsync();
    }

    public async Task<StatisticsMatrixDto> GetOrCreateMatrixAsync(
        long sessionVersionId,
        long statisticalPeriodId,
        long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        await ValidateUserSessionAuthorizationAsync(context, sessionVersionId, currentUserId, "إنشاء أو بدء إدخال إحصائية لهذه الجلسة");

        var existing = await context.StatisticsHeaders
            .FirstOrDefaultAsync(h => h.SessionVersionId == sessionVersionId && h.StatisticalPeriodId == statisticalPeriodId);

        if (existing is not null)
            return await LoadMatrixAsync(context, existing.Id, currentUserId);

        // إنشاء رأس إحصائية جديد
        var period = await context.StatisticalPeriods
            .Include(p => p.JudicialYear)
            .FirstOrDefaultAsync(p => p.Id == statisticalPeriodId)
            ?? throw new KeyNotFoundException($"الفترة الإحصائية رقم {statisticalPeriodId} غير موجودة.");

        var sessionVersion = await context.SessionVersions
            .Include(sv => sv.CircuitVersion).ThenInclude(cv => cv.Circuit).ThenInclude(c => c.CourtUnit)
            .Include(sv => sv.Session).ThenInclude(s => s.Circuit).ThenInclude(c => c.CourtUnit)
            .FirstOrDefaultAsync(sv => sv.Id == sessionVersionId);

        long courtUnitId = sessionVersion?.CircuitVersion?.Circuit?.CourtUnitId
            ?? sessionVersion?.Session?.Circuit?.CourtUnitId
            ?? 0;

        string courtName = sessionVersion?.CircuitVersion?.Circuit?.CourtUnit?.NameAr
            ?? sessionVersion?.Session?.Circuit?.CourtUnit?.NameAr
            ?? "المحكمة";

        bool isPeriodOpen = await CheckPeriodOpenForCourtAsync(context, statisticalPeriodId, courtUnitId);
        if (!isPeriodOpen)
            throw new InvalidOperationException($"لا يمكن إنشاء إحصائية جديدة لهذا الشهر لأنه مغلق للإدخال حالياً لـ ({courtName}) بقرار من رئيس قسم الإحصاء.");

        var activeRuleSet = await context.RuleSetVersions
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (activeRuleSet is null)
        {
            activeRuleSet = new RuleSetVersion
            {
                VersionNo = 1,
                NameAr = "قواعد التصنيف الأساسية",
                EffectiveFrom = new DateOnly(2025, 10, 1),
                Status = "Approved"
            };
            context.RuleSetVersions.Add(activeRuleSet);
            await context.SaveChangesAsync();
        }

        var sessionCaseTypeIds = await context.SessionCaseTypes
            .Where(sct => sct.SessionVersionId == sessionVersionId)
            .Select(sct => sct.CaseTypeId)
            .ToListAsync();

        var defaultPolicy = await context.CaseYearPolicies
            .Include(p => p.Bands)
            .FirstOrDefaultAsync(p => p.JudicialYearId == period.JudicialYearId && sessionCaseTypeIds.Contains(p.CaseTypeId))
            ?? await context.CaseYearPolicies
            .Include(p => p.Bands)
            .FirstOrDefaultAsync(p => p.JudicialYearId == period.JudicialYearId);

        if (defaultPolicy is null)
        {
            var firstCaseType = await context.CaseTypes.FirstOrDefaultAsync();
            if (firstCaseType is null)
                throw new InvalidOperationException("لا توجد أنواع قضايا مسجلة بالنظام.");

            defaultPolicy = new CaseYearPolicy
            {
                JudicialYearId = period.JudicialYearId,
                CaseTypeId = firstCaseType.Id,
                VersionNo = 1,
                DecisionReference = "قرار سياسة تصنيف شرائح سنوات القيد المعتمدة للعام القضائي",
                Status = "Approved"
            };

            defaultPolicy.Bands.Add(new CaseYearBand
            {
                BandKind = "AllYears",
                LabelAr = "كافة السنوات",
                SortOrder = 1
            });

            context.CaseYearPolicies.Add(defaultPolicy);
            await context.SaveChangesAsync();
        }
        else if (defaultPolicy.Bands.Count == 0)
        {
            defaultPolicy.Bands.Add(new CaseYearBand
            {
                BandKind = "AllYears",
                LabelAr = "كافة السنوات",
                SortOrder = 1
            });
            await context.SaveChangesAsync();
        }

        var userExists = await context.AppUsers.AnyAsync(u => u.Id == currentUserId);
        long? validUserId = userExists ? currentUserId : (await context.AppUsers.Select(u => (long?)u.Id).FirstOrDefaultAsync());

        var header = new StatisticsHeader
        {
            SessionVersionId = sessionVersionId,
            StatisticalPeriodId = statisticalPeriodId,
            RuleSetVersionId = activeRuleSet.Id,
            CaseYearPolicyId = defaultPolicy.Id,
            Status = "Draft",
            CreatedByUserId = validUserId,
            CreatedAt = DateTime.UtcNow
        };

        header.AuditEntries.Add(new AuditEntry
        {
            UserId = currentUserId,
            Action = "إنشاء مسودة الإحصائية",
            Comment = "تم فتح نموذج الإحصائية للشهر",
            OccurredAt = DateTime.UtcNow
        });

        context.StatisticsHeaders.Add(header);
        await context.SaveChangesAsync();

        return await LoadMatrixAsync(context, header.Id, currentUserId);
    }

    public async Task<StatisticsMatrixDto> GetMatrixByIdAsync(long headerId, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await LoadMatrixAsync(context, headerId, currentUserId);
    }

    private static async Task<StatisticsMatrixDto> LoadMatrixAsync(
        JudicialDbContext context,
        long headerId,
        long currentUserId)
    {
        var header = await context.StatisticsHeaders
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.Session)
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.CircuitVersion)
                    .ThenInclude(cv => cv.Circuit)
                        .ThenInclude(c => c.CourtUnit)
            .Include(h => h.StatisticalPeriod)
                .ThenInclude(p => p.JudicialYear)
            .Include(h => h.CreatedBy)
            .Include(h => h.SubmittedBy)
            .Include(h => h.ReviewedBy)
            .Include(h => h.ApprovedBy)
            .FirstOrDefaultAsync(h => h.Id == headerId)
            ?? throw new KeyNotFoundException($"الإحصائية رقم {headerId} غير موجودة.");

        await ValidateUserSessionAuthorizationAsync(context, header.SessionVersionId, currentUserId, "استعراض أو تعديل إحصائية هذه الجلسة");

        // ===== OpeningBalances (سابق في كشف المقدم) =====
        // طبقاً للقاعدة 33 و 34:
        // في شهر أكتوبر (بداية العام القضائي الجديد): يُستمد 100% من الرصيد الافتتاحي للجلسة SessionVersion.OpeningBalances
        // مع عزل تام وتجاهل لمؤجلات شهر سبتمبر بالعام السابق.
        // في باقي الشهور (نوفمبر إلى سبتمبر): يُستمد آلياً من المؤجلات المرحلة إلى هذا الشهر من الشهور السابقة داخل نفس العام القضائي حصراً.
        List<OpeningBalanceDto> openings;
        bool isOctober = header.StatisticalPeriod.MonthStartDate.Month == 10;
        if (isOctober)
        {
            openings = await context.OpeningBalances
                .Include(o => o.CaseType)
                .Include(o => o.CaseYearBand)
                .Where(o => o.SessionVersionId == header.SessionVersionId)
                .Select(o => new OpeningBalanceDto(
                    o.Id, o.CaseTypeId, o.CaseType.NameAr,
                    o.CaseYearBandId, o.CaseYearBand != null ? o.CaseYearBand.LabelAr : "كافة السنوات",
                    o.Count))
                .ToListAsync();
        }
        else
        {
            var currentPeriodStartDate = header.StatisticalPeriod.MonthStartDate;
            var currentJudicialYearId = header.StatisticalPeriod.JudicialYearId;

            var carriedPostponements = await context.Postponements
                .Include(p => p.StatisticsHeader)
                    .ThenInclude(h => h.StatisticalPeriod)
                .Include(p => p.CaseYearBand)
                .Where(p => p.StatisticsHeader.SessionVersionId == header.SessionVersionId
                         && p.TargetPeriodId == header.StatisticalPeriodId
                         && p.StatisticsHeader.StatisticalPeriod.JudicialYearId == currentJudicialYearId
                         && p.StatisticsHeader.StatisticalPeriod.MonthStartDate < currentPeriodStartDate)
                .ToListAsync();

            var sessionCaseTypes = await context.SessionCaseTypes
                .Include(sct => sct.CaseType)
                .Where(sct => sct.SessionVersionId == header.SessionVersionId)
                .ToListAsync();
            var primaryCaseType = sessionCaseTypes.FirstOrDefault()?.CaseType;
            long defaultCaseTypeId = primaryCaseType?.Id ?? 0;
            string defaultCaseTypeName = primaryCaseType?.NameAr ?? "قضايا الجلسة";

            openings = carriedPostponements
                .GroupBy(p => new { p.CaseYearBandId, Label = p.CaseYearBand != null ? p.CaseYearBand.LabelAr : "كافة السنوات" })
                .Select((g, idx) => new OpeningBalanceDto(
                    idx + 1,
                    defaultCaseTypeId,
                    defaultCaseTypeName,
                    g.Key.CaseYearBandId,
                    g.Key.Label,
                    g.Sum(x => x.Count)
                ))
                .ToList();
        }

        // ===== IncomingRows (مع SourcePeriod للتقصير) =====
        var incomings = await context.IncomingRows
            .Include(i => i.CaseType)
            .Include(i => i.CaseYearBand)
            .Include(i => i.IncomingType)
            .Include(i => i.SourcePeriod)
            .Where(i => i.StatisticsHeaderId == headerId)
            .Select(i => new IncomingRowDto(
                i.Id, i.CaseTypeId, i.CaseType.NameAr,
                i.CaseYearBandId, i.CaseYearBand != null ? i.CaseYearBand.LabelAr : "كافة السنوات",
                i.IncomingTypeId, i.IncomingType.Code, i.IncomingType.NameAr,
                i.SourcePeriodId,
                i.SourcePeriod != null ? i.SourcePeriod.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG")) : null,
                i.Count))
            .ToListAsync();

        // ===== ProductionRows (جنائي: تفاصيل الأنواع) =====
        var productions = await context.ProductionRows
            .Include(p => p.CaseType)
            .Include(p => p.CaseYearBand)
            .Include(p => p.ProductionItemType)
            .Where(p => p.StatisticsHeaderId == headerId)
            .ToListAsync();

        var prodIds = productions.Select(p => p.Id).ToList();
        var allocations = prodIds.Any()
            ? await context.JudgmentAllocations
                .Include(a => a.Judge)
                .Where(a => prodIds.Contains(a.ProductionRowId))
                .ToListAsync()
            : new List<JudgmentAllocation>();

        var productionDtos = productions.Select(p => new ProductionRowDto(
            p.Id, p.CaseTypeId, p.CaseType.NameAr,
            p.CaseYearBandId, p.CaseYearBand != null ? p.CaseYearBand.LabelAr : "كافة السنوات",
            p.ProductionItemTypeId, p.ProductionItemType.Code, p.ProductionItemType.NameAr,
            p.ProductionItemType.ItemKind == "Judgment",
            p.ProductionItemType.Code.Contains("CONVICT") || p.ProductionItemType.Code.Contains("ACQUIT"),
            p.Count,
            allocations
                .Where(a => a.ProductionRowId == p.Id)
                .Select(a => new JudgmentAllocationDto(a.Id, a.ProductionRowId, a.JudgeId, a.Judge.FullNameAr, a.Count))
                .ToList()
        )).ToList();

        // ===== JudgeJudgments (أحكام القضاة التفصيلية) =====
        var judgeJudgments = await context.JudgeJudgments
            .Include(j => j.Judge)
            .Include(j => j.CaseYearBand)
            .Where(j => j.StatisticsHeaderId == headerId)
            .Select(j => new JudgeJudgmentDto(
                j.Id, j.JudgeId, j.Judge.FullNameAr,
                j.CaseYearBandId, j.CaseYearBand != null ? j.CaseYearBand.LabelAr : "كافة السنوات",
                j.JudgmentKind, j.Count))
            .ToListAsync();

        // ===== Postponements (المؤجلات على الشهور) =====
        var postponements = await context.Postponements
            .Include(p => p.CaseYearBand)
            .Include(p => p.TargetPeriod)
            .Where(p => p.StatisticsHeaderId == headerId)
            .Select(p => new PostponementDto(
                p.Id, p.CaseYearBandId,
                p.CaseYearBand != null ? p.CaseYearBand.LabelAr : "كافة السنوات",
                p.PostponementKind, p.TargetPeriodId,
                p.TargetPeriod.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG")),
                p.Count))
            .ToListAsync();

        // ===== ClosingBalances =====
        var closings = await context.ClosingBalances
            .Include(c => c.CaseType)
            .Include(c => c.CaseYearBand)
            .Where(c => c.StatisticsHeaderId == headerId)
            .Select(c => new ClosingBalanceDto(
                c.Id, c.CaseTypeId, c.CaseType.NameAr,
                c.CaseYearBandId, c.CaseYearBand != null ? c.CaseYearBand.LabelAr : "كافة السنوات",
                c.Count, c.CarriedForward))
            .ToListAsync();

        // ===== AuditEntries =====
        var audits = await context.AuditEntries
            .Include(a => a.User)
            .Where(a => a.StatisticsHeaderId == headerId)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new AuditEntryDto(
                a.Id, a.UserId,
                a.User != null ? a.User.FullNameAr : "النظام",
                a.Action, a.Comment, a.OccurredAt))
            .ToListAsync();

        long courtUnitId = header.SessionVersion?.CircuitVersion?.Circuit?.CourtUnitId
            ?? header.SessionVersion?.Session?.Circuit?.CourtUnitId
            ?? 0;

        bool isPeriodOpen = await CheckPeriodOpenForCourtAsync(context, header.StatisticalPeriodId, courtUnitId);
        bool canEdit   = isPeriodOpen && (header.Status is "Draft" or "Reopened");
        bool canSubmit = isPeriodOpen && (header.Status is "Draft" or "Reopened");
        bool canReview = header.Status is "Submitted";
        bool canApprove = header.Status is "UnderReview" or "Submitted";
        bool canReopen  = header.Status is "Approved";

        return new StatisticsMatrixDto(
            MapHeaderToDto(header, isPeriodOpen),
            openings, incomings, productionDtos,
            judgeJudgments, postponements,
            closings, audits,
            canEdit, canSubmit, canReview, canApprove, canReopen
        );
    }

    private static StatisticsHeaderDto MapHeaderToDto(StatisticsHeader h, bool? isPeriodOpen = null)
    {
        var kind = h.SessionVersion?.CircuitVersion?.CircuitKind ?? "CivilFull";
        string discipline;
        if (kind is "Partial" or "Summary")
        {
            var sName = h.SessionVersion?.Session?.NameAr ?? "";
            var sCode = h.SessionVersion?.Session?.Code ?? "";
            discipline = (sCode.Contains("-CIV-") || sName.Contains("مدني"))
                ? JudicialStatistics.Application.Organization.StatisticalDisciplines.Civil
                : JudicialStatistics.Application.Organization.StatisticalDisciplines.Criminal;
        }
        else
        {
            discipline = JudicialStatistics.Application.Organization.StatisticalDisciplines.FromCircuitKind(kind);
        }

        return new StatisticsHeaderDto(
            h.Id, h.SessionVersionId,
            h.SessionVersion?.Session?.NameAr ?? $"جلسة {h.SessionVersionId}",
            h.SessionVersion?.CircuitVersion?.Circuit?.NameAr ?? string.Empty,
            kind, discipline,
            h.StatisticalPeriodId,
            h.StatisticalPeriod?.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG")) ?? string.Empty,
            h.StatisticalPeriod?.JudicialYearId ?? 1,
            h.StatisticalPeriod?.JudicialYear?.NameAr ?? string.Empty,
            h.RuleSetVersionId, h.CaseYearPolicyId, h.Status,
            h.CreatedByUserId, h.CreatedBy?.FullNameAr, h.CreatedAt,
            h.SubmittedByUserId, h.SubmittedBy?.FullNameAr, h.SubmittedAt,
            h.ReviewedByUserId, h.ReviewedBy?.FullNameAr, h.ReviewedAt,
            h.ApprovedByUserId, h.ApprovedBy?.FullNameAr, h.ApprovedAt,
            h.ReviewComment, h.RejectionReason, h.RowVersion,
            isPeriodOpen ?? (h.StatisticalPeriod?.IsOpenForEntry ?? true)
        );
    }

    public async Task SaveDraftAsync(SaveStatisticsDataCommand cmd, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var header = await context.StatisticsHeaders
            .Include(h => h.StatisticalPeriod)
            .Include(h => h.SessionVersion).ThenInclude(sv => sv.CircuitVersion).ThenInclude(cv => cv.Circuit).ThenInclude(c => c.CourtUnit)
            .Include(h => h.SessionVersion).ThenInclude(sv => sv.Session).ThenInclude(s => s.Circuit).ThenInclude(c => c.CourtUnit)
            .FirstOrDefaultAsync(h => h.Id == cmd.HeaderId)
            ?? throw new KeyNotFoundException($"الإحصائية رقم {cmd.HeaderId} غير موجودة.");

        await ValidateUserSessionAuthorizationAsync(context, header.SessionVersionId, currentUserId, "حفظ مسودة إحصائية لهذه الجلسة");

        long courtUnitId = header.SessionVersion?.CircuitVersion?.Circuit?.CourtUnitId
            ?? header.SessionVersion?.Session?.Circuit?.CourtUnitId
            ?? 0;

        string courtName = header.SessionVersion?.CircuitVersion?.Circuit?.CourtUnit?.NameAr
            ?? header.SessionVersion?.Session?.Circuit?.CourtUnit?.NameAr
            ?? "المحكمة";

        bool isPeriodOpen = await CheckPeriodOpenForCourtAsync(context, header.StatisticalPeriodId, courtUnitId);
        if (!isPeriodOpen)
            throw new InvalidOperationException($"لا يمكن حفظ مسودة الإحصائية لأن إدخال وتعديل هذا الشهر مغلق حالياً لـ ({courtName}) بقرار من رئيس قسم الإحصاء.");

        if (header.Status is not ("Draft" or "Reopened"))
            throw new InvalidOperationException("لا يمكن تعديل الإحصائية إلا في حالتي المسودة أو المعاد فتحها.");

        // ملاحظة: الرصيد الافتتاحي للجلسة (OpeningBalance) لا يُعدل من مسودة الإحصائية الشهرية (القاعدة 33).
        // ===== المقدمات (مع SourcePeriodId للتقصير) =====
        var existingIncomings = await context.IncomingRows
            .Where(i => i.StatisticsHeaderId == cmd.HeaderId)
            .ToListAsync();
        context.IncomingRows.RemoveRange(existingIncomings);

        foreach (var inc in cmd.IncomingRows)
        {
            context.IncomingRows.Add(new IncomingRow
            {
                StatisticsHeaderId = cmd.HeaderId,
                CaseTypeId = inc.CaseTypeId,
                CaseYearBandId = inc.CaseYearBandId,
                IncomingTypeId = inc.IncomingTypeId,
                SourcePeriodId = inc.SourcePeriodId,  // للتقصير فقط
                Count = inc.Count
            });
        }

        // ===== الإنتاج (جنائي: تفاصيل الأنواع) =====
        var existingProds = await context.ProductionRows
            .Where(p => p.StatisticsHeaderId == cmd.HeaderId)
            .ToListAsync();
        context.ProductionRows.RemoveRange(existingProds);

        foreach (var prod in cmd.ProductionRows)
        {
            var row = new ProductionRow
            {
                StatisticsHeaderId = cmd.HeaderId,
                CaseTypeId = prod.CaseTypeId,
                CaseYearBandId = prod.CaseYearBandId,
                ProductionItemTypeId = prod.ProductionItemTypeId,
                Count = prod.Count
            };

            if (prod.Allocations is { Count: > 0 })
            {
                foreach (var alloc in prod.Allocations)
                {
                    context.JudgmentAllocations.Add(new JudgmentAllocation
                    {
                        ProductionRow = row,
                        JudgeId = alloc.JudgeId,
                        Count = alloc.Count
                    });
                }
            }

            context.ProductionRows.Add(row);
        }

        // ===== أحكام القضاة التفصيلية (مدني: كامل | جنائي: فئات إجمالية) =====
        var existingJudgeJudgments = await context.JudgeJudgments
            .Where(j => j.StatisticsHeaderId == cmd.HeaderId)
            .ToListAsync();
        context.JudgeJudgments.RemoveRange(existingJudgeJudgments);

        foreach (var jj in cmd.JudgeJudgments)
        {
            context.JudgeJudgments.Add(new JudgeJudgment
            {
                StatisticsHeaderId = cmd.HeaderId,
                JudgeId = jj.JudgeId,
                CaseYearBandId = jj.CaseYearBandId,
                JudgmentKind = jj.JudgmentKind,
                Count = jj.Count
            });
        }

        // ===== المؤجلات على الشهور =====
        var existingPostponements = await context.Postponements
            .Where(p => p.StatisticsHeaderId == cmd.HeaderId)
            .ToListAsync();
        context.Postponements.RemoveRange(existingPostponements);

        foreach (var post in cmd.Postponements)
        {
            context.Postponements.Add(new Postponement
            {
                StatisticsHeaderId = cmd.HeaderId,
                CaseYearBandId = post.CaseYearBandId,
                PostponementKind = post.PostponementKind,
                TargetPeriodId = post.TargetPeriodId,
                Count = post.Count
            });
        }

        context.AuditEntries.Add(new AuditEntry
        {
            StatisticsHeaderId = cmd.HeaderId,
            UserId = currentUserId,
            Action = "حفظ المسودة",
            Comment = "تم تحديث أرقام الإحصائية وحفظ المسودة",
            OccurredAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task SubmitAsync(long headerId, byte[] rowVersion, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var header = await context.StatisticsHeaders
            .Include(h => h.StatisticalPeriod)
            .Include(h => h.SessionVersion).ThenInclude(sv => sv.CircuitVersion).ThenInclude(cv => cv.Circuit).ThenInclude(c => c.CourtUnit)
            .Include(h => h.SessionVersion).ThenInclude(sv => sv.Session).ThenInclude(s => s.Circuit).ThenInclude(c => c.CourtUnit)
            .FirstOrDefaultAsync(h => h.Id == headerId)
            ?? throw new KeyNotFoundException($"الإحصائية رقم {headerId} غير موجودة.");

        await ValidateUserSessionAuthorizationAsync(context, header.SessionVersionId, currentUserId, "تقديم إحصائية هذه الجلسة للمراجعة");

        long courtUnitId = header.SessionVersion?.CircuitVersion?.Circuit?.CourtUnitId
            ?? header.SessionVersion?.Session?.Circuit?.CourtUnitId
            ?? 0;

        string courtName = header.SessionVersion?.CircuitVersion?.Circuit?.CourtUnit?.NameAr
            ?? header.SessionVersion?.Session?.Circuit?.CourtUnit?.NameAr
            ?? "المحكمة";

        bool isPeriodOpen = await CheckPeriodOpenForCourtAsync(context, header.StatisticalPeriodId, courtUnitId);
        if (!isPeriodOpen)
            throw new InvalidOperationException($"لا يمكن تقديم الإحصائية للمراجعة لأن إدخال هذا الشهر مغلق حالياً لـ ({courtName}) بقرار من رئيس قسم الإحصاء.");

        if (header.Status is not ("Draft" or "Reopened"))
            throw new InvalidOperationException("لا يمكن تقديم الإحصائية إلا من حالة المسودة.");

        header.Status = "Submitted";
        header.SubmittedByUserId = currentUserId;
        header.SubmittedAt = DateTime.UtcNow;

        context.AuditEntries.Add(new AuditEntry
        {
            StatisticsHeaderId = headerId,
            UserId = currentUserId,
            Action = "تقديم الإحصائية",
            Comment = "تم إرسال الإحصائية للمراجعة والاعتماد",
            OccurredAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task ReviewAsync(long headerId, byte[] rowVersion, bool approved, string? comment, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var header = await context.StatisticsHeaders.FindAsync(headerId)
            ?? throw new KeyNotFoundException($"الإحصائية رقم {headerId} غير موجودة.");

        if (approved)
        {
            header.Status = "UnderReview";
            header.ReviewedByUserId = currentUserId;
            header.ReviewedAt = DateTime.UtcNow;
            header.ReviewComment = comment;

            context.AuditEntries.Add(new AuditEntry
            {
                StatisticsHeaderId = headerId,
                UserId = currentUserId,
                Action = "مراجعة — مقبولة",
                Comment = comment ?? "تمت المراجعة وتأكيد دقة البيانات",
                OccurredAt = DateTime.UtcNow
            });
        }
        else
        {
            header.Status = "Draft";
            header.RejectionReason = comment;

            context.AuditEntries.Add(new AuditEntry
            {
                StatisticsHeaderId = headerId,
                UserId = currentUserId,
                Action = "إرجاع للتعديل",
                Comment = comment ?? "تمت إعادة الإحصائية لمسودة لوجود ملاحظات",
                OccurredAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task ApproveAsync(long headerId, byte[] rowVersion, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var header = await context.StatisticsHeaders.FindAsync(headerId)
            ?? throw new KeyNotFoundException($"الإحصائية رقم {headerId} غير موجودة.");

        // ===== حساب الرصيد السابق (القاعدة 33 و 34) =====
        var period = await context.StatisticalPeriods.FindAsync(header.StatisticalPeriodId);
        bool isOctober = period?.MonthStartDate.Month == 10;

        List<(long CaseYearBandId, int Count, long CaseTypeId)> previousBalances;
        if (isOctober)
        {
            var sessionOpenings = await context.OpeningBalances
                .Where(o => o.SessionVersionId == header.SessionVersionId)
                .ToListAsync();
            previousBalances = sessionOpenings.Select(o => (o.CaseYearBandId, o.Count, o.CaseTypeId)).ToList();
        }
        else
        {
            var carriedPostponements = await context.Postponements
                .Include(p => p.StatisticsHeader)
                    .ThenInclude(h => h.StatisticalPeriod)
                .Where(p => p.StatisticsHeader.SessionVersionId == header.SessionVersionId
                         && p.TargetPeriodId == header.StatisticalPeriodId
                         && period != null && p.StatisticsHeader.StatisticalPeriod.JudicialYearId == period.JudicialYearId
                         && period != null && p.StatisticsHeader.StatisticalPeriod.MonthStartDate < period.MonthStartDate)
                .ToListAsync();

            previousBalances = carriedPostponements
                .GroupBy(p => p.CaseYearBandId)
                .Select(g => (g.Key, g.Sum(x => x.Count), 0L))
                .ToList();
        }

        var incomings = await context.IncomingRows
            .Where(i => i.StatisticsHeaderId == headerId)
            .ToListAsync();

        // أحكام القضاة المحسوبة في الإجمالي (باستثناء البيانات المستقلة مثل RETURN_APPEAL_IND)
        var judgeJudgments = await context.JudgeJudgments
            .Where(j => j.StatisticsHeaderId == headerId
                     && j.JudgmentKind != "RETURN_APPEAL_IND")
            .ToListAsync();

        // حذف الأرصدة الختامية السابقة
        var oldClosings = await context.ClosingBalances
            .Where(c => c.StatisticsHeaderId == headerId)
            .ToListAsync();
        context.ClosingBalances.RemoveRange(oldClosings);

        // حساب المفاتيح الفريدة (CaseYearBandId)
        var bandIds = previousBalances.Select(o => o.CaseYearBandId)
            .Union(incomings.Select(i => i.CaseYearBandId))
            .Union(judgeJudgments.Select(j => j.CaseYearBandId))
            .Distinct();

        // جلب CaseTypeId الافتراضي
        var caseTypeId = previousBalances.FirstOrDefault(b => b.CaseTypeId > 0).CaseTypeId;
        if (caseTypeId == 0)
        {
            caseTypeId = incomings.FirstOrDefault()?.CaseTypeId
                ?? (await context.CaseTypes.Select(ct => ct.Id).FirstOrDefaultAsync());
        }

        foreach (var bandId in bandIds)
        {
            var open = previousBalances.Where(o => o.CaseYearBandId == bandId).Sum(o => o.Count);

            // المقدم = كل IncomingRows ما عدا TRANSFER_FROM (الإحالة من الدائرة تُخصم)
            // هذا الحساب مبسّط — المنطق الدقيق في الواجهة
            var inc = incomings.Where(i => i.CaseYearBandId == bandId).Sum(i => i.Count);

            // المحكوم فيه = مجموع أحكام القضاة
            var judged = judgeJudgments.Where(j => j.CaseYearBandId == bandId).Sum(j => j.Count);

            var close = Math.Max(0, open + inc - judged);

            context.ClosingBalances.Add(new ClosingBalance
            {
                StatisticsHeaderId = headerId,
                CaseTypeId = caseTypeId,
                CaseYearBandId = bandId,
                Count = close,
                CarriedForward = false
            });
        }

        header.Status = "Approved";
        header.ApprovedByUserId = currentUserId;
        header.ApprovedAt = DateTime.UtcNow;

        context.AuditEntries.Add(new AuditEntry
        {
            StatisticsHeaderId = headerId,
            UserId = currentUserId,
            Action = "اعتماد نهائي",
            Comment = "تم اعتماد الإحصائية رسمياً وتوليد الأرصدة الختامية",
            OccurredAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task ReopenAsync(long headerId, byte[] rowVersion, string reason, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var header = await context.StatisticsHeaders.FindAsync(headerId)
            ?? throw new KeyNotFoundException($"الإحصائية رقم {headerId} غير موجودة.");

        // إزالة الأرصدة الختامية المولّدة
        var closings = await context.ClosingBalances
            .Where(c => c.StatisticsHeaderId == headerId)
            .ToListAsync();
        context.ClosingBalances.RemoveRange(closings);

        header.Status = "Reopened";
        header.RejectionReason = reason;

        context.AuditEntries.Add(new AuditEntry
        {
            StatisticsHeaderId = headerId,
            UserId = currentUserId,
            Action = "إعادة فتح الإحصائية",
            Comment = reason,
            OccurredAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task<bool> CheckPeriodOpenForCourtAsync(JudicialDbContext context, long periodId, long courtUnitId)
    {
        if (courtUnitId <= 0)
        {
            var p = await context.StatisticalPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == periodId);
            return p?.IsOpenForEntry ?? true;
        }

        // 1. فحص المحكمة المباشرة
        var status = await context.CourtPeriodStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CourtUnitId == courtUnitId && s.StatisticalPeriodId == periodId);
        if (status != null) return status.IsOpenForEntry;

        // 2. فحص المحكمة الأب (المحكمة الابتدائية)
        var court = await context.CourtUnits.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courtUnitId);
        if (court?.ParentCourtUnitId != null)
        {
            var parentStatus = await context.CourtPeriodStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CourtUnitId == court.ParentCourtUnitId.Value && s.StatisticalPeriodId == periodId);
            if (parentStatus != null) return parentStatus.IsOpenForEntry;
        }

        // 3. الرجوع للحالة العامة للفترة
        var period = await context.StatisticalPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId);
        return period?.IsOpenForEntry ?? true;
    }

    private static async Task ValidateUserSessionAuthorizationAsync(
        JudicialDbContext context,
        long sessionVersionId,
        long currentUserId,
        string actionDescription = "إدخال أو تعديل إحصائية هذه الجلسة")
    {
        if (currentUserId <= 0) return;

        var sv = await context.SessionVersions
            .AsNoTracking()
            .Include(x => x.Session)
            .Include(x => x.CircuitVersion).ThenInclude(cv => cv.Circuit).ThenInclude(c => c.CourtUnit)
            .FirstOrDefaultAsync(x => x.Id == sessionVersionId);

        if (sv == null) return;

        var scope = await UserScopeHelper.ResolveUserScopeAsync(context, currentUserId);
        long courtUnitId = sv.CircuitVersion?.Circuit?.CourtUnitId ?? sv.Session?.Circuit?.CourtUnitId ?? 0;
        long? sessionPrimaryCourtId = sv.CircuitVersion?.Circuit?.CourtUnit?.ParentCourtUnitId ?? sv.Session?.Circuit?.CourtUnit?.ParentCourtUnitId;

        if (!scope.IsSessionAuthorized(sv.SessionId, courtUnitId, sessionPrimaryCourtId))
        {
            throw new UnauthorizedAccessException($"غير مصرح لك بـ {actionDescription}، حيث أن الجلسة غير مسندة لنطاق صلاحيات حسابك.");
        }
    }
}
