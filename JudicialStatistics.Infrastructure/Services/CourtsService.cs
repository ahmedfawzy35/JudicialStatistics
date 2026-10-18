using JudicialStatistics.Application.Courts;
using JudicialStatistics.Domain.Courts;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

public class CourtsService : ICourtsService
{
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public CourtsService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<CourtUnitDto>> GetTreeAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var allUnits = await context.CourtUnits
            .OrderBy(c => c.NameAr)
            .ToListAsync();

        return BuildTree(allUnits, null);
    }

    private static List<CourtUnitDto> BuildTree(List<CourtUnit> all, long? parentId)
    {
        return all
            .Where(u => u.ParentCourtUnitId == parentId)
            .Select(u => new CourtUnitDto(
                u.Id,
                u.ParentCourtUnitId,
                u.Code,
                u.NameAr,
                u.UnitKind,
                u.IsActive,
                BuildTree(all, u.Id)
            ))
            .ToList();
    }

    public async Task<List<CourtUnitDto>> GetFlatAsync(bool activeOnly = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.CourtUnits.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.NameAr)
            .Select(c => new CourtUnitDto(
                c.Id,
                c.ParentCourtUnitId,
                c.Code,
                c.NameAr,
                c.UnitKind,
                c.IsActive,
                new List<CourtUnitDto>()
            ))
            .ToListAsync();
    }

    public async Task<CourtUnitDto?> GetByIdAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var unit = await context.CourtUnits.FirstOrDefaultAsync(c => c.Id == id);
        if (unit is null) return null;

        return new CourtUnitDto(
            unit.Id,
            unit.ParentCourtUnitId,
            unit.Code,
            unit.NameAr,
            unit.UnitKind,
            unit.IsActive,
            new List<CourtUnitDto>()
        );
    }

    public async Task<long> CreateCourtUnitAsync(CourtUnitCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        if (await context.CourtUnits.AnyAsync(c => c.Code == cmd.Code))
        {
            throw new InvalidOperationException($"كود المحكمة '{cmd.Code}' موجود بالفعل.");
        }

        var entity = new CourtUnit
        {
            ParentCourtUnitId = cmd.ParentCourtUnitId,
            Code = cmd.Code.Trim(),
            NameAr = cmd.NameAr.Trim(),
            UnitKind = cmd.UnitKind,
            IsActive = true
        };

        context.CourtUnits.Add(entity);
        await context.SaveChangesAsync();

        // إنشاء نطاق تقارير تلقائي إذا كانت المحكمة المنشأة محكمة ابتدائية مستقلة
        if (entity.UnitKind == "Court" && entity.ParentCourtUnitId == null)
        {
            var scopeCode = $"SCOPE-{entity.Code}";
            if (!await context.ReportingScopes.AnyAsync(r => r.Code == scopeCode))
            {
                context.ReportingScopes.Add(new ReportingScope
                {
                    Code = scopeCode,
                    NameAr = $"نطاق {entity.NameAr}",
                    RootCourtUnitId = entity.Id,
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }
        }

        return entity.Id;
    }

    public async Task UpdateCourtUnitAsync(CourtUnitUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.CourtUnits.FindAsync(cmd.Id);
        if (entity is null)
        {
            throw new KeyNotFoundException($"وحدة المحكمة رقم {cmd.Id} غير موجودة.");
        }

        if (await context.CourtUnits.AnyAsync(c => c.Code == cmd.Code && c.Id != cmd.Id))
        {
            throw new InvalidOperationException($"كود المحكمة '{cmd.Code}' مستخدم لوحدة أخرى.");
        }

        entity.Code = cmd.Code.Trim();
        entity.NameAr = cmd.NameAr.Trim();
        entity.UnitKind = cmd.UnitKind;
        entity.IsActive = cmd.IsActive;

        await context.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.CourtUnits.FindAsync(id);
        if (entity is not null)
        {
            entity.IsActive = !entity.IsActive;
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<ReportingScopeDto>> GetReportingScopesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ReportingScopes
            .Include(r => r.RootCourtUnit)
            .OrderBy(r => r.NameAr)
            .Select(r => new ReportingScopeDto(
                r.Id,
                r.Code,
                r.NameAr,
                r.RootCourtUnitId,
                r.RootCourtUnit.NameAr,
                r.IsActive
            ))
            .ToListAsync();
    }
}
