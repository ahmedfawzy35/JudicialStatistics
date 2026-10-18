using JudicialStatistics.Application.Reports;
using JudicialStatistics.Domain.Reports;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace JudicialStatistics.Infrastructure.Services;

public class ReportsService : IReportsService
{
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public ReportsService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PeriodReportSummaryDto> GetPeriodReportAsync(
        long? courtUnitId,
        long? statisticalPeriodId,
        long? circuitId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var period = statisticalPeriodId.HasValue
            ? await context.StatisticalPeriods.Include(p => p.JudicialYear).FirstOrDefaultAsync(p => p.Id == statisticalPeriodId.Value)
            : await context.StatisticalPeriods.Include(p => p.JudicialYear).OrderByDescending(p => p.MonthStartDate).FirstOrDefaultAsync();

        var periodName = period?.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG")) ?? "كافة الفترات";
        var yearName = period?.JudicialYear?.NameAr ?? string.Empty;

        var court = courtUnitId.HasValue
            ? await context.CourtUnits.FindAsync(courtUnitId.Value)
            : await context.CourtUnits.FirstOrDefaultAsync();

        var courtName = court?.NameAr ?? "محكمة بني سويف الابتدائية";

        var headerQuery = context.StatisticsHeaders.AsQueryable();

        if (period is not null)
        {
            headerQuery = headerQuery.Where(h => h.StatisticalPeriodId == period.Id);
        }

        if (circuitId.HasValue)
        {
            headerQuery = headerQuery.Where(h => h.SessionVersion.CircuitVersion.CircuitId == circuitId.Value);
        }
        else if (courtUnitId.HasValue)
        {
            headerQuery = headerQuery.Where(h => h.SessionVersion.CircuitVersion.Circuit.CourtUnitId == courtUnitId.Value);
        }

        var headerIds = await headerQuery.Select(h => h.Id).ToListAsync();
        var sessionVersionIds = await headerQuery.Select(h => h.SessionVersionId).Distinct().ToListAsync();

        var openings = await context.OpeningBalances
            .Include(o => o.CaseType)
            .Where(o => sessionVersionIds.Contains(o.SessionVersionId))
            .ToListAsync();

        var incomings = await context.IncomingRows
            .Include(i => i.CaseType)
            .Where(i => headerIds.Contains(i.StatisticsHeaderId))
            .ToListAsync();

        var productions = await context.ProductionRows
            .Include(p => p.CaseType)
            .Include(p => p.ProductionItemType)
            .Where(p => headerIds.Contains(p.StatisticsHeaderId))
            .ToListAsync();

        var caseTypeIds = openings.Select(o => o.CaseTypeId)
            .Union(incomings.Select(i => i.CaseTypeId))
            .Union(productions.Select(p => p.CaseTypeId))
            .Distinct()
            .ToList();

        var caseTypes = await context.CaseTypes
            .Where(ct => caseTypeIds.Contains(ct.Id))
            .ToDictionaryAsync(ct => ct.Id, ct => ct.NameAr);

        var summaries = new List<CaseTypeSummaryDto>();
        foreach (var cId in caseTypeIds)
        {
            var open = openings.Where(o => o.CaseTypeId == cId).Sum(o => o.Count);
            var inc = incomings.Where(i => i.CaseTypeId == cId).Sum(i => i.Count);
            var totalAvailable = open + inc;
            var jud = productions.Where(p => p.CaseTypeId == cId && p.ProductionItemType.ItemKind == "Judgment").Sum(p => p.Count);
            var def = productions.Where(p => p.CaseTypeId == cId && p.ProductionItemType.ItemKind == "Deferral").Sum(p => p.Count);
            var close = Math.Max(0, totalAvailable - jud);
            var rate = totalAvailable > 0 ? Math.Round((double)jud / totalAvailable * 100, 1) : 0;

            summaries.Add(new CaseTypeSummaryDto(
                cId,
                caseTypes.GetValueOrDefault(cId, $"نوع {cId}"),
                open,
                inc,
                totalAvailable,
                jud,
                def,
                close,
                rate
            ));
        }

        var totalOpen = summaries.Sum(s => s.OpeningCount);
        var totalInc = summaries.Sum(s => s.IncomingCount);
        var totalAvail = totalOpen + totalInc;
        var totalJud = summaries.Sum(s => s.JudgmentCount);
        var totalDef = summaries.Sum(s => s.DeferredCount);
        var totalClose = Math.Max(0, totalAvail - totalJud);
        var overallRate = totalAvail > 0 ? Math.Round((double)totalJud / totalAvail * 100, 1) : 0;

        return new PeriodReportSummaryDto(
            courtName,
            yearName,
            periodName,
            totalOpen,
            totalInc,
            totalAvail,
            totalJud,
            totalDef,
            totalClose,
            overallRate,
            summaries
        );
    }

    public async Task<AnnualReportSummaryDto> GetAnnualReportAsync(long judicialYearId, long? courtUnitId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var year = await context.JudicialYears
            .Include(y => y.ReportingScope)
            .FirstOrDefaultAsync(y => y.Id == judicialYearId)
            ?? throw new KeyNotFoundException($"العام القضائي رقم {judicialYearId} غير موجود.");

        var headersQuery = context.StatisticsHeaders
            .Where(h => h.StatisticalPeriod.JudicialYearId == judicialYearId);

        if (courtUnitId.HasValue)
        {
            headersQuery = headersQuery.Where(h => h.SessionVersion.CircuitVersion.Circuit.CourtUnitId == courtUnitId.Value);
        }

        var headerIds = await headersQuery.Select(h => h.Id).ToListAsync();
        var sessionVersionIds = await headersQuery.Select(h => h.SessionVersionId).Distinct().ToListAsync();

        var openings = await context.OpeningBalances
            .Include(o => o.CaseType)
            .Where(o => sessionVersionIds.Contains(o.SessionVersionId))
            .ToListAsync();

        var incomings = await context.IncomingRows
            .Include(i => i.CaseType)
            .Where(i => headerIds.Contains(i.StatisticsHeaderId))
            .ToListAsync();

        var productions = await context.ProductionRows
            .Include(p => p.CaseType)
            .Include(p => p.ProductionItemType)
            .Where(p => headerIds.Contains(p.StatisticsHeaderId))
            .ToListAsync();

        var caseTypeIds = openings.Select(o => o.CaseTypeId)
            .Union(incomings.Select(i => i.CaseTypeId))
            .Union(productions.Select(p => p.CaseTypeId))
            .Distinct()
            .ToList();

        var caseTypes = await context.CaseTypes
            .Where(ct => caseTypeIds.Contains(ct.Id))
            .ToDictionaryAsync(ct => ct.Id, ct => ct.NameAr);

        var summaries = new List<CaseTypeSummaryDto>();
        foreach (var cId in caseTypeIds)
        {
            var open = openings.Where(o => o.CaseTypeId == cId).Sum(o => o.Count);
            var inc = incomings.Where(i => i.CaseTypeId == cId).Sum(i => i.Count);
            var totalAvailable = open + inc;
            var jud = productions.Where(p => p.CaseTypeId == cId && p.ProductionItemType.ItemKind == "Judgment").Sum(p => p.Count);
            var def = productions.Where(p => p.CaseTypeId == cId && p.ProductionItemType.ItemKind == "Deferral").Sum(p => p.Count);
            var close = Math.Max(0, totalAvailable - jud);
            var rate = totalAvailable > 0 ? Math.Round((double)jud / totalAvailable * 100, 1) : 0;

            summaries.Add(new CaseTypeSummaryDto(
                cId,
                caseTypes.GetValueOrDefault(cId, $"نوع {cId}"),
                open,
                inc,
                totalAvailable,
                jud,
                def,
                close,
                rate
            ));
        }

        var judgesPerf = await GetJudgesPerformanceAsync(judicialYearId);

        var totalJudgments = summaries.Sum(s => s.JudgmentCount);
        var totalAvail = summaries.Sum(s => s.TotalAvailable);
        var rateOverall = totalAvail > 0 ? Math.Round((double)totalJudgments / totalAvail * 100, 1) : 0;

        return new AnnualReportSummaryDto(
            year.NameAr,
            year.ReportingScope?.NameAr ?? "النطاق القضائي",
            headerIds.Count,
            totalJudgments,
            rateOverall,
            summaries,
            judgesPerf
        );
    }

    public async Task<List<JudgePerformanceDto>> GetJudgesPerformanceAsync(
        long judicialYearId,
        long? statisticalPeriodId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var judges = await context.Judges
            .Where(j => j.IsActive)
            .OrderBy(j => j.FullNameAr)
            .ToListAsync();

        var assignments = await context.CircuitJudgeAssignments
            .Include(a => a.JudicialRole)
            .Include(a => a.CircuitVersion)
                .ThenInclude(cv => cv.Circuit)
            .Where(a => a.CircuitVersion.Circuit.JudicialYearId == judicialYearId)
            .ToListAsync();

        var allocQuery = context.JudgmentAllocations
            .Include(a => a.ProductionRow)
                .ThenInclude(p => p.StatisticsHeader)
            .Where(a => a.ProductionRow.StatisticsHeader.StatisticalPeriod.JudicialYearId == judicialYearId);

        if (statisticalPeriodId.HasValue)
        {
            allocQuery = allocQuery.Where(a => a.ProductionRow.StatisticsHeader.StatisticalPeriodId == statisticalPeriodId.Value);
        }

        var allocations = await allocQuery.ToListAsync();

        var result = new List<JudgePerformanceDto>();

        foreach (var j in judges)
        {
            var judgeAssignments = assignments.Where(a => a.JudgeId == j.Id).ToList();
            var primaryRole = judgeAssignments.FirstOrDefault()?.JudicialRole.NameAr ?? "قاضٍ";
            var circuitsCount = judgeAssignments.Select(a => a.CircuitVersion.CircuitId).Distinct().Count();
            var allocsCount = allocations.Where(a => a.JudgeId == j.Id).Sum(a => a.Count);

            result.Add(new JudgePerformanceDto(
                j.Id,
                j.Code,
                j.FullNameAr,
                primaryRole,
                allocsCount,
                allocsCount,
                circuitsCount
            ));
        }

        return result.OrderByDescending(r => r.TotalJudgments).ToList();
    }

    // ======================== Circuit Ranking Implementation ========================

