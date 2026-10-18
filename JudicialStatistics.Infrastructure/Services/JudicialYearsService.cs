using System.Globalization;
using JudicialStatistics.Application.Years;
using JudicialStatistics.Domain.Years;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

public class JudicialYearsService : IJudicialYearsService
{
    private static readonly CultureInfo ArabicCulture = new("ar-EG");
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public JudicialYearsService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<JudicialYearDto>> GetYearsAsync(long? reportingScopeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JudicialYears
            .Include(y => y.ReportingScope)
            .Include(y => y.Periods)
            .AsQueryable();

        if (reportingScopeId.HasValue)
        {
            query = query.Where(y => y.ReportingScopeId == reportingScopeId.Value);
        }

        return await query
            .OrderByDescending(y => y.StartDate)
            .Select(y => new JudicialYearDto(
                y.Id,
                y.ReportingScopeId,
                y.ReportingScope.NameAr,
                y.NameAr,
                y.StartDate,
                y.EndDate,
                y.Status,
                y.Periods.Count
            ))
            .ToListAsync();
    }

    public async Task<JudicialYearDto?> GetByIdAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var y = await context.JudicialYears
            .Include(x => x.ReportingScope)
            .Include(x => x.Periods)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (y is null) return null;

        return new JudicialYearDto(
            y.Id,
            y.ReportingScopeId,
            y.ReportingScope.NameAr,
            y.NameAr,
            y.StartDate,
            y.EndDate,
            y.Status,
            y.Periods.Count
        );
    }

    public async Task<JudicialYearDto?> GetActiveYearAsync(long? reportingScopeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JudicialYears
            .Include(x => x.ReportingScope)
            .Include(x => x.Periods)
            .Where(x => x.Status == "Active");

        if (reportingScopeId.HasValue)
        {
            query = query.Where(x => x.ReportingScopeId == reportingScopeId.Value);
        }

        var y = await query.OrderByDescending(x => x.StartDate).FirstOrDefaultAsync();
        if (y is null) return null;

        return new JudicialYearDto(
            y.Id,
            y.ReportingScopeId,
            y.ReportingScope.NameAr,
            y.NameAr,
            y.StartDate,
            y.EndDate,
            y.Status,
            y.Periods.Count
        );
    }

    public async Task<List<StatisticalPeriodDto>> GetPeriodsForYearAsync(long judicialYearId, long? courtUnitId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var year = await context.JudicialYears.FindAsync(judicialYearId);
        var yearName = year?.NameAr ?? string.Empty;

        var periods = await context.StatisticalPeriods
            .Where(p => p.JudicialYearId == judicialYearId)
            .OrderBy(p => p.MonthStartDate)
            .ToListAsync();

        if (courtUnitId.HasValue && courtUnitId.Value > 0)
        {
            var periodIds = periods.Select(p => p.Id).ToList();
            var courtStatuses = await context.CourtPeriodStatuses
                .Where(s => periodIds.Contains(s.StatisticalPeriodId) && s.CourtUnitId == courtUnitId.Value)
                .ToDictionaryAsync(s => s.StatisticalPeriodId, s => s.IsOpenForEntry);

            var court = await context.CourtUnits.FindAsync(courtUnitId.Value);
            Dictionary<long, bool> parentStatuses = [];
            if (court?.ParentCourtUnitId != null)
            {
                parentStatuses = await context.CourtPeriodStatuses
                    .Where(s => periodIds.Contains(s.StatisticalPeriodId) && s.CourtUnitId == court.ParentCourtUnitId.Value)
                    .ToDictionaryAsync(s => s.StatisticalPeriodId, s => s.IsOpenForEntry);
            }

            return periods.Select(p =>
            {
                bool isOpen = courtStatuses.TryGetValue(p.Id, out var direct)
                    ? direct
                    : (parentStatuses.TryGetValue(p.Id, out var parentVal) ? parentVal : p.IsOpenForEntry);

                return new StatisticalPeriodDto(
                    p.Id,
                    p.JudicialYearId,
                    p.MonthStartDate,
                    p.MonthStartDate.ToString("MMMM", ArabicCulture),
                    yearName,
                    isOpen
                );
            }).ToList();
        }

        return periods.Select(p => new StatisticalPeriodDto(
            p.Id,
            p.JudicialYearId,
            p.MonthStartDate,
            p.MonthStartDate.ToString("MMMM", ArabicCulture),
            yearName,
            p.IsOpenForEntry
        )).ToList();
    }

    public async Task<bool> IsPeriodOpenForCourtAsync(long periodId, long courtUnitId)
    {
        if (courtUnitId <= 0)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var p = await ctx.StatisticalPeriods.FindAsync(periodId);
            return p?.IsOpenForEntry ?? true;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        // 1. فحص حالة المحكمة المباشرة
        var status = await context.CourtPeriodStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CourtUnitId == courtUnitId && s.StatisticalPeriodId == periodId);
        if (status != null) return status.IsOpenForEntry;

        // 2. فحص حالة المحكمة الأب (المحكمة الابتدائية)
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

    public async Task<List<CourtPeriodStatusDto>> GetCourtPeriodStatusesAsync(long periodId, long? primaryCourtId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var period = await context.StatisticalPeriods.FindAsync(periodId);
        if (period is null) return [];

        var courtQuery = context.CourtUnits.Where(c => c.IsActive);
        if (primaryCourtId.HasValue && primaryCourtId.Value > 0)
        {
            courtQuery = courtQuery.Where(c => c.Id == primaryCourtId.Value || c.ParentCourtUnitId == primaryCourtId.Value);
        }

        var courts = await courtQuery
            .OrderBy(c => c.ParentCourtUnitId == null ? 0 : 1)
            .ThenBy(c => c.NameAr)
            .ToListAsync();

        var courtIds = courts.Select(c => c.Id).ToList();
        var statuses = await context.CourtPeriodStatuses
            .Include(s => s.UpdatedByUser)
            .Where(s => s.StatisticalPeriodId == periodId && courtIds.Contains(s.CourtUnitId))
            .ToDictionaryAsync(s => s.CourtUnitId);

        CourtPeriodStatus? primaryStatus = null;
        if (primaryCourtId.HasValue && statuses.TryGetValue(primaryCourtId.Value, out var ps))
        {
            primaryStatus = ps;
        }

        var result = new List<CourtPeriodStatusDto>();
        foreach (var c in courts)
        {
            if (statuses.TryGetValue(c.Id, out var s))
            {
                result.Add(new CourtPeriodStatusDto(
                    c.Id,
                    c.NameAr,
                    c.UnitKind,
                    c.ParentCourtUnitId,
                    periodId,
                    s.IsOpenForEntry,
                    s.UpdatedAt,
                    s.UpdatedByUser?.FullNameAr
                ));
            }
            else
            {
                bool isOpen = (c.ParentCourtUnitId != null && primaryStatus != null)
                    ? primaryStatus.IsOpenForEntry
                    : period.IsOpenForEntry;

                result.Add(new CourtPeriodStatusDto(
                    c.Id,
                    c.NameAr,
                    c.UnitKind,
                    c.ParentCourtUnitId,
                    periodId,
                    isOpen,
                    null,
                    null
                ));
            }
        }

        return result;
    }

    public async Task<long> CreateYearAsync(JudicialYearCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var startDate = new DateOnly(cmd.StartYear, 10, 1);
        var endDate = new DateOnly(cmd.StartYear + 1, 9, 30);
        var nameAr = $"{cmd.StartYear}/{cmd.StartYear + 1}";

        // التحقق من عدم التداخل
        var exists = await context.JudicialYears.AnyAsync(y =>
            y.ReportingScopeId == cmd.ReportingScopeId &&
            y.StartDate == startDate);

        if (exists)
        {
            throw new InvalidOperationException($"العام القضائي {nameAr} مسجل بالفعل لنطاق التقرير هذا.");
        }

        var year = new JudicialYear
        {
            ReportingScopeId = cmd.ReportingScopeId,
            NameAr = nameAr,
            StartDate = startDate,
            EndDate = endDate,
            Status = "Draft"
        };

        // توليد الـ 12 فترة شهرية تلقائياً من أكتوبر حتى سبتمبر
        for (int i = 0; i < 12; i++)
        {
            var periodDate = startDate.AddMonths(i);
            year.Periods.Add(new StatisticalPeriod
            {
                MonthStartDate = periodDate
            });
        }

        context.JudicialYears.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    public async Task UpdateStatusAsync(JudicialYearUpdateStatusCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var year = await context.JudicialYears.FindAsync(cmd.Id);
        if (year is null)
        {
            throw new KeyNotFoundException($"العام القضائي رقم {cmd.Id} غير موجود.");
        }

        if (cmd.Status == "Active")
        {
            // عند تفعيل عام، نحول أي عام نشط آخر في نفس النطاق إلى مغلق
            var existingActive = await context.JudicialYears
                .Where(y => y.ReportingScopeId == year.ReportingScopeId && y.Id != year.Id && y.Status == "Active")
                .ToListAsync();

            foreach (var y in existingActive)
            {
                y.Status = "Closed";
            }
        }

        year.Status = cmd.Status;
        await context.SaveChangesAsync();
    }

    public async Task SetPeriodEntryStatusAsync(long periodId, bool isOpen)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var period = await context.StatisticalPeriods.FindAsync(periodId);
        if (period is null)
        {
            throw new KeyNotFoundException($"الفترة الإحصائية رقم {periodId} غير موجودة.");
        }

        period.IsOpenForEntry = isOpen;

        var existingStatuses = await context.CourtPeriodStatuses
            .Where(s => s.StatisticalPeriodId == periodId)
            .ToListAsync();

        foreach (var cs in existingStatuses)
        {
            cs.IsOpenForEntry = isOpen;
            cs.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task SetCourtPeriodEntryStatusAsync(long periodId, long courtUnitId, bool isOpen, long? currentUserId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var status = await context.CourtPeriodStatuses
            .FirstOrDefaultAsync(s => s.StatisticalPeriodId == periodId && s.CourtUnitId == courtUnitId);

        if (status is null)
        {
            status = new CourtPeriodStatus
            {
                StatisticalPeriodId = periodId,
                CourtUnitId = courtUnitId,
                IsOpenForEntry = isOpen,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = currentUserId
            };
            context.CourtPeriodStatuses.Add(status);
        }
        else
        {
            status.IsOpenForEntry = isOpen;
            status.UpdatedAt = DateTime.UtcNow;
            status.UpdatedByUserId = currentUserId;
        }

        // إذا كانت المحكمة هي المحكمة الابتدائية (ParentCourtUnitId == null) نطبق أيضاً على المحاكم التابعة
        var court = await context.CourtUnits.FindAsync(courtUnitId);
        if (court != null && court.ParentCourtUnitId == null)
        {
            var childCourts = await context.CourtUnits
                .Where(c => c.ParentCourtUnitId == courtUnitId && c.IsActive)
                .ToListAsync();

            var childIds = childCourts.Select(c => c.Id).ToList();
            var existingChildStatuses = await context.CourtPeriodStatuses
                .Where(s => s.StatisticalPeriodId == periodId && childIds.Contains(s.CourtUnitId))
                .ToListAsync();

            foreach (var child in childCourts)
            {
                var cs = existingChildStatuses.FirstOrDefault(s => s.CourtUnitId == child.Id);
                if (cs is null)
                {
                    context.CourtPeriodStatuses.Add(new CourtPeriodStatus
                    {
                        StatisticalPeriodId = periodId,
                        CourtUnitId = child.Id,
                        IsOpenForEntry = isOpen,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedByUserId = currentUserId
                    });
                }
                else
                {
                    cs.IsOpenForEntry = isOpen;
                    cs.UpdatedAt = DateTime.UtcNow;
                    cs.UpdatedByUserId = currentUserId;
                }
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task SetAllCourtsPeriodEntryStatusAsync(long periodId, long primaryCourtId, bool isOpen, long? currentUserId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var courts = await context.CourtUnits
            .Where(c => c.IsActive && (c.Id == primaryCourtId || c.ParentCourtUnitId == primaryCourtId))
            .ToListAsync();

        var courtIds = courts.Select(c => c.Id).ToList();
        var existingStatuses = await context.CourtPeriodStatuses
            .Where(s => s.StatisticalPeriodId == periodId && courtIds.Contains(s.CourtUnitId))
            .ToListAsync();

        foreach (var court in courts)
        {
            var st = existingStatuses.FirstOrDefault(s => s.CourtUnitId == court.Id);
            if (st is null)
            {
                context.CourtPeriodStatuses.Add(new CourtPeriodStatus
                {
                    StatisticalPeriodId = periodId,
                    CourtUnitId = court.Id,
                    IsOpenForEntry = isOpen,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = currentUserId
                });
            }
            else
            {
                st.IsOpenForEntry = isOpen;
                st.UpdatedAt = DateTime.UtcNow;
                st.UpdatedByUserId = currentUserId;
            }
        }

        var period = await context.StatisticalPeriods.FindAsync(periodId);
        if (period != null)
        {
            period.IsOpenForEntry = isOpen;
        }

        await context.SaveChangesAsync();
    }
}