    private static readonly HashSet<string> CivilFinalKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "FORMAL_REJECT", "FORMAL_INCOMP", "FORMAL_INCOMP_LAW", "FORMAL_INCOMP_VALUE", "FORMAL_INCOMP_LOCAL", "FORMAL_INCOMP_TYPE",
        "FORMAL_LAPSE_RIGHT", "FORMAL_LAPSE_LITIG", "FORMAL_WAIVE", "FORMAL_NULL", "FORMAL_DEEMED_NULL", "FORMAL_OTHER",
        "SUBST_ACCEPT", "SUBST_REJECT_STATE", "SUBST_REJECT", "SUBST_RES_JUDICATA",
        "SETTLEMENT",
        "FAM_DIVORCE", "FAM_ALIMONY", "FAM_CUSTODY", "FAM_IMPRISON", "FAM_WAGES"
    };

    private static readonly HashSet<string> CivilEvidenceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "INTERLOC", "EVID_EXPERT", "EVID_RE_EXPERT", "EVID_INVEST", "EVID_CROSS", "EVID_OATH", "EVID_PENALTY", "EVID_CONSENSUAL", "EVID_CONDITIONAL", "FAM_ARBITRATORS"
    };

    private static readonly HashSet<string> CriminalFinalKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "CRIM_CAT_SUBST", "CRIM_CAT_ORDER", "CRIM_CAT_PROC",
        "CRIM_IMPRIS", "CRIM_FINE", "CRIM_ACQUIT", "CRIM_RES_JUDICATA", "CRIM_TIME_BAR",
        "CRIM_ORDER", "CRIM_CONCIL", "CRIM_DEATH", "CRIM_OPP_NULL", "CRIM_OPP_INADMISS", "CRIM_NOT_ACCEPT", "CRIM_APPEAL_LAPSE", "CRIM_ORDER_FINAL", "CRIM_INCOMP"
    };

    private static readonly HashSet<string> CriminalEvidenceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "CRIM_CAT_PREP", "CRIM_EXPERT", "CRIM_RE_EXPERT"
    };

    private static readonly HashSet<string> CriminalStayKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "CRIM_CAT_STAY", "CRIM_STAY_CONCIL", "CRIM_STAY_SUSP", "STAY_PENAL", "STAY_COND", "STAY_AGREE"
    };

    public async Task<CircuitRankingReportDto> GetCircuitRankingReportAsync(CircuitRankingFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.Id == filter.JudicialYearId)
            ?? throw new KeyNotFoundException($"العام القضائي {filter.JudicialYearId} غير موجود.");

        var periods = await context.StatisticalPeriods
            .Where(p => filter.StatisticalPeriodIds.Contains(p.Id))
            .OrderBy(p => p.MonthStartDate)
            .ToListAsync();

        var arCulture = new System.Globalization.CultureInfo("ar-EG");
        string periodRangeNameAr;
        if (periods.Count == 1)
        {
            periodRangeNameAr = $"عن شهر {periods[0].MonthStartDate.ToString("MMMM yyyy", arCulture)}";
        }
        else if (periods.Count > 1)
        {
            var firstP = periods.First();
            var lastP = periods.Last();
            periodRangeNameAr = $"عن الفترة من {firstP.MonthStartDate.ToString("MMMM yyyy", arCulture)} إلى {lastP.MonthStartDate.ToString("MMMM yyyy", arCulture)}";
        }
        else
        {
            periodRangeNameAr = "كافة الفترات";
        }

        var court = filter.CourtUnitId.HasValue
            ? await context.CourtUnits.FindAsync(filter.CourtUnitId.Value)
            : await context.CourtUnits.FirstOrDefaultAsync(c => c.ParentCourtUnitId == null);

        var courtName = court?.NameAr ?? "محكمة بني سويف الابتدائية";

        string reportTitleAr;
        string targetCircuitKind;
        bool isCriminal = false;
        bool isSingleJudgePartial = false;

        switch (filter.ReportType)
        {
            case "CivilFull":
                reportTitleAr = "كشف ترتيب دوائر المدني الكلي تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "CivilFull";
                break;
            case "PartialCivil":
                reportTitleAr = "كشف ترتيب دوائر المدني الجزئي وصحة التوقيع تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "Partial";
                isSingleJudgePartial = true;
                break;
            case "Family":
                reportTitleAr = "كشف ترتيب دوائر محكمة الأسرة تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "Family";
                break;
            case "Labor":
                reportTitleAr = "كشف ترتيب الدوائر العمالية (عمال وتأمينات) تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "Labor";
                break;
            case "LaborMisdemeanor":
                reportTitleAr = "كشف ترتيب دوائر جنح العمال تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "Labor";
                isCriminal = true;
                break;
            case "PartialMisdemeanor":
                reportTitleAr = "كشف ترتيب دوائر الجنح الجزئي المتنوع تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "Partial";
                isCriminal = true;
                isSingleJudgePartial = true;
                break;
            case "AppealMisdemeanor":
                reportTitleAr = "كشف ترتيب دوائر الجنح المستأنفة والأحداث تنازلياً وفقاً للأحكام القطعية";
                targetCircuitKind = "CriminalAppeal";
                isCriminal = true;
                break;
            default:
                reportTitleAr = "كشف ترتيب الدوائر القضائية";
                targetCircuitKind = "CivilFull";
                break;
        }

        var circuitsQuery = context.Circuits
            .Include(c => c.CourtUnit)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Versions)
            .Where(c => c.JudicialYearId == filter.JudicialYearId && !c.IsClosed);

        if (filter.CourtUnitId.HasValue)
        {
            var targetUnit = await context.CourtUnits.FindAsync(filter.CourtUnitId.Value);
            if (targetUnit != null)
            {
                var childUnitIds = await context.CourtUnits
                    .Where(cu => cu.ParentCourtUnitId == targetUnit.Id || cu.Id == targetUnit.Id)
                    .Select(cu => cu.Id)
                    .ToListAsync();
                circuitsQuery = circuitsQuery.Where(c => childUnitIds.Contains(c.CourtUnitId));
            }
        }

        var allCircuits = await circuitsQuery.ToListAsync();

        var matchedCircuits = allCircuits
            .Where(c => c.Versions.Any(v => v.CircuitKind.Equals(targetCircuitKind, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        List<string> judgeColumnsHeaders;
        if (isSingleJudgePartial)
        {
            judgeColumnsHeaders = ["السيد القاضي الفردي (أ)"];
        }
        else
        {
            judgeColumnsHeaders = ["أ (رئيس الدائرة)", "ب (عضو يمين)", "ج (عضو يسار)", "د (عضو رابع)", "هـ (عضو خامس)"];
        }

        var periodIds = filter.StatisticalPeriodIds;
        var rows = new List<CircuitRankingRowDto>();

        foreach (var circuit in matchedCircuits)
        {
            var cv = circuit.Versions.FirstOrDefault(v => v.CircuitKind.Equals(targetCircuitKind, StringComparison.OrdinalIgnoreCase))
                     ?? circuit.Versions.FirstOrDefault();
            if (cv == null) continue;

            var sessions = circuit.Sessions.Where(s => !s.IsClosed).ToList();
            if (filter.ReportType == "PartialCivil")
            {
                sessions = sessions.Where(s => s.Code.Contains("-CIV-", StringComparison.OrdinalIgnoreCase)
                                            || s.NameAr.Contains("مدني")
                                            || s.NameAr.Contains("صحة توقيع")
                                            || s.NameAr.Contains("مستعجل")).ToList();
            }
            else if (filter.ReportType == "PartialMisdemeanor")
            {
                sessions = sessions.Where(s => !(s.Code.Contains("-CIV-", StringComparison.OrdinalIgnoreCase)
                                              || s.NameAr.Contains("مدني")
                                              || s.NameAr.Contains("صحة توقيع")
                                              || s.NameAr.Contains("مستعجل"))).ToList();
            }
            else if (filter.ReportType == "Labor")
            {
                sessions = sessions.Where(s => !s.NameAr.Contains("جنح")).ToList();
            }
            else if (filter.ReportType == "LaborMisdemeanor")
            {
                sessions = sessions.Where(s => s.NameAr.Contains("جنح")).ToList();
            }

            var sessionVersionIds = sessions
                .SelectMany(s => s.Versions)
                .Where(sv => sv.CircuitVersionId == cv.Id)
                .Select(sv => sv.Id)
                .ToList();

            var headers = await context.StatisticsHeaders
                .Where(h => sessionVersionIds.Contains(h.SessionVersionId) && periodIds.Contains(h.StatisticalPeriodId))
                .Select(h => h.Id)
                .ToListAsync();

            int totalIncoming = 0;
            int suspendedCount = 0;
            int finalJudgmentsCount = 0;
            int evidenceJudgmentsCount = 0;
            int otherJudgmentsCount = 0;
            int strikeoutCount = 0;
            int reportPostponedCount = 0;
            int otherPostponedCount = 0;
            int totalPostponedCount = 0;

            var judgeFinals = new Dictionary<long, int>();

            if (headers.Count > 0)
            {
                var openings = await context.OpeningBalances
                    .Where(o => sessionVersionIds.Contains(o.SessionVersionId))
                    .ToListAsync();

                var incomings = await context.IncomingRows
                    .Include(i => i.IncomingType)
                    .Where(i => headers.Contains(i.StatisticsHeaderId))
                    .ToListAsync();

                var judgeJudgments = await context.JudgeJudgments
                    .Where(j => headers.Contains(j.StatisticsHeaderId))
                    .ToListAsync();

                var prodRows = await context.ProductionRows
                    .Include(p => p.ProductionItemType)
                    .Where(p => headers.Contains(p.StatisticsHeaderId))
                    .ToListAsync();

                var postponements = await context.Postponements
                    .Where(p => headers.Contains(p.StatisticsHeaderId))
                    .ToListAsync();

                // Compute Incoming
                var openSum = openings.Sum(o => o.Count);
                var transFrom = incomings.Where(i => i.IncomingType?.Code == "TRANSFER_FROM").Sum(i => i.Count);
                var incAdd = incomings.Where(i => i.IncomingType?.Code != "TRANSFER_FROM" && i.IncomingType?.Code != "PREV").Sum(i => i.Count);
                totalIncoming = Math.Max(0, openSum + incAdd - transFrom);
                if (totalIncoming == 0 && incomings.Count > 0)
                {
                    totalIncoming = Math.Max(0, incomings.Sum(i => i.Count));
                }

                // Compute Judgments
                if (judgeJudgments.Count > 0)
                {
                    foreach (var jj in judgeJudgments)
                    {
                        var kind = jj.JudgmentKind;
                        if (isCriminal)
                        {
                            if (CriminalFinalKinds.Contains(kind))
                            {
                                finalJudgmentsCount += jj.Count;
                                judgeFinals[jj.JudgeId] = judgeFinals.GetValueOrDefault(jj.JudgeId) + jj.Count;
                            }
                            else if (CriminalEvidenceKinds.Contains(kind))
                            {
                                evidenceJudgmentsCount += jj.Count;
                            }
                            else if (CriminalStayKinds.Contains(kind))
                            {
                                suspendedCount += jj.Count;
                            }
                            else
                            {
                                otherJudgmentsCount += jj.Count;
                            }
                        }
                        else
                        {
                            if (CivilFinalKinds.Contains(kind))
                            {
                                finalJudgmentsCount += jj.Count;
                                judgeFinals[jj.JudgeId] = judgeFinals.GetValueOrDefault(jj.JudgeId) + jj.Count;
                            }
                            else if (CivilEvidenceKinds.Contains(kind))
                            {
                                evidenceJudgmentsCount += jj.Count;
                            }
                            else if (kind.StartsWith("STAY_", StringComparison.OrdinalIgnoreCase))
                            {
                                suspendedCount += jj.Count;
                            }
                            else if (kind.Equals("STRIKEOUT", StringComparison.OrdinalIgnoreCase))
                            {
                                strikeoutCount += jj.Count;
                            }
                            else
                            {
                                otherJudgmentsCount += jj.Count;
                            }
                        }
                    }
                }
                else if (prodRows.Count > 0)
                {
                    foreach (var pr in prodRows)
                    {
                        var code = pr.ProductionItemType?.Code ?? "";
                        if (isCriminal)
                        {
                            if (CriminalFinalKinds.Contains(code) || pr.ProductionItemType?.ItemKind == "Judgment")
                            {
                                finalJudgmentsCount += pr.Count;
                            }
                            else if (CriminalEvidenceKinds.Contains(code))
                            {
                                evidenceJudgmentsCount += pr.Count;
                            }
                            else if (CriminalStayKinds.Contains(code))
                            {
                                suspendedCount += pr.Count;
                            }
                            else
                            {
                                otherJudgmentsCount += pr.Count;
                            }
                        }
                        else
                        {
                            if (CivilFinalKinds.Contains(code) || pr.ProductionItemType?.ItemKind == "Judgment")
                            {
                                finalJudgmentsCount += pr.Count;
                            }
                            else if (CivilEvidenceKinds.Contains(code))
                            {
                                evidenceJudgmentsCount += pr.Count;
                            }
                            else
                            {
                                otherJudgmentsCount += pr.Count;
                            }
                        }
                    }
                }

                // Compute Postponements
                foreach (var pos in postponements)
                {
                    totalPostponedCount += pos.Count;
                    if (pos.PostponementKind is "DEF_REPORT" or "FOR_REPORT" or "CRIM_DEF_REP")
                    {
                        reportPostponedCount += pos.Count;
                    }
                    else
                    {
                        otherPostponedCount += pos.Count;
                    }
                }
            }

            int totalAllJudgmentsCount = finalJudgmentsCount + evidenceJudgmentsCount + otherJudgmentsCount;

            var assignedJudges = cv.JudgeAssignments
                .OrderBy(ja => ja.DisplayOrder)
                .ToList();

            var presidentAssignment = assignedJudges.FirstOrDefault(ja => ja.JudicialRole?.Code == "PRESIDENT" || ja.DisplayOrder == 1)
                                      ?? assignedJudges.FirstOrDefault();
            var presidentName = presidentAssignment?.Judge?.FullNameAr ?? "غير مسجل";
            var presidentGrade = presidentAssignment?.JudicialRole?.NameAr ?? "رئيس الدائرة";

            var judgesAllocations = new List<CircuitJudgeJudgmentsDto>();

            if (isSingleJudgePartial)
            {
                var judgeId = presidentAssignment?.JudgeId ?? 0;
                var jName = presidentAssignment?.Judge?.FullNameAr ?? presidentName;
                var jRole = presidentAssignment?.JudicialRole?.NameAr ?? "قاضٍ فردي";
                judgesAllocations.Add(new CircuitJudgeJudgmentsDto(judgeId, jName, jRole, 1, finalJudgmentsCount));
            }
            else
            {
                short order = 1;
                foreach (var ja in assignedJudges)
                {
                    var count = judgeFinals.GetValueOrDefault(ja.JudgeId, 0);
                    judgesAllocations.Add(new CircuitJudgeJudgmentsDto(
                        ja.JudgeId,
                        ja.Judge?.FullNameAr ?? $"قاضٍ {order}",
                        ja.JudicialRole?.NameAr ?? $"عضو {order}",
                        ja.DisplayOrder > 0 ? ja.DisplayOrder : order,
                        count
                    ));
                    order++;
                }

                if (judgesAllocations.Count == 0)
                {
                    judgesAllocations.Add(new CircuitJudgeJudgmentsDto(0, presidentName, "رئيس الدائرة", 1, finalJudgmentsCount));
                }
                else
                {
                    var currentAllocSum = judgesAllocations.Sum(j => j.FinalJudgmentsCount);
                    if (currentAllocSum < finalJudgmentsCount)
                    {
                        var diff = finalJudgmentsCount - currentAllocSum;
                        var first = judgesAllocations[0];
                        judgesAllocations[0] = first with { FinalJudgmentsCount = first.FinalJudgmentsCount + diff };
                    }
                }
            }

            int totalJudgesAllocations = judgesAllocations.Sum(j => j.FinalJudgmentsCount);

            int effectiveIncoming;
            if (filter.ReportType == "PartialMisdemeanor" && filter.DeductSuspendedInMisdemeanors)
            {
                effectiveIncoming = Math.Max(0, totalIncoming - suspendedCount);
            }
            else
            {
                effectiveIncoming = totalIncoming;
            }

            double completionRate = effectiveIncoming > 0
                ? Math.Round(((double)finalJudgmentsCount / effectiveIncoming) * 100, 2)
                : 0.0;

            double totalJudgmentsRate = effectiveIncoming > 0
                ? Math.Round(((double)totalAllJudgmentsCount / effectiveIncoming) * 100, 2)
                : 0.0;

            rows.Add(new CircuitRankingRowDto(
                0,
                circuit.Id,
                circuit.Code,
                circuit.NameAr,
                circuit.CourtUnit?.NameAr ?? courtName,
                presidentName,
                presidentGrade,
                totalIncoming,
                suspendedCount,
                effectiveIncoming,
                finalJudgmentsCount,
                evidenceJudgmentsCount,
                otherJudgmentsCount,
                totalAllJudgmentsCount,
                strikeoutCount,
                reportPostponedCount,
                otherPostponedCount,
                totalPostponedCount,
                completionRate,
                totalJudgmentsRate,
                judgesAllocations,
                totalJudgesAllocations
            ));
        }

        // Sort descending: by CompletionRate, then by FinalJudgmentsCount, then by TotalIncoming
        rows = rows
            .OrderByDescending(r => r.CompletionRate)
            .ThenByDescending(r => r.FinalJudgmentsCount)
            .ThenByDescending(r => r.TotalIncoming)
            .Select((r, idx) => r with { Rank = idx + 1 })
            .ToList();

        // Calculate Totals Row
        int totalIncomingSum = rows.Sum(r => r.TotalIncoming);
        int totalSuspendedSum = rows.Sum(r => r.SuspendedCount);
        int totalEffectiveIncomingSum = rows.Sum(r => r.EffectiveIncoming);
        int totalFinalSum = rows.Sum(r => r.FinalJudgmentsCount);
        int totalEvidenceSum = rows.Sum(r => r.EvidenceJudgmentsCount);
        int totalOtherSum = rows.Sum(r => r.OtherJudgmentsCount);
        int totalAllJudgSum = rows.Sum(r => r.TotalAllJudgmentsCount);
        int totalStrikeoutSum = rows.Sum(r => r.StrikeoutCount);
        int totalReportPostponedSum = rows.Sum(r => r.ReportPostponedCount);
        int totalOtherPostponedSum = rows.Sum(r => r.OtherPostponedCount);
        int totalPostponedSum = rows.Sum(r => r.TotalPostponedCount);

        double totalWeightedRate = totalEffectiveIncomingSum > 0
            ? Math.Round(((double)totalFinalSum / totalEffectiveIncomingSum) * 100, 2)
            : 0.0;

        double totalWeightedAllRate = totalEffectiveIncomingSum > 0
            ? Math.Round(((double)totalAllJudgSum / totalEffectiveIncomingSum) * 100, 2)
            : 0.0;

        var maxJudgeCount = rows.Count > 0 ? rows.Max(r => r.JudgesAllocations.Count) : 1;
        var totalsJudgesAllocations = new List<CircuitJudgeJudgmentsDto>();
        for (int i = 0; i < maxJudgeCount; i++)
        {
            int slotSum = rows.Sum(r => i < r.JudgesAllocations.Count ? r.JudgesAllocations[i].FinalJudgmentsCount : 0);
            string colHeader = i < judgeColumnsHeaders.Count ? judgeColumnsHeaders[i] : $"عضو {i + 1}";
            totalsJudgesAllocations.Add(new CircuitJudgeJudgmentsDto(0, "الإجمالي", colHeader, (short)(i + 1), slotSum));
        }

        var totalsRow = new CircuitRankingRowDto(
            0,
            0,
            "-",
            "الإجمالي العام",
            courtName,
            "-",
            "-",
            totalIncomingSum,
            totalSuspendedSum,
            totalEffectiveIncomingSum,
            totalFinalSum,
            totalEvidenceSum,
            totalOtherSum,
            totalAllJudgSum,
            totalStrikeoutSum,
            totalReportPostponedSum,
            totalOtherPostponedSum,
            totalPostponedSum,
            totalWeightedRate,
            totalWeightedAllRate,
            totalsJudgesAllocations,
            totalsJudgesAllocations.Sum(j => j.FinalJudgmentsCount)
        );

        return new CircuitRankingReportDto(
            reportTitleAr,
            year.NameAr,
            periodRangeNameAr,
            courtName,
            filter.ReportType,
            filter.DeductSuspendedInMisdemeanors,
            judgeColumnsHeaders,
            rows,
            totalsRow
        );
    }

    public async Task<byte[]> ExportCircuitRankingToExcelAsync(CircuitRankingFilter filter)
    {
        var report = await GetCircuitRankingReportAsync(filter);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("كشف الترتيب");
        ws.RightToLeft = true;
        ws.ShowGridLines = true;

        // Title and Header metadata
        ws.Cell("B2").Value = report.CourtNameAr;
        ws.Cell("B2").Style.Font.Bold = true;
        ws.Cell("B2").Style.Font.FontSize = 14;
        ws.Cell("B2").Style.Font.FontName = "Arial";

        ws.Cell("B3").Value = "قسم الإحصاء القضائي";
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontSize = 11;
        ws.Cell("B3").Style.Font.FontName = "Arial";

        ws.Cell("B4").Value = report.ReportTitleAr;
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.FontSize = 13;
        ws.Cell("B4").Style.Font.FontName = "Arial";

        ws.Cell("B5").Value = $"{report.PeriodRangeNameAr} — {report.JudicialYearNameAr}";
        ws.Cell("B5").Style.Font.Bold = true;
        ws.Cell("B5").Style.Font.FontSize = 11;
        ws.Cell("B5").Style.Font.FontColor = XLColor.FromHtml("#1A4A6B");
        ws.Cell("B5").Style.Font.FontName = "Arial";

        int headerRow1 = 7;
        int headerRow2 = 8;
        int currentCol = 1;

        // Define Columns
        int colRank = currentCol++;
        int colCode = currentCol++;
        int colCircuit = currentCol++;
        int colPresident = currentCol++;
        int colCourt = currentCol++;
        int colIncoming = currentCol++;

        int? colSuspended = null;
        int? colEffectiveIncoming = null;
        if (report.ReportType == "PartialMisdemeanor")
        {
            colSuspended = currentCol++;
            if (report.DeductSuspendedInMisdemeanors)
            {
                colEffectiveIncoming = currentCol++;
            }
        }

        int colFinal = currentCol++;
        int colEvidence = currentCol++;
        int colOtherJudg = currentCol++;
        int colTotalJudg = currentCol++;
        int colPostponedRep = currentCol++;
        int colPostponedTot = currentCol++;
        int colRate = currentCol++;

        var judgeCols = new List<int>();
        for (int i = 0; i < report.JudgeColumnsHeaders.Count; i++)
        {
            judgeCols.Add(currentCol++);
        }
        int colTotalJudges = currentCol++;

        int totalColumns = colTotalJudges;

        // Row 7: Category Group Headers
        // 1. بيانات الدائرة
        ws.Range(headerRow1, colRank, headerRow1, colCourt).Merge();
        ws.Cell(headerRow1, colRank).Value = "بيانات الدائرة والتشكيل القضائي";
        ws.Range(headerRow1, colRank, headerRow1, colCourt).Style.Fill.BackgroundColor = XLColor.FromHtml("#1A4A6B");
        ws.Range(headerRow1, colRank, headerRow1, colCourt).Style.Font.FontColor = XLColor.White;
        ws.Range(headerRow1, colRank, headerRow1, colCourt).Style.Font.Bold = true;
        ws.Range(headerRow1, colRank, headerRow1, colCourt).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 2. المقدم
        int incEndCol = colEffectiveIncoming ?? colSuspended ?? colIncoming;
        ws.Range(headerRow1, colIncoming, headerRow1, incEndCol).Merge();
        ws.Cell(headerRow1, colIncoming).Value = "المقدم";
        ws.Range(headerRow1, colIncoming, headerRow1, incEndCol).Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
        ws.Range(headerRow1, colIncoming, headerRow1, incEndCol).Style.Font.FontColor = XLColor.White;
        ws.Range(headerRow1, colIncoming, headerRow1, incEndCol).Style.Font.Bold = true;
        ws.Range(headerRow1, colIncoming, headerRow1, incEndCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 3. الأحكام الصادرة
        ws.Range(headerRow1, colFinal, headerRow1, colTotalJudg).Merge();
        ws.Cell(headerRow1, colFinal).Value = "الأحكام الصادرة";
        ws.Range(headerRow1, colFinal, headerRow1, colTotalJudg).Style.Fill.BackgroundColor = XLColor.FromHtml("#375623");
        ws.Range(headerRow1, colFinal, headerRow1, colTotalJudg).Style.Font.FontColor = XLColor.White;
        ws.Range(headerRow1, colFinal, headerRow1, colTotalJudg).Style.Font.Bold = true;
        ws.Range(headerRow1, colFinal, headerRow1, colTotalJudg).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 4. المؤجلات
        ws.Range(headerRow1, colPostponedRep, headerRow1, colPostponedTot).Merge();
        ws.Cell(headerRow1, colPostponedRep).Value = "المؤجلات";
        ws.Range(headerRow1, colPostponedRep, headerRow1, colPostponedTot).Style.Fill.BackgroundColor = XLColor.FromHtml("#7030A0");
        ws.Range(headerRow1, colPostponedRep, headerRow1, colPostponedTot).Style.Font.FontColor = XLColor.White;
        ws.Range(headerRow1, colPostponedRep, headerRow1, colPostponedTot).Style.Font.Bold = true;
        ws.Range(headerRow1, colPostponedRep, headerRow1, colPostponedTot).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 5. نسبة الإنجاز
        ws.Cell(headerRow1, colRate).Value = "نسبة الإنجاز";
        ws.Cell(headerRow1, colRate).Style.Fill.BackgroundColor = XLColor.FromHtml("#C00000");
        ws.Cell(headerRow1, colRate).Style.Font.FontColor = XLColor.White;
        ws.Cell(headerRow1, colRate).Style.Font.Bold = true;
        ws.Cell(headerRow1, colRate).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 6. توزيع الأحكام القطعية على القضاة
        ws.Range(headerRow1, judgeCols[0], headerRow1, colTotalJudges).Merge();
        ws.Cell(headerRow1, judgeCols[0]).Value = "توزيع الأحكام القطعية على السادة القضاة";
        ws.Range(headerRow1, judgeCols[0], headerRow1, colTotalJudges).Style.Fill.BackgroundColor = XLColor.FromHtml("#7F6000");
        ws.Range(headerRow1, judgeCols[0], headerRow1, colTotalJudges).Style.Font.FontColor = XLColor.White;
        ws.Range(headerRow1, judgeCols[0], headerRow1, colTotalJudges).Style.Font.Bold = true;
        ws.Range(headerRow1, judgeCols[0], headerRow1, colTotalJudges).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Row 8: Column Subheaders
        ws.Cell(headerRow2, colRank).Value = "الترتيب";
        ws.Cell(headerRow2, colCode).Value = "الكود";
        ws.Cell(headerRow2, colCircuit).Value = "الدائرة";
        ws.Cell(headerRow2, colPresident).Value = "رئيس الدائرة";
        ws.Cell(headerRow2, colCourt).Value = "المحكمة";
        ws.Cell(headerRow2, colIncoming).Value = colSuspended.HasValue ? "المقدم الإجمالي" : "المقدم";

        if (colSuspended.HasValue)
        {
            ws.Cell(headerRow2, colSuspended.Value).Value = "الموقوف (الوقف)";
        }
        if (colEffectiveIncoming.HasValue)
        {
            ws.Cell(headerRow2, colEffectiveIncoming.Value).Value = "الصافي بعد الخصم";
        }

        ws.Cell(headerRow2, colFinal).Value = "الأحكام القطعية";
        ws.Cell(headerRow2, colEvidence).Value = "أحكام الإثبات";
        ws.Cell(headerRow2, colOtherJudg).Value = "أخرى";
        ws.Cell(headerRow2, colTotalJudg).Value = "جملة الأحكام";
        ws.Cell(headerRow2, colPostponedRep).Value = "مؤجل للتقرير";
        ws.Cell(headerRow2, colPostponedTot).Value = "إجمالي المؤجل";
        ws.Cell(headerRow2, colRate).Value = "نسبة القطعي %";

        for (int i = 0; i < report.JudgeColumnsHeaders.Count; i++)
        {
            ws.Cell(headerRow2, judgeCols[i]).Value = report.JudgeColumnsHeaders[i];
        }
        ws.Cell(headerRow2, colTotalJudges).Value = "جملة القضاة";

        var headerRange = ws.Range(headerRow2, 1, headerRow2, totalColumns);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A4A6B");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Fill Data Rows
        int currentRow = 9;
        foreach (var r in report.Rows)
        {
            ws.Cell(currentRow, colRank).Value = r.Rank;
            ws.Cell(currentRow, colCode).Value = r.CircuitCode;
            ws.Cell(currentRow, colCircuit).Value = r.CircuitNameAr;
            ws.Cell(currentRow, colPresident).Value = r.PresidentJudgeNameAr;
            ws.Cell(currentRow, colCourt).Value = r.CourtNameAr;
            ws.Cell(currentRow, colIncoming).Value = r.TotalIncoming;

            if (colSuspended.HasValue)
            {
                ws.Cell(currentRow, colSuspended.Value).Value = r.SuspendedCount;
            }
            if (colEffectiveIncoming.HasValue)
            {
                ws.Cell(currentRow, colEffectiveIncoming.Value).Value = r.EffectiveIncoming;
            }

            ws.Cell(currentRow, colFinal).Value = r.FinalJudgmentsCount;
            ws.Cell(currentRow, colFinal).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEECC");

            ws.Cell(currentRow, colEvidence).Value = r.EvidenceJudgmentsCount;
            ws.Cell(currentRow, colEvidence).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAD1F7");

            ws.Cell(currentRow, colOtherJudg).Value = r.OtherJudgmentsCount;
            ws.Cell(currentRow, colTotalJudg).Value = r.TotalAllJudgmentsCount;
            ws.Cell(currentRow, colPostponedRep).Value = r.ReportPostponedCount;
            ws.Cell(currentRow, colPostponedTot).Value = r.TotalPostponedCount;

            ws.Cell(currentRow, colRate).Value = r.CompletionRate / 100.0;
            ws.Cell(currentRow, colRate).Style.NumberFormat.Format = "0.0%";
            ws.Cell(currentRow, colRate).Style.Font.Bold = true;
            ws.Cell(currentRow, colRate).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE0E0");

            for (int i = 0; i < judgeCols.Count; i++)
            {
                int val = i < r.JudgesAllocations.Count ? r.JudgesAllocations[i].FinalJudgmentsCount : 0;
                ws.Cell(currentRow, judgeCols[i]).Value = val;
            }
            ws.Cell(currentRow, colTotalJudges).Value = r.TotalJudgesAllocations;
            ws.Cell(currentRow, colTotalJudges).Style.Font.Bold = true;

            var dataRowRange = ws.Range(currentRow, 1, currentRow, totalColumns);
            dataRowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, colCircuit).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(currentRow, colPresident).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            currentRow++;
        }

        // Totals Row
        int totalRowIndex = currentRow;
        ws.Range(totalRowIndex, colRank, totalRowIndex, colCourt).Merge();
        ws.Cell(totalRowIndex, colRank).Value = "الإجمالي العام";
        ws.Cell(totalRowIndex, colRank).Style.Font.Bold = true;
        ws.Cell(totalRowIndex, colRank).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(totalRowIndex, colIncoming).Value = report.Totals.TotalIncoming;
        if (colSuspended.HasValue)
        {
            ws.Cell(totalRowIndex, colSuspended.Value).Value = report.Totals.SuspendedCount;
        }
        if (colEffectiveIncoming.HasValue)
        {
            ws.Cell(totalRowIndex, colEffectiveIncoming.Value).Value = report.Totals.EffectiveIncoming;
        }

        ws.Cell(totalRowIndex, colFinal).Value = report.Totals.FinalJudgmentsCount;
        ws.Cell(totalRowIndex, colEvidence).Value = report.Totals.EvidenceJudgmentsCount;
        ws.Cell(totalRowIndex, colOtherJudg).Value = report.Totals.OtherJudgmentsCount;
        ws.Cell(totalRowIndex, colTotalJudg).Value = report.Totals.TotalAllJudgmentsCount;
        ws.Cell(totalRowIndex, colPostponedRep).Value = report.Totals.ReportPostponedCount;
        ws.Cell(totalRowIndex, colPostponedTot).Value = report.Totals.TotalPostponedCount;

        ws.Cell(totalRowIndex, colRate).Value = report.Totals.CompletionRate / 100.0;
        ws.Cell(totalRowIndex, colRate).Style.NumberFormat.Format = "0.0%";

        for (int i = 0; i < judgeCols.Count; i++)
        {
            int val = i < report.Totals.JudgesAllocations.Count ? report.Totals.JudgesAllocations[i].FinalJudgmentsCount : 0;
            ws.Cell(totalRowIndex, judgeCols[i]).Value = val;
        }
        ws.Cell(totalRowIndex, colTotalJudges).Value = report.Totals.TotalJudgesAllocations;

        var totalRowRange = ws.Range(totalRowIndex, 1, totalRowIndex, totalColumns);
        totalRowRange.Style.Font.Bold = true;
        totalRowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        totalRowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Apply grid borders to entire table
        var entireTable = ws.Range(headerRow1, 1, totalRowIndex, totalColumns);
        entireTable.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        entireTable.Style.Border.InsideBorderColor = XLColor.FromHtml("#CCCCCC");
        entireTable.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        entireTable.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1A4A6B");

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<StatisticsEntryProgressReportDto> GetStatisticsEntryProgressReportAsync(
        long judicialYearId,
        long statisticalPeriodId,
        long? courtUnitId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var period = await context.StatisticalPeriods
            .Include(p => p.JudicialYear)
            .FirstOrDefaultAsync(p => p.Id == statisticalPeriodId);

        if (period is null)
        {
            throw new KeyNotFoundException($"الفترة الإحصائية رقم {statisticalPeriodId} غير موجودة.");
        }

        var yearName = period.JudicialYear?.NameAr ?? "";
        var monthName = period.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG"));
        var monthStart = period.MonthStartDate;
        var monthEnd = period.MonthStartDate.AddMonths(1).AddDays(-1);

        // 1. استعلام كافة نسخ الجلسات السارية خلال هذا الشهر
        var svQuery = context.SessionVersions
            .Include(sv => sv.Session)
            .Include(sv => sv.CircuitVersion)
                .ThenInclude(cv => cv.Circuit)
                    .ThenInclude(c => c.CourtUnit)
                        .ThenInclude(cu => cu.ParentCourtUnit)
            .Include(sv => sv.CircuitVersion)
                .ThenInclude(cv => cv.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(sv => sv.OpeningBalances)
            .Where(sv => sv.EffectiveFrom <= monthEnd && sv.EffectiveTo >= monthStart)
            .Where(sv => sv.Session.ShowInProgressReport)
            .AsQueryable();

        if (courtUnitId.HasValue)
        {
            svQuery = svQuery.Where(sv => 
                sv.CircuitVersion.Circuit.CourtUnitId == courtUnitId.Value ||
                (sv.CircuitVersion.Circuit.CourtUnit != null && sv.CircuitVersion.Circuit.CourtUnit.ParentCourtUnitId == courtUnitId.Value));
        }

        var sessionVersions = await svQuery
            .OrderBy(sv => sv.CircuitVersion.Circuit.CourtUnit.Code)
            .ThenBy(sv => sv.CircuitVersion.Circuit.Code)
            .ThenBy(sv => sv.Session.Code)
            .ToListAsync();

        var sessionVersionIds = sessionVersions.Select(sv => sv.Id).ToList();

        // 2. استعلام كافة رؤوس الإحصائيات المسجلة لهذه الفترات
        var headers = await context.StatisticsHeaders
            .Include(h => h.IncomingRows)
            .Include(h => h.JudgeJudgments)
            .Include(h => h.Postponements)
            .Include(h => h.ProductionRows)
                .ThenInclude(pr => pr.ProductionItemType)
            .Where(h => h.StatisticalPeriodId == statisticalPeriodId && sessionVersionIds.Contains(h.SessionVersionId))
            .ToListAsync();

        var headersBySessionVersionId = headers
            .GroupBy(h => h.SessionVersionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var allSessionStatuses = new List<SessionEntryStatusDto>();

        foreach (var sv in sessionVersions)
        {
            var cv = sv.CircuitVersion;
            var circuit = cv?.Circuit;
            var court = circuit?.CourtUnit;
            var kind = cv?.CircuitKind ?? "CivilFull";
            
            // تحديد المسمى العربي لنوع الإحصائية والفرز
            string discKey;
            string discName;
            var sName = sv.Session?.NameAr ?? "";
            var sCode = sv.Session?.Code ?? "";

            if (kind is "Partial" or "Summary" or "CivilPartial" or "CriminalPartial")
            {
                if (sCode.Contains("-CIV-") || sName.Contains("مدني") || sName.Contains("توقيع"))
                {
                    discKey = "PartialCivil";
                    discName = "مدني جزئي وصحة توقيع";
                }
                else
                {
                    discKey = "PartialMisdemeanor";
                    discName = "جنح جزئي";
                }
            }
            else if (kind == "CivilFull")
            {
                discKey = "CivilFull";
                discName = "مدني كلي وتجاري";
            }
            else if (kind is "Labor" or "LaborMisdemeanor")
            {
                discKey = "Labor";
                discName = "عمال وتأمينات";
            }
            else if (kind is "CriminalAppeal" or "Juvenile")
            {
                discKey = "CriminalAppeal";
                discName = "جنح مستأنف وأحداث";
            }
            else if (kind == "Family")
            {
                discKey = "Family";
                discName = "محكمة الأسرة";
            }
            else
            {
                discKey = kind;
                discName = kind;
            }

            var judges = cv?.JudgeAssignments?
                .OrderBy(ja => ja.DisplayOrder)
                .Select(ja => ja.Judge?.FullNameAr ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? [];

            headersBySessionVersionId.TryGetValue(sv.Id, out var header);

            bool isEntered = header != null;
            bool isBalanced = false;
            SessionEntryState state = SessionEntryState.NotEntered;
            int totalIncoming = 0;
            int totalJudgments = 0;
            int totalPostponed = 0;
            string? balanceNotes = null;

            if (header != null)
            {
                int totalOpening = sv.OpeningBalances.Sum(o => o.Count);
                int totalNewIncoming = header.IncomingRows.Sum(i => i.Count);
                totalIncoming = totalOpening + totalNewIncoming;
                totalPostponed = header.Postponements.Sum(p => p.Count);

                // أحكام القضاة أو الأحكام من ProductionRows
                if (header.JudgeJudgments.Count > 0)
                {
                    totalJudgments = header.JudgeJudgments.Sum(j => j.Count);
                }
                else if (header.ProductionRows.Count > 0)
                {
                    totalJudgments = header.ProductionRows
                        .Where(pr => pr.ProductionItemType?.ItemKind == "Judgment" || pr.ProductionItemType == null)
                        .Sum(pr => pr.Count);
                }

                if (header.Status is "Approved" or "Submitted" or "UnderReview")
                {
                    isBalanced = true;
                    state = SessionEntryState.EnteredBalanced;
                }
                else
                {
                    // التحقق من توازن الإحصائية في حالة المسودة
                    int outgoing = totalJudgments + totalPostponed;
                    if (totalIncoming == outgoing && totalIncoming > 0)
                    {
                        isBalanced = true;
                        state = SessionEntryState.EnteredBalanced;
                    }
                    else if (totalIncoming == 0 && outgoing == 0)
                    {
                        // مسودة جديدة فارغة
                        isBalanced = false;
                        state = SessionEntryState.EnteredUnbalanced;
                        balanceNotes = "مسودة لم تُدخل أرقامها بعد";
                    }
                    else
                    {
                        isBalanced = false;
                        state = SessionEntryState.EnteredUnbalanced;
                        int diff = totalIncoming - outgoing;
                        balanceNotes = diff > 0 
                            ? $"المقدم ({totalIncoming}) أكبر من الخارج ({outgoing}) بمقدار {diff}" 
                            : $"الخارج ({outgoing}) أكبر من المقدم ({totalIncoming}) بمقدار {Math.Abs(diff)}";
                    }
                }
            }

            allSessionStatuses.Add(new SessionEntryStatusDto(
                SessionId: sv.SessionId,
                SessionVersionId: sv.Id,
                SessionCode: sv.Session?.Code ?? "",
                SessionNameAr: sv.Session?.NameAr ?? $"جلسة {sv.Id}",
                ScheduleDescription: sv.ScheduleDescription,
                CircuitId: circuit?.Id ?? 0,
                CircuitCode: circuit?.Code ?? "",
                CircuitNameAr: circuit?.NameAr ?? "",
                CircuitKind: kind,
                DisciplineKey: discKey,
                DisciplineNameAr: discName,
                CourtUnitId: court?.Id ?? 0,
                CourtUnitNameAr: court?.NameAr ?? "المحكمة",
                JudgesNames: judges,
                HeaderId: header?.Id,
                HeaderStatus: header?.Status,
                IsEntered: isEntered,
                IsBalanced: isBalanced,
                State: state,
                TotalIncoming: totalIncoming,
                TotalJudgments: totalJudgments,
                TotalPostponed: totalPostponed,
                CreatedAt: header?.CreatedAt,
                BalanceNotes: balanceNotes
            ));
        }

        // 3. حساب نسب الإدخال لكل محكمة
        var courtsProgress = allSessionStatuses
            .GroupBy(s => new { s.CourtUnitId, s.CourtUnitNameAr })
            .Select(g =>
            {
                int total = g.Count();
                int entered = g.Count(s => s.IsEntered);
                int balanced = g.Count(s => s.IsBalanced);
                int unbalanced = g.Count(s => s.IsEntered && !s.IsBalanced);
                int notEntered = total - entered;
                double entryRate = total > 0 ? Math.Round((double)entered / total * 100, 1) : 0;
                double balancedRate = total > 0 ? Math.Round((double)balanced / total * 100, 1) : 0;

                return new CourtEntryProgressDto(
                    g.Key.CourtUnitId,
                    g.Key.CourtUnitNameAr,
                    total,
                    entered,
                    balanced,
                    unbalanced,
                    notEntered,
                    entryRate,
                    balancedRate
                );
            })
            .OrderByDescending(c => c.TotalSessionsCount)
            .ToList();

        // 4. تجميع الدوائر والتصنيفات كما في ورقة الرئيسية
        var disciplineDefs = new[]
        {
            ("PartialCivil", "مدني وجنح جزئي وصحة توقيع", "📑"),
            ("CivilFull", "مدني كلي وتجاري ومستأنف", "🏛️"),
            ("Labor", "عمال وتأمينات وجنح عمال", "👷"),
            ("CriminalAppeal", "جنح مستأنف وأحداث", "⚖️"),
            ("Family", "محكمة الأسرة (نفس ومال)", "👨‍👩‍👧")
        };

        var disciplineGroups = new List<DisciplineEntryGroupDto>();
        foreach (var (key, label, icon) in disciplineDefs)
        {
            var sessions = allSessionStatuses.Where(s => 
                s.DisciplineKey == key || 
                (key == "PartialCivil" && s.DisciplineKey == "PartialMisdemeanor")
            ).ToList();

            if (sessions.Count > 0)
            {
                int total = sessions.Count;
                int entered = sessions.Count(s => s.IsEntered);
                int balanced = sessions.Count(s => s.IsBalanced);
                double rate = total > 0 ? Math.Round((double)entered / total * 100, 1) : 0;

                disciplineGroups.Add(new DisciplineEntryGroupDto(
                    key,
                    label,
                    icon,
                    total,
                    entered,
                    balanced,
                    rate,
                    sessions
                ));
            }
        }

        // إضافة أي تصنيفات أخرى إن وجدت ولم تكن ضمن القائمة
        var coveredKeys = disciplineGroups.Select(g => g.DisciplineKey).ToHashSet();
        coveredKeys.Add("PartialMisdemeanor");
        var otherSessions = allSessionStatuses.Where(s => !coveredKeys.Contains(s.DisciplineKey)).ToList();
        if (otherSessions.Count > 0)
        {
            int total = otherSessions.Count;
            int entered = otherSessions.Count(s => s.IsEntered);
            int balanced = otherSessions.Count(s => s.IsBalanced);
            double rate = total > 0 ? Math.Round((double)entered / total * 100, 1) : 0;

            disciplineGroups.Add(new DisciplineEntryGroupDto(
                "Other",
                "دوائر وجلسات أخرى",
                "📂",
                total,
                entered,
                balanced,
                rate,
                otherSessions
            ));
        }

        // 5. المؤشرات العامة
        int totalAll = allSessionStatuses.Count;
        int totalEnteredAll = allSessionStatuses.Count(s => s.IsEntered);
        int totalBalancedAll = allSessionStatuses.Count(s => s.IsBalanced);
        int totalUnbalancedAll = allSessionStatuses.Count(s => s.IsEntered && !s.IsBalanced);
        int totalNotEnteredAll = totalAll - totalEnteredAll;
        double overallEntryRate = totalAll > 0 ? Math.Round((double)totalEnteredAll / totalAll * 100, 1) : 0;
        double overallBalancedRate = totalAll > 0 ? Math.Round((double)totalBalancedAll / totalAll * 100, 1) : 0;

        return new StatisticsEntryProgressReportDto(
            JudicialYearId: judicialYearId,
            JudicialYearNameAr: yearName,
            StatisticalPeriodId: statisticalPeriodId,
            PeriodMonthNameAr: monthName,
            MonthStartDate: period.MonthStartDate,
            IsPeriodOpenForEntry: period.IsOpenForEntry,
            TotalSessionsCount: totalAll,
            TotalEnteredCount: totalEnteredAll,
            TotalBalancedCount: totalBalancedAll,
            TotalUnbalancedCount: totalUnbalancedAll,
            TotalNotEnteredCount: totalNotEnteredAll,
            OverallEntryRatePercent: overallEntryRate,
            OverallBalancedRatePercent: overallBalancedRate,
            CourtsProgress: courtsProgress,
            DisciplineGroups: disciplineGroups,
            AllSessions: allSessionStatuses
        );
    }

    // ======================== تقرير الجرد الشهري (الرصيد المتاح من المؤجلات على الشهور) ========================

    private static bool IsSubJudgmentOrEvidencePostponement(string kind) =>
        kind.Equals("SUB_JUDGMENT_EVID", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("INTERLOC_EVID", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("CRIM_CAT_PREP", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("EVID_EXPERT", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("INTERLOC", StringComparison.OrdinalIgnoreCase);

    private static bool IsReservedPostponement(string kind) =>
        kind.Equals("RESERVED", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("DEF_RESERVED", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("CRIM_DEF_RES", StringComparison.OrdinalIgnoreCase);

    private static bool IsForReportPostponement(string kind) =>
        kind.Equals("WAIT_REPORT", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("DEF_REPORT", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("FOR_REPORT", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("CRIM_DEF_REP", StringComparison.OrdinalIgnoreCase);

    private static bool IsOtherPostponement(string kind) =>
        !IsSubJudgmentOrEvidencePostponement(kind) &&
        !IsReservedPostponement(kind) &&
        !IsForReportPostponement(kind);

    public async Task<MonthlyInventoryReportDto> GetMonthlyInventoryReportAsync(MonthlyInventoryFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.Id == filter.JudicialYearId)
            ?? throw new KeyNotFoundException($"العام القضائي {filter.JudicialYearId} غير موجود.");

        var basePeriod = await context.StatisticalPeriods.FirstOrDefaultAsync(p => p.Id == filter.BaseStatisticalPeriodId)
            ?? throw new KeyNotFoundException($"الفترة الشهرية الأساس {filter.BaseStatisticalPeriodId} غير موجودة.");

        var baseMonthName = basePeriod.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG"));

        var court = filter.CourtUnitId.HasValue
            ? await context.CourtUnits.FindAsync(filter.CourtUnitId.Value)
            : await context.CourtUnits.FirstOrDefaultAsync();
        var courtName = court?.NameAr ?? "محكمة بني سويف الابتدائية";

        // حصر الإحصائيات المودعة حتى شهر الأساس
        var headersQuery = context.StatisticsHeaders
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.CircuitVersion)
                    .ThenInclude(cv => cv.Circuit)
            .Include(h => h.StatisticalPeriod)
            .Where(h => h.StatisticalPeriod.MonthStartDate <= basePeriod.MonthStartDate && h.Status != "Cancelled");

        if (filter.CourtUnitId.HasValue)
        {
            var childUnitIds = await context.CourtUnits
                .Where(cu => cu.ParentCourtUnitId == filter.CourtUnitId.Value || cu.Id == filter.CourtUnitId.Value)
                .Select(cu => cu.Id)
                .ToListAsync();
            headersQuery = headersQuery.Where(h => childUnitIds.Contains(h.SessionVersion.CircuitVersion.Circuit.CourtUnitId));
        }

        if (filter.CircuitId.HasValue)
        {
            headersQuery = headersQuery.Where(h => h.SessionVersion.CircuitVersion.CircuitId == filter.CircuitId.Value);
        }

        var headerIds = await headersQuery.Select(h => h.Id).ToListAsync();

        // حصر الشهور التالية بعد شهر الأساس التي يوجد بها مؤجلات أو تتبع العام
        var actualTargetPeriodIds = await context.Postponements
            .Where(p => headerIds.Contains(p.StatisticsHeaderId) && p.TargetPeriod.MonthStartDate > basePeriod.MonthStartDate)
            .Select(p => p.TargetPeriodId)
            .Distinct()
            .ToListAsync();

        var targetPeriods = await context.StatisticalPeriods
            .Where(p => (p.JudicialYearId == filter.JudicialYearId && p.MonthStartDate > basePeriod.MonthStartDate)
                     || actualTargetPeriodIds.Contains(p.Id))
            .OrderBy(p => p.MonthStartDate)
            .ToListAsync();

        if (targetPeriods.Count == 0)
        {
            targetPeriods = await context.StatisticalPeriods
                .Where(p => p.MonthStartDate > basePeriod.MonthStartDate)
                .OrderBy(p => p.MonthStartDate)
                .Take(12)
                .ToListAsync();
        }

        var targetPeriodIds = targetPeriods.Select(p => p.Id).ToList();

        // المؤجلات
        var postponements = await context.Postponements
            .Include(p => p.CaseYearBand)
            .Where(p => headerIds.Contains(p.StatisticsHeaderId) && targetPeriodIds.Contains(p.TargetPeriodId))
            .ToListAsync();

        // التقصير الوارد (SHORT)
        var shortIncomings = await context.IncomingRows
            .Include(i => i.IncomingType)
            .Include(i => i.CaseYearBand)
            .Where(i => headerIds.Contains(i.StatisticsHeaderId)
                     && i.IncomingType.Code == "SHORT"
                     && i.SourcePeriodId.HasValue
                     && targetPeriodIds.Contains(i.SourcePeriodId.Value))
            .ToListAsync();

        // شرائح سنوات القيد
        var allBands = await context.CaseYearBands
            .Where(b => b.Policy.JudicialYearId == filter.JudicialYearId)
            .OrderBy(b => b.SortOrder)
            .ToListAsync();

        if (allBands.Count == 0)
        {
            allBands = await context.CaseYearBands.OrderBy(b => b.SortOrder).ToListAsync();
        }

        var usedBandIds = postponements.Select(p => p.CaseYearBandId).Union(shortIncomings.Select(i => i.CaseYearBandId)).Distinct();
        foreach (var ubId in usedBandIds)
        {
            if (!allBands.Any(b => b.Id == ubId))
            {
                var bObj = await context.CaseYearBands.FindAsync(ubId);
                if (bObj != null) allBands.Add(bObj);
            }
        }
        allBands = allBands.OrderBy(b => b.SortOrder).ToList();

        var monthSections = new List<MonthlyInventoryMonthSectionDto>();

        foreach (var tp in targetPeriods)
        {
            var tpMonthName = tp.MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG"));
            var bandRows = new List<MonthlyInventoryBandRowDto>();

            foreach (var band in allBands)
            {
                var pForBand = postponements.Where(p => p.TargetPeriodId == tp.Id && p.CaseYearBandId == band.Id).ToList();
                var sForBand = shortIncomings.Where(i => i.SourcePeriodId == tp.Id && i.CaseYearBandId == band.Id).ToList();

                int subCount = pForBand.Where(p => IsSubJudgmentOrEvidencePostponement(p.PostponementKind)).Sum(p => p.Count);
                int resCount = pForBand.Where(p => IsReservedPostponement(p.PostponementKind)).Sum(p => p.Count);
                int repGross = pForBand.Where(p => IsForReportPostponement(p.PostponementKind)).Sum(p => p.Count);
                int shortCount = sForBand.Sum(i => i.Count);
                int repNet = Math.Max(0, repGross - shortCount);
                int othCount = pForBand.Where(p => IsOtherPostponement(p.PostponementKind)).Sum(p => p.Count);
                int totalAvail = subCount + resCount + repNet + othCount;

                bandRows.Add(new MonthlyInventoryBandRowDto(
                    band.Id,
                    band.LabelAr,
                    subCount,
                    resCount,
                    repGross,
                    shortCount,
                    repNet,
                    othCount,
                    totalAvail
                ));
            }

            var monthTotals = new MonthlyInventoryBandRowDto(
                0,
                "إجمالي الشهر",
                bandRows.Sum(b => b.SubJudgmentsCount),
                bandRows.Sum(b => b.ReservedCount),
                bandRows.Sum(b => b.ReportGrossCount),
                bandRows.Sum(b => b.ShortenedCount),
                bandRows.Sum(b => b.ReportNetCount),
                bandRows.Sum(b => b.OtherPostponedCount),
                bandRows.Sum(b => b.TotalAvailableBalance)
            );

            monthSections.Add(new MonthlyInventoryMonthSectionDto(
                tp.Id,
                tpMonthName,
                tp.MonthStartDate,
                bandRows,
                monthTotals
            ));
        }

        // إجمالي تراكمي لسنوات القيد عبر كافة الشهور التالية
        var cumulativeBands = new List<MonthlyInventoryBandRowDto>();
        foreach (var band in allBands)
        {
            var pForBandAll = postponements.Where(p => p.CaseYearBandId == band.Id).ToList();
            var sForBandAll = shortIncomings.Where(i => i.CaseYearBandId == band.Id).ToList();

            int subCount = pForBandAll.Where(p => IsSubJudgmentOrEvidencePostponement(p.PostponementKind)).Sum(p => p.Count);
            int resCount = pForBandAll.Where(p => IsReservedPostponement(p.PostponementKind)).Sum(p => p.Count);
            int repGross = pForBandAll.Where(p => IsForReportPostponement(p.PostponementKind)).Sum(p => p.Count);
            int shortCount = sForBandAll.Sum(i => i.Count);
            int repNet = Math.Max(0, repGross - shortCount);
            int othCount = pForBandAll.Where(p => IsOtherPostponement(p.PostponementKind)).Sum(p => p.Count);
            int totalAvail = subCount + resCount + repNet + othCount;

            cumulativeBands.Add(new MonthlyInventoryBandRowDto(
                band.Id,
                band.LabelAr,
                subCount,
                resCount,
                repGross,
                shortCount,
                repNet,
                othCount,
                totalAvail
            ));
        }

        var grandTotal = new MonthlyInventoryBandRowDto(
            0,
            "المجموع الكلي للرصيد المتاح",
            cumulativeBands.Sum(b => b.SubJudgmentsCount),
            cumulativeBands.Sum(b => b.ReservedCount),
            cumulativeBands.Sum(b => b.ReportGrossCount),
            cumulativeBands.Sum(b => b.ShortenedCount),
            cumulativeBands.Sum(b => b.ReportNetCount),
            cumulativeBands.Sum(b => b.OtherPostponedCount),
            cumulativeBands.Sum(b => b.TotalAvailableBalance)
        );

        var scopeDesc = filter.CircuitId.HasValue
            ? "دائرة قضائية محددة"
            : (filter.CourtUnitId.HasValue ? courtName : "كافة دوائر وجلسات المحكمة");

        return new MonthlyInventoryReportDto(
            courtName,
            year.NameAr,
            $"جرد بعد إيداع شهر {baseMonthName}",
            scopeDesc,
            monthSections,
            cumulativeBands,
            grandTotal
        );
    }

    public async Task<byte[]> ExportMonthlyInventoryToExcelAsync(MonthlyInventoryFilter filter)
    {
        var report = await GetMonthlyInventoryReportAsync(filter);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("الجرد الشهري");
        ws.RightToLeft = true;
        ws.ShowGridLines = true;

        // Title and Header metadata
        ws.Cell("B2").Value = report.CourtNameAr;
        ws.Cell("B2").Style.Font.Bold = true;
        ws.Cell("B2").Style.Font.FontSize = 14;
        ws.Cell("B2").Style.Font.FontName = "Arial";

        ws.Cell("B3").Value = "قسم الإحصاء القضائي — كشف الجرد الشهري (الرصيد المتاح من المؤجلات على الشهور)";
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontSize = 12;
        ws.Cell("B3").Style.Font.FontName = "Arial";

        ws.Cell("B4").Value = $"{report.BasePeriodNameAr} — العام القضائي: {report.JudicialYearNameAr}";
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.FontSize = 11;
        ws.Cell("B4").Style.Font.FontColor = XLColor.FromHtml("#1A4A6B");
        ws.Cell("B4").Style.Font.FontName = "Arial";

        ws.Cell("B5").Value = $"نطاق الحصر: {report.ScopeDescriptionAr} — تاريخ التصدير: {DateTime.Now:yyyy/MM/dd HH:mm}";
        ws.Cell("B5").Style.Font.FontSize = 10;
        ws.Cell("B5").Style.Font.FontColor = XLColor.FromHtml("#64748B");
        ws.Cell("B5").Style.Font.FontName = "Arial";

        int currentRow = 7;

        // 1. الجدول المجمع لكافة الشهور التالية حسب سنوات القيد
        ws.Cell(currentRow, 1).Value = "أولاً: إجمالي الرصيد المتاح لكافة الشهور التالية مقسماً بحسب سنوات القيد";
        ws.Cell(currentRow, 1).Style.Font.Bold = true;
        ws.Cell(currentRow, 1).Style.Font.FontSize = 11;
        ws.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromHtml("#0F172A");
        currentRow++;

        int hRow = currentRow;
        string[] headers = new[]
        {
            "شريحة سنة القيد",
            "أحكام فرعية وإثبات",
            "محجوز للحكم",
            "مؤجل للتقرير (إجمالي)",
            "المقصر من الشهور التالية (خصم)",
            "صافي مؤجل للتقرير",
            "مؤجل لأسباب أخرى",
            "جملة الرصيد المتاح"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(hRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Arial";
            cell.Style.Font.FontSize = 10;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(i == 7 ? "#0F766E" : (i == 5 ? "#1E40AF" : "#334155"));
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        currentRow++;

        foreach (var b in report.CumulativeBandTotals)
        {
            ws.Cell(currentRow, 1).Value = b.BandLabelAr;
            ws.Cell(currentRow, 1).Style.Font.Bold = true;
            ws.Cell(currentRow, 2).Value = b.SubJudgmentsCount;
            ws.Cell(currentRow, 3).Value = b.ReservedCount;
            ws.Cell(currentRow, 4).Value = b.ReportGrossCount;
            ws.Cell(currentRow, 5).Value = b.ShortenedCount;
            ws.Cell(currentRow, 6).Value = b.ReportNetCount;
            ws.Cell(currentRow, 7).Value = b.OtherPostponedCount;
            ws.Cell(currentRow, 8).Value = b.TotalAvailableBalance;

            for (int i = 1; i <= 8; i++)
            {
                var cell = ws.Cell(currentRow, i);
                cell.Style.Font.FontName = "Arial";
                cell.Style.Alignment.Horizontal = i == 1 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                if (i == 8) { cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0FDFA"); }
                else if (i == 6) { cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF"); }
                else if (i == 5 && b.ShortenedCount > 0) { cell.Style.Font.FontColor = XLColor.FromHtml("#DC2626"); }
            }
            currentRow++;
        }

        // سطر المجموع الكلي
        var g = report.GrandTotal;
        ws.Cell(currentRow, 1).Value = g.BandLabelAr;
        ws.Cell(currentRow, 1).Style.Font.Bold = true;
        ws.Cell(currentRow, 2).Value = g.SubJudgmentsCount;
        ws.Cell(currentRow, 3).Value = g.ReservedCount;
        ws.Cell(currentRow, 4).Value = g.ReportGrossCount;
        ws.Cell(currentRow, 5).Value = g.ShortenedCount;
        ws.Cell(currentRow, 6).Value = g.ReportNetCount;
        ws.Cell(currentRow, 7).Value = g.OtherPostponedCount;
        ws.Cell(currentRow, 8).Value = g.TotalAvailableBalance;

        for (int i = 1; i <= 8; i++)
        {
            var cell = ws.Cell(currentRow, i);
            cell.Style.Font.FontName = "Arial";
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = i == 1 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Center;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        }
        currentRow += 3;

        // 2. الجداول التفصيلية لكل شهر تالٍ
        ws.Cell(currentRow, 1).Value = "ثانياً: الجداول التفصيلية للرصيد المتاح موزعة على الشهور التالية";
        ws.Cell(currentRow, 1).Style.Font.Bold = true;
        ws.Cell(currentRow, 1).Style.Font.FontSize = 11;
        currentRow++;

        foreach (var mSec in report.TargetMonths)
        {
            ws.Cell(currentRow, 1).Value = $"رصيد شهر: {mSec.TargetMonthNameAr}";
            ws.Cell(currentRow, 1).Style.Font.Bold = true;
            ws.Cell(currentRow, 1).Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
            currentRow++;

            int mhRow = currentRow;
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(mhRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontName = "Arial";
                cell.Style.Font.FontSize = 9;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#475569");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            currentRow++;

            foreach (var b in mSec.BandRows)
            {
                ws.Cell(currentRow, 1).Value = b.BandLabelAr;
                ws.Cell(currentRow, 2).Value = b.SubJudgmentsCount;
                ws.Cell(currentRow, 3).Value = b.ReservedCount;
                ws.Cell(currentRow, 4).Value = b.ReportGrossCount;
                ws.Cell(currentRow, 5).Value = b.ShortenedCount;
                ws.Cell(currentRow, 6).Value = b.ReportNetCount;
                ws.Cell(currentRow, 7).Value = b.OtherPostponedCount;
                ws.Cell(currentRow, 8).Value = b.TotalAvailableBalance;

                for (int i = 1; i <= 8; i++)
                {
                    var cell = ws.Cell(currentRow, i);
                    cell.Style.Font.FontName = "Arial";
                    cell.Style.Alignment.Horizontal = i == 1 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    if (i == 8) cell.Style.Font.Bold = true;
                }
                currentRow++;
            }

            // إجمالي الشهر
            var mt = mSec.MonthTotals;
            ws.Cell(currentRow, 1).Value = mt.BandLabelAr;
            ws.Cell(currentRow, 2).Value = mt.SubJudgmentsCount;
            ws.Cell(currentRow, 3).Value = mt.ReservedCount;
            ws.Cell(currentRow, 4).Value = mt.ReportGrossCount;
            ws.Cell(currentRow, 5).Value = mt.ShortenedCount;
            ws.Cell(currentRow, 6).Value = mt.ReportNetCount;
            ws.Cell(currentRow, 7).Value = mt.OtherPostponedCount;
            ws.Cell(currentRow, 8).Value = mt.TotalAvailableBalance;

            for (int i = 1; i <= 8; i++)
            {
                var cell = ws.Cell(currentRow, i);
                cell.Style.Font.FontName = "Arial";
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = i == 1 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Center;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            currentRow += 2;
        }

        ws.Columns().AdjustToContents(15, 35);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ======================== تقرير أحكام قاضٍ محدد منسوبة للجلسات ========================

    public async Task<JudgeSessionJudgmentsReportDto> GetJudgeSessionJudgmentsReportAsync(JudgeSessionJudgmentsFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var judge = await context.Judges.FirstOrDefaultAsync(j => j.Id == filter.JudgeId)
            ?? throw new KeyNotFoundException($"القاضي رقم {filter.JudgeId} غير موجود.");

        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.Id == filter.JudicialYearId)
            ?? throw new KeyNotFoundException($"العام القضائي {filter.JudicialYearId} غير موجود.");

        var periods = await context.StatisticalPeriods
            .Where(p => filter.StatisticalPeriodIds.Contains(p.Id))
            .OrderBy(p => p.MonthStartDate)
            .ToListAsync();

        var periodRangeName = periods.Count switch
        {
            0 => "كامل العام",
            1 => periods[0].MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG")),
            _ => $"من {periods.First().MonthStartDate:MMMM yyyy} إلى {periods.Last().MonthStartDate:MMMM yyyy}"
        };

        var court = filter.CourtUnitId.HasValue
            ? await context.CourtUnits.FindAsync(filter.CourtUnitId.Value)
            : await context.CourtUnits.FirstOrDefaultAsync();
        var courtName = court?.NameAr ?? "محكمة بني سويف الابتدائية";

        // حصر الإحصائيات في الفترات المحددة
        var headersQuery = context.StatisticsHeaders
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.Session)
                    .ThenInclude(s => s.Circuit)
                        .ThenInclude(c => c.CourtUnit)
            .Include(h => h.SessionVersion.CircuitVersion)
            .Where(h => filter.StatisticalPeriodIds.Contains(h.StatisticalPeriodId) && h.Status != "Cancelled");

        if (filter.CourtUnitId.HasValue)
        {
            var childUnitIds = await context.CourtUnits
                .Where(cu => cu.ParentCourtUnitId == filter.CourtUnitId.Value || cu.Id == filter.CourtUnitId.Value)
                .Select(cu => cu.Id)
                .ToListAsync();
            headersQuery = headersQuery.Where(h => childUnitIds.Contains(h.SessionVersion.CircuitVersion.Circuit.CourtUnitId));
        }

        var headers = await headersQuery.ToListAsync();
        var headerIds = headers.Select(h => h.Id).ToList();

        // أحكام القاضي
        var judgeJudgments = await context.JudgeJudgments
            .Where(jj => jj.JudgeId == filter.JudgeId && headerIds.Contains(jj.StatisticsHeaderId))
            .ToListAsync();

        var judgeAllocs = await context.JudgmentAllocations
            .Include(ja => ja.ProductionRow)
                .ThenInclude(pr => pr.ProductionItemType)
            .Where(ja => ja.JudgeId == filter.JudgeId && headerIds.Contains(ja.ProductionRow.StatisticsHeaderId))
            .ToListAsync();

        // مهام وتعيينات القاضي لمعرفة صفته في كل دائرة
        var assignments = await context.CircuitJudgeAssignments
            .Include(a => a.JudicialRole)
            .Include(a => a.CircuitVersion)
            .Where(a => a.JudgeId == filter.JudgeId)
            .ToListAsync();

        var primaryRole = assignments.FirstOrDefault()?.JudicialRole.NameAr ?? "قاضٍ";

        // استخراج أنواع القضايا المرتبطة بالجلسات
        var sessionVersionIds = headers.Select(h => h.SessionVersionId).Distinct().ToList();
        var sessionCaseTypes = await context.SessionCaseTypes
            .Include(sct => sct.CaseType)
            .Where(sct => sessionVersionIds.Contains(sct.SessionVersionId))
            .ToListAsync();

        // تجميع حسب الجلسة
        var sessionGroups = headers
            .GroupBy(h => h.SessionVersion.SessionId)
            .ToList();

        var sessionRows = new List<JudgeSessionRowDto>();

        foreach (var grp in sessionGroups)
        {
            var sessionHeaderIds = grp.Select(h => h.Id).ToList();
            var firstHeader = grp.First();
            var session = firstHeader.SessionVersion.Session;
            var circuit = session.Circuit;
            var cUnit = circuit.CourtUnit;

            // صفة القاضي في هذه الدائرة/الجلسة
            var assign = assignments.FirstOrDefault(a => a.CircuitVersion.CircuitId == circuit.Id);
            var roleName = assign?.JudicialRole?.NameAr ?? (circuit.Versions.Any(v => v.MinJudges == 1) ? "القاضي الفردي" : primaryRole);

            var sJudgments = judgeJudgments.Where(jj => sessionHeaderIds.Contains(jj.StatisticsHeaderId)).ToList();
            var sAllocs = judgeAllocs.Where(ja => sessionHeaderIds.Contains(ja.ProductionRow.StatisticsHeaderId)).ToList();

            if (sJudgments.Count == 0 && sAllocs.Count == 0)
            {
                continue;
            }

            // استخراج نوع القضايا لهذه الجلسة
            var grpVersionIds = grp.Select(h => h.SessionVersionId).Distinct().ToList();
            var caseTypeNames = sessionCaseTypes
                .Where(sct => grpVersionIds.Contains(sct.SessionVersionId))
                .Select(sct => sct.CaseType.NameAr)
                .Distinct()
                .ToList();

            string caseTypesDesc;
            if (caseTypeNames.Count > 0)
            {
                caseTypesDesc = string.Join("، ", caseTypeNames);
            }
            else
            {
                caseTypesDesc = firstHeader.SessionVersion.CircuitVersion?.CircuitKind switch
                {
                    "CivilFull" => "مدني كلي",
                    "Partial" => "مدني جزئي ومستعجل",
                    "CriminalAppeal" => "جنح مستأنف",
                    "Family" => "أسرة",
                    "Labor" => "عمال",
                    "LaborMisdemeanor" => "جنح عمال",
                    "PartialMisdemeanor" => "جنح جزئي",
                    _ => circuit.NameAr
                };
            }

            int finalAccept = 0;
            int finalReject = 0;
            int finalFormal = 0;
            int otherFinal = 0;
            int evidence = 0;
            int stay = 0;
            int other = 0;

            foreach (var jj in sJudgments)
            {
                var k = jj.JudgmentKind;
                int count = jj.Count;

                if (k.Equals("SUBST_ACCEPT", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith("FAM_DIVORCE", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith("FAM_ALIMONY", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith("FAM_CUSTODY", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith("FAM_WAGES", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("CRIM_CAT_SUBST", StringComparison.OrdinalIgnoreCase))
                {
                    finalAccept += count;
                }
                else if (k.Equals("SUBST_REJECT", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("SUBST_REJECT_STATE", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("SUBST_RES_JUDICATA", StringComparison.OrdinalIgnoreCase))
                {
                    finalReject += count;
                }
                else if (k.StartsWith("FORMAL_", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_CAT_PROC", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_NOT_ACCEPT", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_OPP_INADMISS", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_OPP_NULL", StringComparison.OrdinalIgnoreCase))
                {
                    finalFormal += count;
                }
                else if (k.Equals("SETTLEMENT", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_CAT_ORDER", StringComparison.OrdinalIgnoreCase) ||
                         k.StartsWith("CRIM_IMPRIS", StringComparison.OrdinalIgnoreCase) ||
                         k.StartsWith("CRIM_FINE", StringComparison.OrdinalIgnoreCase) ||
                         k.StartsWith("CRIM_ACQUIT", StringComparison.OrdinalIgnoreCase))
                {
                    otherFinal += count;
                }
                else if (k.StartsWith("EVID_", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("INTERLOC", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("FAM_ARBITRATORS", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_CAT_PREP", StringComparison.OrdinalIgnoreCase))
                {
                    evidence += count;
                }
                else if (k.StartsWith("STAY_", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("CRIM_CAT_STAY", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("STRIKEOUT", StringComparison.OrdinalIgnoreCase) ||
                         k.Equals("PROD_STRIKE", StringComparison.OrdinalIgnoreCase))
                {
                    stay += count;
                }
                else
                {
                    other += count;
                }
            }

            // تضمين Allocations إن وُجدت
            foreach (var ja in sAllocs)
            {
                var pt = ja.ProductionRow.ProductionItemType;
                if (pt != null)
                {
                    if (pt.Code.Contains("SUBST")) finalAccept += ja.Count;
                    else if (pt.Code.Contains("PROC")) finalFormal += ja.Count;
                    else if (pt.Code.Contains("PREP")) evidence += ja.Count;
                    else if (pt.Code.Contains("STAY")) stay += ja.Count;
                    else other += ja.Count;
                }
            }

            int totalFinal = finalAccept + finalReject + finalFormal + otherFinal;
            int grandTotal = totalFinal + evidence + stay + other;

            sessionRows.Add(new JudgeSessionRowDto(
                session.Id,
                circuit.Id,
                circuit.Code,
                circuit.NameAr,
                session.Code,
                session.NameAr,
                firstHeader.SessionVersion.ScheduleDescription ?? string.Empty,
                cUnit?.NameAr ?? courtName,
                roleName,
                judge.FullNameAr,
                caseTypesDesc,
                finalAccept,
                finalReject,
                finalFormal,
                otherFinal,
                totalFinal,
                evidence,
                stay,
                other,
                grandTotal
            ));
        }

        var grandTotals = new JudgeSessionRowDto(
            0,
            0,
            string.Empty,
            "المجموع الكلي لكافة الجلسات",
            string.Empty,
            string.Empty,
            string.Empty,
            courtName,
            string.Empty,
            judge.FullNameAr,
            string.Empty,
            sessionRows.Sum(s => s.FinalAcceptCount),
            sessionRows.Sum(s => s.FinalRejectCount),
            sessionRows.Sum(s => s.FinalFormalRejectCount),
            sessionRows.Sum(s => s.OtherFinalCount),
            sessionRows.Sum(s => s.TotalFinalJudgmentsCount),
            sessionRows.Sum(s => s.EvidenceJudgmentsCount),
            sessionRows.Sum(s => s.StayJudgmentsCount),
            sessionRows.Sum(s => s.OtherJudgmentsCount),
            sessionRows.Sum(s => s.GrandTotalJudgmentsCount)
        );

        return new JudgeSessionJudgmentsReportDto(
            judge.Id,
            judge.Code,
            judge.FullNameAr,
            primaryRole,
            year.NameAr,
            periodRangeName,
            courtName,
            sessionRows,
            grandTotals
        );
    }

    public async Task<byte[]> ExportJudgeSessionJudgmentsToExcelAsync(JudgeSessionJudgmentsFilter filter)
    {
        var report = await GetJudgeSessionJudgmentsReportAsync(filter);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("أحكام القاضي بالجلسات");
        ws.RightToLeft = true;
        ws.ShowGridLines = true;

        // Title and Header metadata
        ws.Cell("B2").Value = report.CourtNameAr;
        ws.Cell("B2").Style.Font.Bold = true;
        ws.Cell("B2").Style.Font.FontSize = 14;
        ws.Cell("B2").Style.Font.FontName = "Arial";

        ws.Cell("B3").Value = "قسم الإحصاء القضائي — تقرير إنجاز وأحكام السيد القاضي منسوبة للجلسات والدوائر";
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontSize = 12;
        ws.Cell("B3").Style.Font.FontName = "Arial";

        ws.Cell("B4").Value = $"السيد المستشار: {report.JudgeFullNameAr} (كود: {report.JudgeCode}) — الصفة: {report.JudicialRoleNameAr}";
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.FontSize = 11;
        ws.Cell("B4").Style.Font.FontColor = XLColor.FromHtml("#1A4A6B");
        ws.Cell("B4").Style.Font.FontName = "Arial";

        ws.Cell("B5").Value = $"الفترة: {report.PeriodRangeNameAr} — العام القضائي: {report.JudicialYearNameAr} — تاريخ التصدير: {DateTime.Now:yyyy/MM/dd HH:mm}";
        ws.Cell("B5").Style.Font.FontSize = 10;
        ws.Cell("B5").Style.Font.FontColor = XLColor.FromHtml("#64748B");
        ws.Cell("B5").Style.Font.FontName = "Arial";

        int hRow = 7;
        string[] headers = new[]
        {
            "م",
            "اسم القاضي",
            "اسم الدائرة",
            "عمله بالدائرة",
            "اسم الجلسة",
            "نوع القضايا",
            "الأحكام القطعية",
            "أحكام الفرعية والإثبات",
            "أحكام الوقف",
            "أحكام أخرى",
            "جملة الأحكام"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(hRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Arial";
            cell.Style.Font.FontSize = 10;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(i == 10 ? "#843C0C" : (i == 6 ? "#1D4ED8" : "#1A4A6B"));
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int currentRow = 8;
        int seq = 1;
        foreach (var s in report.SessionRows)
        {
            ws.Cell(currentRow, 1).Value = seq++;
            ws.Cell(currentRow, 2).Value = s.JudgeFullNameAr;
            ws.Cell(currentRow, 3).Value = s.CircuitNameAr;
            ws.Cell(currentRow, 4).Value = s.JudicialRoleNameAr;
            ws.Cell(currentRow, 5).Value = string.IsNullOrWhiteSpace(s.ScheduleDescription) ? s.SessionNameAr : $"{s.SessionNameAr} ({s.ScheduleDescription})";
            ws.Cell(currentRow, 6).Value = s.CaseTypesDescriptionAr;
            ws.Cell(currentRow, 7).Value = s.TotalFinalJudgmentsCount;
            ws.Cell(currentRow, 8).Value = s.EvidenceJudgmentsCount;
            ws.Cell(currentRow, 9).Value = s.StayJudgmentsCount;
            ws.Cell(currentRow, 10).Value = s.OtherJudgmentsCount;
            ws.Cell(currentRow, 11).Value = s.GrandTotalJudgmentsCount;

            for (int i = 1; i <= 11; i++)
            {
                var cell = ws.Cell(currentRow, i);
                cell.Style.Font.FontName = "Arial";
                cell.Style.Alignment.Horizontal = (i >= 2 && i <= 6) ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                if (i == 7) { cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF"); }
                else if (i == 11) { cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7"); }
            }
            currentRow++;
        }

        // سطر الإجمالي
        var tot = report.GrandTotals;
        ws.Cell(currentRow, 1).Value = "المجموع العام لكافة الجلسات";
        ws.Range(currentRow, 1, currentRow, 6).Merge();
        ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(currentRow, 7).Value = tot.TotalFinalJudgmentsCount;
        ws.Cell(currentRow, 8).Value = tot.EvidenceJudgmentsCount;
        ws.Cell(currentRow, 9).Value = tot.StayJudgmentsCount;
        ws.Cell(currentRow, 10).Value = tot.OtherJudgmentsCount;
        ws.Cell(currentRow, 11).Value = tot.GrandTotalJudgmentsCount;

        for (int i = 1; i <= 11; i++)
        {
            var cell = ws.Cell(currentRow, i);
            cell.Style.Font.FontName = "Arial";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
            if (i >= 7) cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Columns().AdjustToContents(12, 35);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ======================== تقرير أحكام الدوائر والجلسات ========================

    public async Task<List<CircuitReportColumnConfigDto>> GetCircuitReportColumnConfigsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var configs = await context.CircuitReportColumnConfigs
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        if (configs.Count == 0)
        {
            var defaultConfigs = GetDefaultCircuitReportColumnConfigs();
            context.CircuitReportColumnConfigs.AddRange(defaultConfigs);
            await context.SaveChangesAsync();

            configs = defaultConfigs;
        }

        return configs.Select(c => new CircuitReportColumnConfigDto(
            c.Id,
            c.ColumnKey,
            c.DefaultTitleAr,
            c.CustomTitleAr,
            c.Category,
            c.IsVisible,
            c.DisplayOrder,
            c.DisplayMode,
            c.IsSystemRequired,
            c.Description
        )).ToList();
    }

    public async Task SaveCircuitReportColumnConfigsAsync(List<CircuitReportColumnConfigDto> dtos)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.CircuitReportColumnConfigs.ToListAsync();

        foreach (var dto in dtos)
        {
            var entity = existing.FirstOrDefault(e => e.ColumnKey == dto.ColumnKey);
            if (entity != null)
            {
                entity.CustomTitleAr = dto.CustomTitleAr ?? string.Empty;
                entity.IsVisible = dto.IsVisible;
                entity.DisplayOrder = dto.DisplayOrder;
                entity.DisplayMode = dto.DisplayMode;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task ResetCircuitReportColumnConfigsToDefaultAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.CircuitReportColumnConfigs.ToListAsync();
        context.CircuitReportColumnConfigs.RemoveRange(existing);
        await context.SaveChangesAsync();

        var defaults = GetDefaultCircuitReportColumnConfigs();
        context.CircuitReportColumnConfigs.AddRange(defaults);
        await context.SaveChangesAsync();
    }

    private static List<CircuitReportColumnConfig> GetDefaultCircuitReportColumnConfigs() => new()
    {
        new() { ColumnKey = "FINAL", DefaultTitleAr = "أحكام قطعية", Category = "Final", IsVisible = true, DisplayOrder = 10, DisplayMode = "Consolidated", IsSystemRequired = true, Description = "مجموع الأحكام القطعية المنهية للخصومة (الموضوعية والشكلية والصلح)" },
        new() { ColumnKey = "INTERLOC", DefaultTitleAr = "أحكام فرعية", Category = "Interlocutory", IsVisible = true, DisplayOrder = 20, DisplayMode = "Consolidated", Description = "الأحكام الفرعية والتمهيدية قبل الفصل في الدعوى" },
        new() { ColumnKey = "EVID_EXPERT", DefaultTitleAr = "ندب خبير وإعادة", Category = "Evidence", IsVisible = true, DisplayOrder = 30, DisplayMode = "Consolidated", Description = "أحكام ندب الخبير وقرارات إعادة الدعوى لمكتب الخبراء" },
        new() { ColumnKey = "EVID_OTHER", DefaultTitleAr = "باقي الإثبات", Category = "Evidence", IsVisible = true, DisplayOrder = 40, DisplayMode = "Consolidated", Description = "التحقيق والاستجواب وحلف اليمين وندب حكمين" },
        new() { ColumnKey = "EVID_TOTAL", DefaultTitleAr = "جملة أحكام الإثبات", Category = "Evidence", IsVisible = true, DisplayOrder = 50, DisplayMode = "Consolidated", IsSystemRequired = true, Description = "مجموع ندب الخبير والإعادة وباقي أحكام الإثبات" },
        new() { ColumnKey = "STAY", DefaultTitleAr = "أحكام وقف", Category = "Stay", IsVisible = true, DisplayOrder = 60, DisplayMode = "Consolidated", Description = "أحكام الوقف الجزائي والتعليقي والاتفاقي" },
        new() { ColumnKey = "OTHER", DefaultTitleAr = "أحكام أخرى", Category = "Other", IsVisible = true, DisplayOrder = 70, DisplayMode = "Consolidated", Description = "انقطاع سير الخصومة والأحكام القضائية الأخرى" },
        new() { ColumnKey = "TOTAL_JUDGMENTS", DefaultTitleAr = "جملة الأحكام", Category = "Total", IsVisible = true, DisplayOrder = 80, DisplayMode = "Consolidated", IsSystemRequired = true, Description = "المجموع الإجمالي لكافة الأحكام الصادرة بالجلسة" },

        // أعمدة تفصيلية اختيارية
        new() { ColumnKey = "FINAL_FORMAL", DefaultTitleAr = "شكلي", Category = "Final", IsVisible = false, DisplayOrder = 11, DisplayMode = "Detailed", Description = "عدم القبول وعدم الاختصاص وسقوط الخصومة" },
        new() { ColumnKey = "FINAL_SUBST", DefaultTitleAr = "موضوعي", Category = "Final", IsVisible = false, DisplayOrder = 12, DisplayMode = "Detailed", Description = "القبول والرفض والأحكام الموضوعية" },
        new() { ColumnKey = "FINAL_SETTLE", DefaultTitleAr = "صلح", Category = "Final", IsVisible = false, DisplayOrder = 13, DisplayMode = "Detailed", Description = "إلحاق الصلح بمحضر الجلسة" },
        new() { ColumnKey = "EVID_EXPERT_NEW", DefaultTitleAr = "ندب خبير", Category = "Evidence", IsVisible = false, DisplayOrder = 31, DisplayMode = "Detailed", Description = "ندب الخبير لأول مرة" },
        new() { ColumnKey = "EVID_EXPERT_RECALL", DefaultTitleAr = "إعادة للخبير", Category = "Evidence", IsVisible = false, DisplayOrder = 32, DisplayMode = "Detailed", Description = "إعادة الدعوى للخبراء" },
        new() { ColumnKey = "EVID_INVEST", DefaultTitleAr = "تحقيق", Category = "Evidence", IsVisible = false, DisplayOrder = 41, DisplayMode = "Detailed", Description = "إحالة الدعوى للتحقيق" },
        new() { ColumnKey = "EVID_CROSS", DefaultTitleAr = "استجواب", Category = "Evidence", IsVisible = false, DisplayOrder = 42, DisplayMode = "Detailed", Description = "استجواب الخصوم" },
        new() { ColumnKey = "EVID_OATH", DefaultTitleAr = "حلف يمين", Category = "Evidence", IsVisible = false, DisplayOrder = 43, DisplayMode = "Detailed", Description = "توجيه اليمين الحاسمة أو المتممة" },
        new() { ColumnKey = "STAY_PENAL", DefaultTitleAr = "وقف جزائي", Category = "Stay", IsVisible = false, DisplayOrder = 61, DisplayMode = "Detailed", Description = "الوقف الجزائي" },
        new() { ColumnKey = "STAY_SUSP", DefaultTitleAr = "وقف تعليقي", Category = "Stay", IsVisible = false, DisplayOrder = 62, DisplayMode = "Detailed", Description = "الوقف التعليقي لحين الفصل في مسألة أخرى" },
        new() { ColumnKey = "STAY_AGREE", DefaultTitleAr = "وقف اتفاقي", Category = "Stay", IsVisible = false, DisplayOrder = 63, DisplayMode = "Detailed", Description = "الوقف الاتفاقي بين الخصوم" }
    };

    public async Task<CircuitJudgmentsReportDto> GetCircuitJudgmentsReportAsync(CircuitJudgmentsFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var activeConfigs = await GetCircuitReportColumnConfigsAsync();
        var visibleConfigs = activeConfigs.Where(c => c.IsVisible).OrderBy(c => c.DisplayOrder).ToList();

        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.Id == filter.JudicialYearId);
        var yearName = year?.NameAr ?? string.Empty;

        var periods = await context.StatisticalPeriods
            .Where(p => filter.StatisticalPeriodIds.Contains(p.Id))
            .OrderBy(p => p.MonthStartDate)
            .ToListAsync();

        var periodRangeName = periods.Count switch
        {
            0 => "كامل العام",
            1 => periods[0].MonthStartDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ar-EG")),
            _ => $"من {periods.First().MonthStartDate:MMMM yyyy} إلى {periods.Last().MonthStartDate:MMMM yyyy}"
        };

        // المحكمة المختارة
        string courtFilterName = "كافة المحاكم والمأموريات";
        List<long>? targetCourtUnitIds = null;
        if (filter.CourtUnitId.HasValue)
        {
            var selectedCourt = await context.CourtUnits.FindAsync(filter.CourtUnitId.Value);
            courtFilterName = selectedCourt?.NameAr ?? string.Empty;
            targetCourtUnitIds = await context.CourtUnits
                .Where(u => u.Id == filter.CourtUnitId.Value || u.ParentCourtUnitId == filter.CourtUnitId.Value)
                .Select(u => u.Id)
                .ToListAsync();
        }

        // التخصص القضائي
        string disciplineFilterName = "كافة التخصصات";
        List<string>? allowedCircuitKinds = null;
        if (!string.IsNullOrWhiteSpace(filter.StatisticalDisciplineCode))
        {
            var disc = await context.StatisticalDisciplines.FirstOrDefaultAsync(d => d.Code == filter.StatisticalDisciplineCode);
            if (disc != null)
            {
                disciplineFilterName = disc.NameAr;
                if (!string.IsNullOrWhiteSpace(disc.CircuitKinds))
                {
                    allowedCircuitKinds = disc.CircuitKinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                }
            }
            else
            {
                allowedCircuitKinds = filter.StatisticalDisciplineCode.ToUpperInvariant() switch
                {
                    "CIVIL" => new List<string> { "CivilFull", "Partial" },
                    "CRIMINAL" => new List<string> { "CriminalAppeal", "PartialMisdemeanor", "LaborMisdemeanor" },
                    "FAMILY" => new List<string> { "Family" },
                    "LABOR" => new List<string> { "Labor" },
                    _ => null
                };
            }
        }

        // نوع القضية
        string caseTypeFilterName = "كافة أنواع القضايا";
        if (filter.SubCaseTypeId.HasValue)
        {
            var ct = await context.CaseTypes.FindAsync(filter.SubCaseTypeId.Value);
            if (ct != null) caseTypeFilterName = ct.NameAr;
        }
        else if (filter.MainCaseTaxonomyNodeId.HasValue)
        {
            var node = await context.CaseTaxonomyNodes.FindAsync(filter.MainCaseTaxonomyNodeId.Value);
            if (node != null) caseTypeFilterName = node.NameAr;
        }

        // استعلام الإحصائيات المعتمدة فقط
        var query = context.StatisticsHeaders
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.Session)
                    .ThenInclude(s => s.Circuit)
                        .ThenInclude(c => c.CourtUnit)
            .Include(h => h.SessionVersion.CircuitVersion)
            .Where(h => filter.StatisticalPeriodIds.Contains(h.StatisticalPeriodId) && h.Status == "Approved");

        if (targetCourtUnitIds != null)
        {
            query = query.Where(h => targetCourtUnitIds.Contains(h.SessionVersion.CircuitVersion.Circuit.CourtUnitId));
        }

        if (allowedCircuitKinds != null && allowedCircuitKinds.Count > 0)
        {
            query = query.Where(h => allowedCircuitKinds.Contains(h.SessionVersion.CircuitVersion.CircuitKind));
        }

        var headers = await query.ToListAsync();
        var headerIds = headers.Select(h => h.Id).ToList();

        // فلترة بنوع القضايا إن وُجدت
        var sessionVersionIds = headers.Select(h => h.SessionVersionId).Distinct().ToList();
        var sessionCaseTypes = await context.SessionCaseTypes
            .Include(sct => sct.CaseType)
                .ThenInclude(ct => ct.TaxonomyNode)
            .Where(sct => sessionVersionIds.Contains(sct.SessionVersionId))
            .ToListAsync();

        if (filter.SubCaseTypeId.HasValue)
        {
            var validSvIds = sessionCaseTypes
                .Where(sct => sct.CaseTypeId == filter.SubCaseTypeId.Value)
                .Select(sct => sct.SessionVersionId)
                .ToHashSet();
            headers = headers.Where(h => validSvIds.Contains(h.SessionVersionId)).ToList();
            headerIds = headers.Select(h => h.Id).ToList();
        }
        else if (filter.MainCaseTaxonomyNodeId.HasValue)
        {
            var validSvIds = sessionCaseTypes
                .Where(sct => sct.CaseType.TaxonomyNodeId == filter.MainCaseTaxonomyNodeId.Value ||
                              (sct.CaseType.TaxonomyNode != null && sct.CaseType.TaxonomyNode.ParentId == filter.MainCaseTaxonomyNodeId.Value))
                .Select(sct => sct.SessionVersionId)
                .ToHashSet();
            headers = headers.Where(h => validSvIds.Contains(h.SessionVersionId)).ToList();
            headerIds = headers.Select(h => h.Id).ToList();
        }

        // جلب أحكام القضاة والإنتاج للإحصائيات المفلترة
        var allJudgeJudgments = await context.JudgeJudgments
            .Where(jj => headerIds.Contains(jj.StatisticsHeaderId))
            .ToListAsync();

        var allProductionRows = await context.ProductionRows
            .Include(pr => pr.ProductionItemType)
            .Where(pr => headerIds.Contains(pr.StatisticsHeaderId))
            .ToListAsync();

        var allJudgmentAllocs = await context.JudgmentAllocations
            .Include(ja => ja.ProductionRow)
                .ThenInclude(pr => pr.ProductionItemType)
            .Where(ja => headerIds.Contains(ja.ProductionRow.StatisticsHeaderId))
            .ToListAsync();

        // تجميع حسب الجلسة
        var sessionGroups = headers
            .GroupBy(h => h.SessionVersion.SessionId)
            .ToList();

        var sessionDtos = new List<CircuitJudgmentsSessionRowDto>();

        foreach (var sGrp in sessionGroups)
        {
            var sHeaderIds = sGrp.Select(h => h.Id).ToHashSet();
            var firstHeader = sGrp.First();
            var session = firstHeader.SessionVersion.Session;
            var circuit = session.Circuit;
            var cUnit = circuit.CourtUnit;
            var cKind = firstHeader.SessionVersion.CircuitVersion?.CircuitKind ?? string.Empty;

            var sJudgments = allJudgeJudgments.Where(jj => sHeaderIds.Contains(jj.StatisticsHeaderId)).ToList();
            var sProductions = allProductionRows.Where(pr => sHeaderIds.Contains(pr.StatisticsHeaderId)).ToList();
            var sAllocs = allJudgmentAllocs.Where(ja => sHeaderIds.Contains(ja.ProductionRow.StatisticsHeaderId)).ToList();

            // نوع القضايا
            var sVersionIds = sGrp.Select(h => h.SessionVersionId).Distinct().ToList();
            var ctNames = sessionCaseTypes
                .Where(sct => sVersionIds.Contains(sct.SessionVersionId))
                .Select(sct => sct.CaseType.NameAr)
                .Distinct()
                .ToList();

            string caseTypeDesc = ctNames.Count > 0 ? string.Join("، ", ctNames) : GetCircuitKindAr(cKind);

            var detailed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int finalCount = 0;
            int interlocCount = 0;
            int expertCount = 0;
            int otherEvidenceCount = 0;
            int stayCount = 0;
            int otherCount = 0;

            bool isCrim = cKind.Contains("Criminal", StringComparison.OrdinalIgnoreCase) ||
                          cKind.Contains("Misdemeanor", StringComparison.OrdinalIgnoreCase);

            if (isCrim)
            {
                // الجنائي
                if (sJudgments.Any(jj => jj.JudgmentKind.StartsWith("CRIM_CAT_")))
                {
                    foreach (var jj in sJudgments)
                    {
                        var k = jj.JudgmentKind;
                        int c = jj.Count;
                        if (k.Equals("CRIM_CAT_SUBST", StringComparison.OrdinalIgnoreCase) ||
                            k.Equals("CRIM_CAT_PROC", StringComparison.OrdinalIgnoreCase) ||
                            k.Equals("CRIM_CAT_ORDER", StringComparison.OrdinalIgnoreCase))
                        {
                            finalCount += c;
                        }
                        else if (k.Equals("CRIM_CAT_PREP", StringComparison.OrdinalIgnoreCase))
                        {
                            expertCount += c;
                        }
                        else if (k.Equals("CRIM_CAT_STAY", StringComparison.OrdinalIgnoreCase))
                        {
                            stayCount += c;
                        }
                        else
                        {
                            otherCount += c;
                        }
                    }
                }
                else
                {
                    foreach (var pr in sProductions)
                    {
                        var code = pr.ProductionItemType?.Code ?? string.Empty;
                        int c = pr.Count;
                        if (code.StartsWith("CRIM_EXP_"))
                        {
                            expertCount += c;
                            if (code == "CRIM_EXP_NEW") detailed["EVID_EXPERT_NEW"] = detailed.GetValueOrDefault("EVID_EXPERT_NEW") + c;
                            if (code == "CRIM_EXP_RECALL") detailed["EVID_EXPERT_RECALL"] = detailed.GetValueOrDefault("EVID_EXPERT_RECALL") + c;
                        }
                        else if (code.StartsWith("CRIM_SUSP"))
                        {
                            stayCount += c;
                        }
                        else if (code == "CRIM_OTHER" || code == "CRIM_STAY" || code == "CRIM_RESUME")
                        {
                            otherCount += c;
                        }
                        else
                        {
                            finalCount += c;
                        }
                    }
                }
            }
            else
            {
                // مدني / أسرة / عمال
                foreach (var jj in sJudgments)
                {
                    var k = jj.JudgmentKind;
                    int c = jj.Count;

                    if (k.StartsWith("FORMAL_"))
                    {
                        finalCount += c;
                        detailed["FINAL_FORMAL"] = detailed.GetValueOrDefault("FINAL_FORMAL") + c;
                    }
                    else if (k.StartsWith("SUBST_") || k.StartsWith("FAM_"))
                    {
                        if (k == "FAM_ARBITRATORS")
                        {
                            otherEvidenceCount += c;
                        }
                        else
                        {
                            finalCount += c;
                            detailed["FINAL_SUBST"] = detailed.GetValueOrDefault("FINAL_SUBST") + c;
                        }
                    }
                    else if (k == "SETTLEMENT")
                    {
                        finalCount += c;
                        detailed["FINAL_SETTLE"] = detailed.GetValueOrDefault("FINAL_SETTLE") + c;
                    }
                    else if (k == "INTERLOC")
                    {
                        interlocCount += c;
                    }
                    else if (k == "EVID_EXPERT")
                    {
                        expertCount += c;
                        detailed["EVID_EXPERT_NEW"] = detailed.GetValueOrDefault("EVID_EXPERT_NEW") + c;
                    }
                    else if (k == "EVID_RE_EXPERT")
                    {
                        expertCount += c;
                        detailed["EVID_EXPERT_RECALL"] = detailed.GetValueOrDefault("EVID_EXPERT_RECALL") + c;
                    }
                    else if (k == "EVID_INVEST")
                    {
                        otherEvidenceCount += c;
                        detailed["EVID_INVEST"] = detailed.GetValueOrDefault("EVID_INVEST") + c;
                    }
                    else if (k == "EVID_CROSS")
                    {
                        otherEvidenceCount += c;
                        detailed["EVID_CROSS"] = detailed.GetValueOrDefault("EVID_CROSS") + c;
                    }
                    else if (k == "EVID_OATH")
                    {
                        otherEvidenceCount += c;
                        detailed["EVID_OATH"] = detailed.GetValueOrDefault("EVID_OATH") + c;
                    }
                    else if (k.StartsWith("STAY_"))
                    {
                        stayCount += c;
                        if (k == "STAY_PENAL") detailed["STAY_PENAL"] = detailed.GetValueOrDefault("STAY_PENAL") + c;
                        else if (k == "STAY_COND") detailed["STAY_SUSP"] = detailed.GetValueOrDefault("STAY_SUSP") + c;
                        else if (k == "STAY_AGREE") detailed["STAY_AGREE"] = detailed.GetValueOrDefault("STAY_AGREE") + c;
                    }
                    else
                    {
                        otherCount += c;
                    }
                }
            }

            int totalEvidence = expertCount + otherEvidenceCount;
            int totalJudgments = finalCount + interlocCount + totalEvidence + stayCount + otherCount;

            sessionDtos.Add(new CircuitJudgmentsSessionRowDto(
                session.Id,
                session.Code,
                session.NameAr,
                circuit.Id,
                circuit.Code,
                circuit.NameAr,
                cKind,
                GetCircuitKindAr(cKind),
                cUnit.Id,
                cUnit.NameAr,
                caseTypeDesc,
                finalCount,
                interlocCount,
                expertCount,
                otherEvidenceCount,
                totalEvidence,
                stayCount,
                otherCount,
                totalJudgments,
                detailed
            ));
        }

        // تجميع هرمي: محاكم -> دوائر -> جلسات
        var courtGroups = new List<CircuitJudgmentsCourtGroupDto>();

        var courtGrpList = sessionDtos.GroupBy(s => s.CourtUnitId).ToList();
        foreach (var cGrp in courtGrpList)
        {
            var firstSession = cGrp.First();
            var courtName = firstSession.CourtNameAr;

            var circuitGroups = new List<CircuitJudgmentsCircuitGroupDto>();
            var circGrpList = cGrp.GroupBy(s => s.CircuitId).ToList();

            foreach (var circGrp in circGrpList)
            {
                var firstCircSession = circGrp.First();
                var sRows = circGrp.OrderBy(s => s.SessionNameAr).ToList();

                var circSubTotal = SumSessionRows(
                    sRows,
                    0,
                    $"إجمالي دائرة {firstCircSession.CircuitNameAr}",
                    firstCircSession.CircuitId,
                    firstCircSession.CircuitNameAr,
                    firstCircSession.CircuitKindAr,
                    firstCircSession.CourtUnitId,
                    firstCircSession.CourtNameAr
                );

                circuitGroups.Add(new CircuitJudgmentsCircuitGroupDto(
                    firstCircSession.CircuitId,
                    firstCircSession.CircuitCode,
                    firstCircSession.CircuitNameAr,
                    firstCircSession.CircuitKindAr,
                    firstCircSession.CourtNameAr,
                    sRows,
                    circSubTotal
                ));
            }

            var courtSubTotal = SumSessionRows(
                cGrp.ToList(),
                0,
                $"إجمالي محكمة {courtName}",
                0,
                string.Empty,
                string.Empty,
                cGrp.Key,
                courtName
            );

            courtGroups.Add(new CircuitJudgmentsCourtGroupDto(
                cGrp.Key,
                courtName,
                circuitGroups.OrderBy(cg => cg.CircuitNameAr).ToList(),
                courtSubTotal
            ));
        }

        var grandTotal = SumSessionRows(
            sessionDtos,
            0,
            "الإجمالي العام لكافة المحاكم والدوائر",
            0,
            string.Empty,
            string.Empty,
            0,
            string.Empty
        );

        return new CircuitJudgmentsReportDto(
            yearName,
            periodRangeName,
            courtFilterName,
            disciplineFilterName,
            caseTypeFilterName,
            courtGroups.OrderBy(cg => cg.CourtNameAr).ToList(),
            grandTotal,
            visibleConfigs
        );
    }

    private static CircuitJudgmentsSessionRowDto SumSessionRows(
        IEnumerable<CircuitJudgmentsSessionRowDto> rows,
        long sessionId,
        string sessionNameAr,
        long circuitId,
        string circuitNameAr,
        string circuitKindAr,
        long courtUnitId,
        string courtNameAr)
    {
        int finalCount = rows.Sum(r => r.FinalCount);
        int interlocCount = rows.Sum(r => r.InterlocutoryCount);
        int expertCount = rows.Sum(r => r.ExpertCount);
        int otherEvidenceCount = rows.Sum(r => r.OtherEvidenceCount);
        int totalEvidenceCount = rows.Sum(r => r.TotalEvidenceCount);
        int stayCount = rows.Sum(r => r.StayCount);
        int otherCount = rows.Sum(r => r.OtherCount);
        int totalJudgmentsCount = rows.Sum(r => r.TotalJudgmentsCount);

        var detailed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            foreach (var kvp in r.DetailedCounts)
            {
                detailed[kvp.Key] = detailed.GetValueOrDefault(kvp.Key) + kvp.Value;
            }
        }

        return new CircuitJudgmentsSessionRowDto(
            sessionId,
            string.Empty,
            sessionNameAr,
            circuitId,
            string.Empty,
            circuitNameAr,
            string.Empty,
            circuitKindAr,
            courtUnitId,
            courtNameAr,
            "—",
            finalCount,
            interlocCount,
            expertCount,
            otherEvidenceCount,
            totalEvidenceCount,
            stayCount,
            otherCount,
            totalJudgmentsCount,
            detailed
        );
    }

    private static string GetCircuitKindAr(string kind) => kind switch
    {
        "CivilFull" => "مدني كلي",
        "Partial" => "مدني جزئي ومستعجل",
        "CriminalAppeal" => "جنح مستأنف",
        "PartialMisdemeanor" => "جنح جزئي",
        "LaborMisdemeanor" => "جنح عمال",
        "Family" => "أسرة",
        "Labor" => "عمال",
        _ => kind
    };

    public async Task<byte[]> ExportCircuitJudgmentsToExcelAsync(CircuitJudgmentsFilter filter)
    {
        var report = await GetCircuitJudgmentsReportAsync(filter);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("أحكام الدوائر");
        ws.RightToLeft = true;
        ws.ShowGridLines = true;

        // Metadata Header
        ws.Cell("B2").Value = report.CourtFilterNameAr;
        ws.Cell("B2").Style.Font.Bold = true;
        ws.Cell("B2").Style.Font.FontSize = 14;
        ws.Cell("B2").Style.Font.FontName = "Arial";

        ws.Cell("B3").Value = "قسم الإحصاء القضائي — تقرير أحكام الدوائر وتوزيع الجلسات";
        ws.Cell("B3").Style.Font.Bold = true;
        ws.Cell("B3").Style.Font.FontSize = 12;
        ws.Cell("B3").Style.Font.FontName = "Arial";

        ws.Cell("B4").Value = $"العام القضائي: {report.JudicialYearNameAr} — الفترة: {report.PeriodRangeNameAr} — التخصص: {report.DisciplineFilterNameAr} — نوع القضايا: {report.CaseTypeFilterNameAr}";
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.FontSize = 10;
        ws.Cell("B4").Style.Font.FontColor = XLColor.FromHtml("#1E3A8A");
        ws.Cell("B4").Style.Font.FontName = "Arial";

        ws.Cell("B5").Value = $"تاريخ الاستخراج: {DateTime.Now:yyyy/MM/dd HH:mm} — مقتصر على الإحصائيات المعتمدة نهائياً";
        ws.Cell("B5").Style.Font.FontSize = 9;
        ws.Cell("B5").Style.Font.FontColor = XLColor.FromHtml("#64748B");
        ws.Cell("B5").Style.Font.FontName = "Arial";

        int hRow = 7;
        var headers = new List<string> { "م", "المحكمة", "الدائرة", "نوع الدائرة", "الجلسة", "نوع القضايا" };
        var activeCols = report.ActiveColumns;
        foreach (var col in activeCols)
        {
            headers.Add(col.EffectiveTitleAr);
        }

        for (int i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cell(hRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Arial";
            cell.Style.Font.FontSize = 10;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            var hName = headers[i];
            if (hName.Contains("جملة الأحكام"))
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#7C2D12");
                cell.Style.Font.FontColor = XLColor.White;
            }
            else if (hName.Contains("جملة") || hName.Contains("الإثبات"))
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#166534");
                cell.Style.Font.FontColor = XLColor.White;
            }
            else if (hName.Contains("قطعية"))
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
                cell.Style.Font.FontColor = XLColor.White;
            }
            else
            {
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                cell.Style.Font.FontColor = XLColor.White;
            }
        }

        int currentRow = 8;
        int seq = 1;

        foreach (var courtGrp in report.CourtGroups)
        {
            foreach (var circGrp in courtGrp.CircuitGroups)
            {
                foreach (var row in circGrp.SessionRows)
                {
                    ws.Cell(currentRow, 1).Value = seq++;
                    ws.Cell(currentRow, 2).Value = row.CourtNameAr;
                    ws.Cell(currentRow, 3).Value = row.CircuitNameAr;
                    ws.Cell(currentRow, 4).Value = row.CircuitKindAr;
                    ws.Cell(currentRow, 5).Value = row.SessionNameAr;
                    ws.Cell(currentRow, 6).Value = row.CaseTypeNamesAr;

                    int colIdx = 7;
                    foreach (var col in activeCols)
                    {
                        var cell = ws.Cell(currentRow, colIdx++);
                        int val = GetColumnValue(row, col.ColumnKey);
                        cell.Value = val;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        if (col.ColumnKey == "TOTAL_JUDGMENTS")
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                        else if (col.ColumnKey == "EVID_TOTAL")
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0FDF4");
                        else if (col.ColumnKey == "FINAL")
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
                    }

                    for (int c = 1; c <= headers.Count; c++)
                    {
                        ws.Cell(currentRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        ws.Cell(currentRow, c).Style.Font.FontName = "Arial";
                        ws.Cell(currentRow, c).Style.Font.FontSize = 9.5;
                    }

                    currentRow++;
                }

                // سطر إجمالي الدائرة
                var circSub = circGrp.SubTotal;
                ws.Cell(currentRow, 1).Value = "—";
                ws.Cell(currentRow, 2).Value = circSub.CourtNameAr;
                ws.Cell(currentRow, 3).Value = circSub.CircuitNameAr;
                ws.Cell(currentRow, 4).Value = circSub.CircuitKindAr;
                ws.Cell(currentRow, 5).Value = circSub.SessionNameAr;
                ws.Cell(currentRow, 6).Value = "—";

                int cColIdx = 7;
                foreach (var col in activeCols)
                {
                    var cell = ws.Cell(currentRow, cColIdx++);
                    cell.Value = GetColumnValue(circSub, col.ColumnKey);
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int c = 1; c <= headers.Count; c++)
                {
                    var cell = ws.Cell(currentRow, c);
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontName = "Arial";
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                currentRow++;
            }

            // سطر إجمالي المحكمة
            var courtSub = courtGrp.SubTotal;
            ws.Cell(currentRow, 1).Value = "—";
            ws.Cell(currentRow, 2).Value = courtSub.CourtNameAr;
            ws.Cell(currentRow, 3).Value = "—";
            ws.Cell(currentRow, 4).Value = "—";
            ws.Cell(currentRow, 5).Value = courtSub.SessionNameAr;
            ws.Cell(currentRow, 6).Value = "—";

            int courtColIdx = 7;
            foreach (var col in activeCols)
            {
                var cell = ws.Cell(currentRow, courtColIdx++);
                cell.Value = GetColumnValue(courtSub, col.ColumnKey);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int c = 1; c <= headers.Count; c++)
            {
                var cell = ws.Cell(currentRow, c);
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontName = "Arial";
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0F2FE");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            }
            currentRow++;
        }

        // سطر الإجمالي العام
        var gTotal = report.GrandTotal;
        ws.Cell(currentRow, 1).Value = "★";
        ws.Cell(currentRow, 2).Value = "الإجمالي العام لكافة المحاكم والدوائر";
        ws.Range(currentRow, 2, currentRow, 6).Merge();

        int gColIdx = 7;
        foreach (var col in activeCols)
        {
            var cell = ws.Cell(currentRow, gColIdx++);
            cell.Value = GetColumnValue(gTotal, col.ColumnKey);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (int c = 1; c <= headers.Count; c++)
        {
            var cell = ws.Cell(currentRow, c);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Arial";
            cell.Style.Font.FontSize = 10.5;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Double;
        }

        ws.Columns().AdjustToContents(10, 35);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static int GetColumnValue(CircuitJudgmentsSessionRowDto row, string columnKey) => columnKey switch
    {
        "FINAL" => row.FinalCount,
        "INTERLOC" => row.InterlocutoryCount,
        "EVID_EXPERT" => row.ExpertCount,
        "EVID_OTHER" => row.OtherEvidenceCount,
        "EVID_TOTAL" => row.TotalEvidenceCount,
        "STAY" => row.StayCount,
        "OTHER" => row.OtherCount,
        "TOTAL_JUDGMENTS" => row.TotalJudgmentsCount,
        _ => row.DetailedCounts.GetValueOrDefault(columnKey, 0)
    };
}

