using JudicialStatistics.Application.Organization;
using JudicialStatistics.Application.Statistics;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Domain.Geography;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Domain.Statistics;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IDbContextFactory<JudicialDbContext> _contextFactory;

    public OrganizationService(IDbContextFactory<JudicialDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ======================== Versions ========================

    public async Task<List<OrganizationVersionDto>> GetVersionsAsync(long judicialYearId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.OrganizationVersions
            .Include(v => v.JudicialYear)
            .Include(v => v.CircuitVersions)
            .Where(v => v.JudicialYearId == judicialYearId)
            .OrderByDescending(v => v.VersionNo)
            .Select(v => new OrganizationVersionDto(
                v.Id,
                v.JudicialYearId,
                v.JudicialYear.NameAr,
                v.VersionNo,
                v.DecisionReference,
                v.DecisionDate,
                v.EffectiveFrom,
                v.EffectiveTo,
                v.Status,
                v.ApprovedByUserId,
                v.ApprovedAt,
                v.CircuitVersions.Count
            ))
            .ToListAsync();
    }

    public async Task<OrganizationVersionDto?> GetVersionByIdAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var v = await context.OrganizationVersions
            .Include(x => x.JudicialYear)
            .Include(x => x.CircuitVersions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (v is null) return null;

        return new OrganizationVersionDto(
            v.Id,
            v.JudicialYearId,
            v.JudicialYear.NameAr,
            v.VersionNo,
            v.DecisionReference,
            v.DecisionDate,
            v.EffectiveFrom,
            v.EffectiveTo,
            v.Status,
            v.ApprovedByUserId,
            v.ApprovedAt,
            v.CircuitVersions.Count
        );
    }

    public async Task<long> CreateVersionAsync(OrganizationVersionCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.OrganizationVersions.AnyAsync(v =>
            v.JudicialYearId == cmd.JudicialYearId && v.VersionNo == cmd.VersionNo);

        if (exists)
        {
            throw new InvalidOperationException($"نسخة التشكيل رقم {cmd.VersionNo} مسجلة بالفعل لهذا العام القضائي.");
        }

        var entity = new OrganizationVersion
        {
            JudicialYearId = cmd.JudicialYearId,
            VersionNo = cmd.VersionNo,
            DecisionReference = cmd.DecisionReference.Trim(),
            DecisionDate = cmd.DecisionDate,
            EffectiveFrom = cmd.EffectiveFrom,
            EffectiveTo = cmd.EffectiveTo,
            Status = "Draft"
        };

        context.OrganizationVersions.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task ApproveVersionAsync(long id, long userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var version = await context.OrganizationVersions.FindAsync(id);
        if (version is null)
        {
            throw new KeyNotFoundException($"نسخة التشكيل رقم {id} غير موجودة.");
        }

        version.Status = "Approved";
        version.ApprovedByUserId = userId;
        version.ApprovedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    // ======================== Circuits ========================

    public async Task<List<CircuitDto>> GetCircuitsAsync(long judicialYearId, long? courtUnitId = null, long? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Circuits
            .Include(c => c.CourtUnit)
            .Include(c => c.Sessions)
            .Where(c => c.JudicialYearId == judicialYearId);

        if (courtUnitId.HasValue)
        {
            query = query.Where(c => c.CourtUnitId == courtUnitId.Value || c.CourtUnit.ParentCourtUnitId == courtUnitId.Value);
        }

        if (userId.HasValue && userId.Value > 0)
        {
            var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId.Value);
            if (!scope.IsSystemAdmin)
            {
                if (scope.AllowedSessionIds != null)
                {
                    query = query.Where(c => c.Sessions.Any(s => scope.AllowedSessionIds.Contains(s.Id)));
                }
                else if (scope.AllowedCourtIds != null)
                {
                    query = query.Where(c => scope.AllowedCourtIds.Contains(c.CourtUnitId) ||
                        (c.CourtUnit.ParentCourtUnitId != null && scope.AllowedCourtIds.Contains(c.CourtUnit.ParentCourtUnitId.Value)));
                }
                else if (scope.PrimaryCourtId.HasValue)
                {
                    query = query.Where(c => c.CourtUnitId == scope.PrimaryCourtId.Value ||
                        c.CourtUnit.ParentCourtUnitId == scope.PrimaryCourtId.Value);
                }
            }
        }

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new CircuitDto(
                c.Id,
                c.JudicialYearId,
                c.CourtUnitId,
                c.CourtUnit.NameAr,
                c.Code,
                c.NameAr,
                c.IsClosed,
                c.Sessions.Count,
                c.Versions.OrderByDescending(v => v.EffectiveFrom).Select(v => v.CircuitKind).FirstOrDefault() ?? ""
            ))
            .ToListAsync();
    }

    public async Task<CircuitDetailDto?> GetCircuitDetailAsync(long circuitId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var circuit = await context.Circuits
            .Include(c => c.CourtUnit)
            .Include(c => c.Versions)
                .ThenInclude(v => v.OrganizationVersion)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Versions)
                    .ThenInclude(v => v.Jurisdictions)
                        .ThenInclude(j => j.GeographicArea)
            .FirstOrDefaultAsync(c => c.Id == circuitId);

        if (circuit is null) return null;

        var versionDtos = circuit.Versions.Select(v => new CircuitVersionDto(
            v.Id,
            v.CircuitId,
            v.OrganizationVersionId,
            v.OrganizationVersion.DecisionReference,
            v.CircuitKind,
            v.EffectiveFrom,
            v.EffectiveTo,
            v.MinJudges,
            v.MaxJudges,
            v.JudgeAssignments.Select(ja => new CircuitJudgeAssignmentDto(
                ja.Id,
                ja.CircuitVersionId,
                ja.JudgeId,
                ja.Judge.FullNameAr,
                ja.JudicialRoleId,
                ja.JudicialRole.NameAr,
                ja.EffectiveFrom,
                ja.EffectiveTo,
                ja.DisplayOrder,
                circuit.Id,
                circuit.NameAr
            )).OrderBy(ja => ja.DisplayOrder).ToList()
        )).ToList();

        var sessionDtos = circuit.Sessions.Select(s => {
            var latestVersion = s.Versions.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault();
            var areas = latestVersion?.Jurisdictions
                .Where(j => j.GeographicArea != null)
                .Select(j => j.GeographicArea.NameAr)
                .ToList() ?? new List<string>();
            return new SessionDto(
                s.Id,
                s.CircuitId,
                circuit.NameAr,
                s.Code,
                s.NameAr,
                s.IsClosed,
                areas,
                latestVersion?.ScheduleDescription
            );
        }).ToList();

        return new CircuitDetailDto(
            circuit.Id,
            circuit.JudicialYearId,
            circuit.CourtUnitId,
            circuit.CourtUnit.NameAr,
            circuit.Code,
            circuit.NameAr,
            circuit.IsClosed,
            versionDtos,
            sessionDtos
        );
    }

    public async Task<long> CreateCircuitAsync(CircuitCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // إيجاد نسخة تشكيل إن لم تُحدد
        long orgVersionId;
        if (cmd.OrganizationVersionId.HasValue)
        {
            orgVersionId = cmd.OrganizationVersionId.Value;
        }
        else
        {
            var defaultVersion = await context.OrganizationVersions
                .Where(v => v.JudicialYearId == cmd.JudicialYearId)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefaultAsync();

            if (defaultVersion is null)
            {
                // إنشاء نسخة تشكيل افتراضية أولى
                var newVersion = new OrganizationVersion
                {
                    JudicialYearId = cmd.JudicialYearId,
                    VersionNo = 1,
                    DecisionReference = "قرار التشكيل السنوي الأساسي",
                    DecisionDate = cmd.EffectiveFrom,
                    EffectiveFrom = cmd.EffectiveFrom,
                    EffectiveTo = cmd.EffectiveTo,
                    Status = "Draft"
                };
                context.OrganizationVersions.Add(newVersion);
                await context.SaveChangesAsync();
                orgVersionId = newVersion.Id;
            }
            else
            {
                orgVersionId = defaultVersion.Id;
            }
        }

        var circuit = new Circuit
        {
            JudicialYearId = cmd.JudicialYearId,
            CourtUnitId = cmd.CourtUnitId,
            Code = cmd.Code.Trim(),
            NameAr = cmd.NameAr.Trim(),
            IsClosed = false
        };

        var circuitVersion = new CircuitVersion
        {
            OrganizationVersionId = orgVersionId,
            CircuitKind = cmd.CircuitKind,
            EffectiveFrom = cmd.EffectiveFrom,
            EffectiveTo = cmd.EffectiveTo,
            MinJudges = cmd.MinJudges,
            MaxJudges = cmd.MaxJudges
        };

        circuit.Versions.Add(circuitVersion);
        context.Circuits.Add(circuit);
        await context.SaveChangesAsync();

        return circuit.Id;
    }

    // ======================== Sessions ========================

    public async Task<List<SessionDto>> GetSessionsAsync(long? circuitId = null, long? judicialYearId = null, long? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Sessions
            .AsNoTracking()
            .Include(s => s.Employee)
            .Include(s => s.Circuit)
                .ThenInclude(c => c.CourtUnit)
            .Include(s => s.Versions)
                .ThenInclude(v => v.Jurisdictions)
                    .ThenInclude(j => j.GeographicArea)
            .AsQueryable();

        if (circuitId.HasValue)
        {
            query = query.Where(s => s.CircuitId == circuitId.Value);
        }

        if (judicialYearId.HasValue)
        {
            query = query.Where(s => s.Circuit.JudicialYearId == judicialYearId.Value);
        }

        if (userId.HasValue && userId.Value > 0)
        {
            var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId.Value);
            if (!scope.IsSystemAdmin)
            {
                if (scope.AllowedSessionIds != null)
                {
                    query = query.Where(s => scope.AllowedSessionIds.Contains(s.Id));
                }
                else if (scope.AllowedCourtIds != null)
                {
                    query = query.Where(s => scope.AllowedCourtIds.Contains(s.Circuit.CourtUnitId) ||
                        (s.Circuit.CourtUnit.ParentCourtUnitId != null && scope.AllowedCourtIds.Contains(s.Circuit.CourtUnit.ParentCourtUnitId.Value)));
                }
                else if (scope.PrimaryCourtId.HasValue)
                {
                    query = query.Where(s => s.Circuit.CourtUnitId == scope.PrimaryCourtId.Value ||
                        s.Circuit.CourtUnit.ParentCourtUnitId == scope.PrimaryCourtId.Value);
                }
            }
        }

        var sessions = await query
            .OrderBy(s => s.Code)
            .ToListAsync();

        var allVersionIds = sessions.SelectMany(s => s.Versions.Select(v => v.Id)).Distinct().ToList();
        var sessionCaseTypes = await context.SessionCaseTypes
            .AsNoTracking()
            .Include(sct => sct.CaseType)
            .Where(sct => allVersionIds.Contains(sct.SessionVersionId))
            .ToListAsync();
        var caseTypesMap = sessionCaseTypes
            .GroupBy(sct => sct.SessionVersionId)
            .ToDictionary(g => g.Key, g => g.Select(sct => sct.CaseType.NameAr).Distinct().ToList());

        return sessions.Select(s => {
            var latestVersion = s.Versions.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault();
            var areas = latestVersion?.Jurisdictions
                .Where(j => j.GeographicArea != null)
                .Select(j => j.GeographicArea.NameAr)
                .ToList() ?? new List<string>();
            var vId = latestVersion?.Id ?? 0;
            return new SessionDto(
                s.Id,
                s.CircuitId,
                s.Circuit.NameAr,
                s.Code,
                s.NameAr,
                s.IsClosed,
                areas,
                latestVersion?.ScheduleDescription,
                vId,
                s.EmployeeId,
                s.Employee?.FullNameAr,
                s.Employee?.JobTitle,
                s.Description,
                s.ShowInProgressReport,
                caseTypesMap.GetValueOrDefault(vId, new List<string>())
            );
        }).ToList();
    }

    public async Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var session = await context.Sessions
            .AsNoTracking()
            .Include(s => s.Employee)
            .Include(s => s.Circuit)
            .Include(s => s.Versions)
                .ThenInclude(v => v.CircuitVersion)
                    .ThenInclude(cv => cv.Circuit)
            .Include(s => s.Versions)
                .ThenInclude(v => v.Jurisdictions)
                    .ThenInclude(j => j.GeographicArea)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return null;

        var versionIds = session.Versions.Select(v => v.Id).ToList();
        var caseTypes = await context.SessionCaseTypes
            .AsNoTracking()
            .Where(ct => versionIds.Contains(ct.SessionVersionId))
            .ToListAsync();

        var versions = session.Versions.Select(v => new SessionVersionDto(
            v.Id,
            v.SessionId,
            v.CircuitVersionId,
            v.CircuitVersion.Circuit.NameAr,
            v.EffectiveFrom,
            v.EffectiveTo,
            v.ScheduleDescription,
            v.Jurisdictions.Where(j => j.GeographicArea != null).Select(j => j.GeographicArea.NameAr).ToList(),
            v.Jurisdictions.Select(j => j.GeographicAreaId).ToList(),
            caseTypes.Where(ct => ct.SessionVersionId == v.Id).Select(ct => ct.CaseTypeId).ToList()
        )).ToList();

        return new SessionDetailDto(
            session.Id,
            session.CircuitId,
            session.Circuit.NameAr,
            session.Code,
            session.NameAr,
            session.IsClosed,
            versions,
            session.EmployeeId,
            session.Employee?.FullNameAr,
            session.Employee?.JobTitle,
            session.Description,
            session.ShowInProgressReport
        );
    }

    public async Task<List<SessionVersionDto>> GetSessionVersionsForYearAsync(long judicialYearId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var versions = await context.SessionVersions
            .AsNoTracking()
            .Include(sv => sv.Session)
            .Include(sv => sv.CircuitVersion)
                .ThenInclude(cv => cv.Circuit)
            .Include(sv => sv.Jurisdictions)
                .ThenInclude(j => j.GeographicArea)
            .Where(sv => sv.CircuitVersion.Circuit.JudicialYearId == judicialYearId)
            .OrderBy(sv => sv.Session.Code)
            .ToListAsync();

        var versionIds = versions.Select(v => v.Id).ToList();
        var caseTypes = await context.SessionCaseTypes
            .AsNoTracking()
            .Where(ct => versionIds.Contains(ct.SessionVersionId))
            .ToListAsync();

        return versions.Select(sv => new SessionVersionDto(
            sv.Id,
            sv.SessionId,
            sv.CircuitVersionId,
            sv.CircuitVersion.Circuit.NameAr + " - " + sv.Session.NameAr,
            sv.EffectiveFrom,
            sv.EffectiveTo,
            sv.ScheduleDescription,
            sv.Jurisdictions.Where(j => j.GeographicArea != null).Select(j => j.GeographicArea.NameAr).ToList(),
            sv.Jurisdictions.Select(j => j.GeographicAreaId).ToList(),
            caseTypes.Where(ct => ct.SessionVersionId == sv.Id).Select(ct => ct.CaseTypeId).ToList()
        )).ToList();
    }

    public async Task<long> CreateSessionAsync(SessionCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var session = new Session
        {
            CircuitId = cmd.CircuitId,
            Code = cmd.Code.Trim(),
            NameAr = cmd.NameAr.Trim(),
            Description = cmd.Description?.Trim(),
            ShowInProgressReport = cmd.ShowInProgressReport,
            IsClosed = false,
            EmployeeId = cmd.EmployeeId
        };

        long circuitVerId;
        if (cmd.CircuitVersionId.HasValue)
        {
            circuitVerId = cmd.CircuitVersionId.Value;
        }
        else
        {
            var cv = await context.CircuitVersions
                .Where(v => v.CircuitId == cmd.CircuitId)
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (cv is null)
            {
                throw new InvalidOperationException("لا توجد نسخة دائرة معتمدة لربط الجلسة بها.");
            }
            circuitVerId = cv.Id;
        }

        var sessionVersion = new SessionVersion
        {
            CircuitVersionId = circuitVerId,
            EffectiveFrom = cmd.EffectiveFrom,
            EffectiveTo = cmd.EffectiveTo,
            ScheduleDescription = cmd.ScheduleDescription
        };

        if (cmd.GeographicAreaIds is { Count: > 0 })
        {
            foreach (var areaId in cmd.GeographicAreaIds)
            {
                sessionVersion.Jurisdictions.Add(new SessionJurisdiction
                {
                    GeographicAreaId = areaId
                });
            }
        }

        session.Versions.Add(sessionVersion);
        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        if (cmd.CaseTypeIds is { Count: > 0 })
        {
            foreach (var ctId in cmd.CaseTypeIds)
            {
                context.SessionCaseTypes.Add(new SessionCaseType
                {
                    SessionVersionId = sessionVersion.Id,
                    CaseTypeId = ctId
                });
            }
            await context.SaveChangesAsync();
        }

        return session.Id;
    }

    public async Task UpdateSessionAsync(SessionUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var session = await context.Sessions.FindAsync(cmd.Id);
        if (session == null) throw new InvalidOperationException("الجلسة غير موجودة.");
        session.NameAr = cmd.NameAr.Trim();
        session.Description = cmd.Description?.Trim();
        session.ShowInProgressReport = cmd.ShowInProgressReport;
        session.IsClosed = cmd.IsClosed;
        session.EmployeeId = cmd.EmployeeId;

        var latestVersion = await context.SessionVersions
            .Include(v => v.Jurisdictions)
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefaultAsync(v => v.SessionId == cmd.Id);

        if (latestVersion != null)
        {
            if (cmd.ScheduleDescription != null)
            {
                latestVersion.ScheduleDescription = cmd.ScheduleDescription.Trim();
            }

            if (cmd.GeographicAreaIds != null)
            {
                var toRemove = latestVersion.Jurisdictions
                    .Where(j => !cmd.GeographicAreaIds.Contains(j.GeographicAreaId))
                    .ToList();
                foreach (var r in toRemove)
                {
                    context.SessionJurisdictions.Remove(r);
                }

                var existingAreaIds = latestVersion.Jurisdictions.Select(j => j.GeographicAreaId).ToHashSet();
                foreach (var areaId in cmd.GeographicAreaIds)
                {
                    if (!existingAreaIds.Contains(areaId))
                    {
                        latestVersion.Jurisdictions.Add(new SessionJurisdiction
                        {
                            GeographicAreaId = areaId
                        });
                    }
                }
            }

            if (cmd.CaseTypeIds != null)
            {
                var existingCaseTypes = await context.SessionCaseTypes
                    .Where(ct => ct.SessionVersionId == latestVersion.Id)
                    .ToListAsync();

                var toRemoveCt = existingCaseTypes
                    .Where(ct => !cmd.CaseTypeIds.Contains(ct.CaseTypeId))
                    .ToList();
                foreach (var r in toRemoveCt)
                {
                    context.SessionCaseTypes.Remove(r);
                }

                var existingCtIds = existingCaseTypes.Select(ct => ct.CaseTypeId).ToHashSet();
                foreach (var ctId in cmd.CaseTypeIds)
                {
                    if (!existingCtIds.Contains(ctId))
                    {
                        context.SessionCaseTypes.Add(new SessionCaseType
                        {
                            SessionVersionId = latestVersion.Id,
                            CaseTypeId = ctId
                        });
                    }
                }
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task ToggleSessionClosedAsync(long sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var session = await context.Sessions.FindAsync(sessionId);
        if (session == null) throw new InvalidOperationException("الجلسة غير موجودة.");
        session.IsClosed = !session.IsClosed;
        await context.SaveChangesAsync();
    }

    public async Task<List<OpeningBalanceDto>> GetSessionOpeningBalancesAsync(long sessionVersionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.OpeningBalances
            .Include(o => o.CaseType)
            .Include(o => o.CaseYearBand)
            .Where(o => o.SessionVersionId == sessionVersionId)
            .OrderBy(o => o.CaseYearBandId)
            .Select(o => new OpeningBalanceDto(
                o.Id,
                o.CaseTypeId,
                o.CaseType.NameAr,
                o.CaseYearBandId,
                o.CaseYearBand != null ? o.CaseYearBand.LabelAr : "كافة السنوات",
                o.Count
            ))
            .ToListAsync();
    }

    public async Task SaveSessionOpeningBalancesAsync(long sessionVersionId, List<OpeningBalanceValue> balances, long currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sessionVersion = await context.SessionVersions
            .Include(v => v.Session)
            .FirstOrDefaultAsync(v => v.Id == sessionVersionId)
            ?? throw new KeyNotFoundException("نسخة الجلسة غير موجودة.");

        var existing = await context.OpeningBalances
            .Where(o => o.SessionVersionId == sessionVersionId)
            .ToListAsync();

        context.OpeningBalances.RemoveRange(existing);

        foreach (var b in balances.Where(b => b.Count > 0))
        {
            context.OpeningBalances.Add(new JudicialStatistics.Domain.Statistics.OpeningBalance
            {
                SessionVersionId = sessionVersionId,
                CaseTypeId = b.CaseTypeId,
                CaseYearBandId = b.CaseYearBandId,
                Count = b.Count
            });
        }

        await context.SaveChangesAsync();
    }

    // ======================== Judges & Roles ========================

    public async Task<List<JudgeDto>> GetJudgesAsync(bool activeOnly = true, long? judicialYearId = null, long? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Judges
            .Include(j => j.CreatedCourtUnit)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(j => j.IsActive);
        }

        if (userId.HasValue && userId.Value > 0)
        {
            var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId.Value);
            if (!scope.IsSystemAdmin)
            {
                HashSet<long> allowedCircuitIds = new();

                if (scope.AllowedSessionIds != null)
                {
                    var sQuery = context.Sessions.AsNoTracking().Where(s => scope.AllowedSessionIds.Contains(s.Id));
                    if (judicialYearId.HasValue && judicialYearId.Value > 0)
                    {
                        sQuery = sQuery.Where(s => s.Circuit.JudicialYearId == judicialYearId.Value);
                    }
                    var cIds = await sQuery.Select(s => s.CircuitId).Distinct().ToListAsync();
                    allowedCircuitIds = cIds.ToHashSet();
                }
                else if (scope.AllowedCourtIds != null)
                {
                    var cQuery = context.Circuits.AsNoTracking()
                        .Where(c => scope.AllowedCourtIds.Contains(c.CourtUnitId) ||
                                   (c.CourtUnit.ParentCourtUnitId != null && scope.AllowedCourtIds.Contains(c.CourtUnit.ParentCourtUnitId.Value)));
                    if (judicialYearId.HasValue && judicialYearId.Value > 0)
                    {
                        cQuery = cQuery.Where(c => c.JudicialYearId == judicialYearId.Value);
                    }
                    var cIds = await cQuery.Select(c => c.Id).Distinct().ToListAsync();
                    allowedCircuitIds = cIds.ToHashSet();
                }
                else if (scope.PrimaryCourtId.HasValue)
                {
                    var cQuery = context.Circuits.AsNoTracking()
                        .Where(c => c.CourtUnitId == scope.PrimaryCourtId.Value ||
                                   c.CourtUnit.ParentCourtUnitId == scope.PrimaryCourtId.Value);
                    if (judicialYearId.HasValue && judicialYearId.Value > 0)
                    {
                        cQuery = cQuery.Where(c => c.JudicialYearId == judicialYearId.Value);
                    }
                    var cIds = await cQuery.Select(c => c.Id).Distinct().ToListAsync();
                    allowedCircuitIds = cIds.ToHashSet();
                }

                // إيجاد القضاة المكلفين أو أصحاب الحركات في هذه الدوائر
                var assignedJudgeIds = await context.CircuitJudgeAssignments
                    .AsNoTracking()
                    .Where(a => allowedCircuitIds.Contains(a.CircuitVersion.CircuitId))
                    .Select(a => a.JudgeId)
                    .Distinct()
                    .ToListAsync();

                var movedJudgeIds = await context.JudgeMovements
                    .AsNoTracking()
                    .Where(m => (m.CircuitId.HasValue && allowedCircuitIds.Contains(m.CircuitId.Value)) ||
                                (m.FromCircuitId.HasValue && allowedCircuitIds.Contains(m.FromCircuitId.Value)) ||
                                (m.ToCircuitId.HasValue && allowedCircuitIds.Contains(m.ToCircuitId.Value)))
                    .Select(m => m.JudgeId)
                    .Distinct()
                    .ToListAsync();

                var eligibleJudgeIds = assignedJudgeIds.Union(movedJudgeIds).ToHashSet();
                query = query.Where(j => eligibleJudgeIds.Contains(j.Id));
            }
            else if (judicialYearId.HasValue && judicialYearId.Value > 0)
            {
                // SystemAdmin مع تحديد عام قضائي: عرض القضاة العاملين في ذلك العام
                var assignedJudgeIds = await context.CircuitJudgeAssignments
                    .AsNoTracking()
                    .Where(a => a.CircuitVersion.Circuit.JudicialYearId == judicialYearId.Value)
                    .Select(a => a.JudgeId)
                    .Distinct()
                    .ToListAsync();

                var movedJudgeIds = await context.JudgeMovements
                    .AsNoTracking()
                    .Where(m => (m.Circuit != null && m.Circuit.JudicialYearId == judicialYearId.Value) ||
                                (m.FromCircuit != null && m.FromCircuit.JudicialYearId == judicialYearId.Value) ||
                                (m.ToCircuit != null && m.ToCircuit.JudicialYearId == judicialYearId.Value))
                    .Select(m => m.JudgeId)
                    .Distinct()
                    .ToListAsync();

                var eligibleJudgeIds = assignedJudgeIds.Union(movedJudgeIds).ToHashSet();
                query = query.Where(j => eligibleJudgeIds.Contains(j.Id));
            }
        }
        else if (judicialYearId.HasValue && judicialYearId.Value > 0)
        {
            // بدون تحديد مستخدم ولكن مع تحديد عام قضائي
            var assignedJudgeIds = await context.CircuitJudgeAssignments
                .AsNoTracking()
                .Where(a => a.CircuitVersion.Circuit.JudicialYearId == judicialYearId.Value)
                .Select(a => a.JudgeId)
                .Distinct()
                .ToListAsync();

            var movedJudgeIds = await context.JudgeMovements
                .AsNoTracking()
                .Where(m => (m.Circuit != null && m.Circuit.JudicialYearId == judicialYearId.Value) ||
                            (m.FromCircuit != null && m.FromCircuit.JudicialYearId == judicialYearId.Value) ||
                            (m.ToCircuit != null && m.ToCircuit.JudicialYearId == judicialYearId.Value))
                .Select(m => m.JudgeId)
                .Distinct()
                .ToListAsync();

            var eligibleJudgeIds = assignedJudgeIds.Union(movedJudgeIds).ToHashSet();
            query = query.Where(j => eligibleJudgeIds.Contains(j.Id));
        }

        return await query
            .OrderBy(j => j.FullNameAr)
            .Select(j => new JudgeDto(
                j.Id,
                j.Code,
                j.FullNameAr,
                j.IsActive,
                j.CreatedCourtUnitId,
                j.CreatedCourtUnit != null ? j.CreatedCourtUnit.NameAr : null
            ))
            .ToListAsync();
    }

    public async Task<long> CreateJudgeAsync(JudgeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var trimmedCode = cmd.Code.Trim();
        var trimmedName = cmd.FullNameAr.Trim();

        if (await context.Judges.AnyAsync(j => j.Code == trimmedCode))
        {
            throw new InvalidOperationException($"كود القاضي '{trimmedCode}' مسجل مسبقاً.");
        }

        if (await context.Judges.AnyAsync(j => j.FullNameAr == trimmedName))
        {
            throw new InvalidOperationException($"اسم القاضي '{trimmedName}' مسجل مسبقاً بالنظام ولا يسمح بتكرار الأسماء.");
        }

        var judge = new Judge
        {
            Code = trimmedCode,
            FullNameAr = trimmedName,
            IsActive = true,
            CreatedCourtUnitId = cmd.CreatedCourtUnitId
        };

        context.Judges.Add(judge);
        await context.SaveChangesAsync();
        return judge.Id;
    }

    public async Task UpdateJudgeAsync(JudgeUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var judge = await context.Judges
            .Include(j => j.CreatedCourtUnit)
            .FirstOrDefaultAsync(j => j.Id == cmd.Id)
            ?? throw new KeyNotFoundException($"القاضي رقم {cmd.Id} غير موجود.");

        // التحقق من حصرية التعديل للمحكمة المنشئة
        if (judge.CreatedCourtUnitId.HasValue &&
            cmd.RequestingCourtUnitId.HasValue &&
            judge.CreatedCourtUnitId.Value != cmd.RequestingCourtUnitId.Value &&
            !cmd.IsSuperAdmin)
        {
            var courtName = judge.CreatedCourtUnit?.NameAr ?? "المحكمة المنشئة";
            throw new UnauthorizedAccessException($"لا يمكن تعديل بيانات هذا القاضي إلا من قبل محكمته المنشئة ({courtName}).");
        }

        var trimmedCode = cmd.Code.Trim();
        var trimmedName = cmd.FullNameAr.Trim();

        if (await context.Judges.AnyAsync(j => j.Id != cmd.Id && j.Code == trimmedCode))
        {
            throw new InvalidOperationException($"كود القاضي '{trimmedCode}' مسجل مسبقاً لقاضٍ آخر.");
        }

        if (await context.Judges.AnyAsync(j => j.Id != cmd.Id && j.FullNameAr == trimmedName))
        {
            throw new InvalidOperationException($"اسم القاضي '{trimmedName}' مسجل مسبقاً بالنظام ولا يسمح بتكرار الأسماء.");
        }

        judge.Code = trimmedCode;
        judge.FullNameAr = trimmedName;
        judge.IsActive = cmd.IsActive;

        await context.SaveChangesAsync();
    }


    public async Task<List<JudicialRoleDto>> GetJudicialRolesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.JudicialRoles
            .OrderBy(r => r.SortOrder)
            .Select(r => new JudicialRoleDto(
                r.Id,
                r.Code,
                r.NameAr,
                r.SortOrder
            ))
            .ToListAsync();
    }

    public async Task AssignJudgeAsync(CircuitJudgeAssignCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var assignment = new CircuitJudgeAssignment
        {
            CircuitVersionId = cmd.CircuitVersionId,
            JudgeId = cmd.JudgeId,
            JudicialRoleId = cmd.JudicialRoleId,
            EffectiveFrom = cmd.EffectiveFrom,
            EffectiveTo = cmd.EffectiveTo,
            DisplayOrder = cmd.DisplayOrder
        };

        context.CircuitJudgeAssignments.Add(assignment);
        await context.SaveChangesAsync();
    }

    public async Task<List<CircuitJudgeAssignmentDto>> GetAssignmentsForYearAsync(long judicialYearId, long? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.CircuitJudgeAssignments
            .Include(a => a.Judge)
            .Include(a => a.JudicialRole)
            .Include(a => a.CircuitVersion)
                .ThenInclude(cv => cv.Circuit)
                    .ThenInclude(c => c.CourtUnit)
            .Where(a => a.CircuitVersion.Circuit.JudicialYearId == judicialYearId);

        if (userId.HasValue && userId.Value > 0)
        {
            var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId.Value);
            if (!scope.IsSystemAdmin)
            {
                if (scope.AllowedSessionIds != null)
                {
                    var allowedCircuitIds = await context.Sessions
                        .AsNoTracking()
                        .Where(s => scope.AllowedSessionIds.Contains(s.Id) && s.Circuit.JudicialYearId == judicialYearId)
                        .Select(s => s.CircuitId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(a => allowedCircuitIds.Contains(a.CircuitVersion.CircuitId));
                }
                else if (scope.AllowedCourtIds != null)
                {
                    query = query.Where(a => scope.AllowedCourtIds.Contains(a.CircuitVersion.Circuit.CourtUnitId) ||
                        (a.CircuitVersion.Circuit.CourtUnit.ParentCourtUnitId != null && scope.AllowedCourtIds.Contains(a.CircuitVersion.Circuit.CourtUnit.ParentCourtUnitId.Value)));
                }
                else if (scope.PrimaryCourtId.HasValue)
                {
                    query = query.Where(a => a.CircuitVersion.Circuit.CourtUnitId == scope.PrimaryCourtId.Value ||
                        a.CircuitVersion.Circuit.CourtUnit.ParentCourtUnitId == scope.PrimaryCourtId.Value);
                }
            }
        }

        return await query
            .OrderBy(a => a.CircuitVersion.Circuit.Code)
            .ThenBy(a => a.DisplayOrder)
            .Select(a => new CircuitJudgeAssignmentDto(
                a.Id,
                a.CircuitVersionId,
                a.JudgeId,
                a.Judge.FullNameAr,
                a.JudicialRoleId,
                a.JudicialRole.NameAr,
                a.EffectiveFrom,
                a.EffectiveTo,
                a.DisplayOrder,
                a.CircuitVersion.CircuitId,
                a.CircuitVersion.Circuit.NameAr
            ))
            .ToListAsync();
    }

    public async Task<List<CircuitJudgeAssignmentDto>> GetJudgeAssignmentsForSessionVersionAsync(long sessionVersionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sv = await context.SessionVersions
            .Include(s => s.CircuitVersion)
                .ThenInclude(cv => cv.Circuit)
            .Include(s => s.CircuitVersion)
                .ThenInclude(cv => cv.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(s => s.CircuitVersion)
                .ThenInclude(cv => cv.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .FirstOrDefaultAsync(s => s.Id == sessionVersionId);

        if (sv == null) return new List<CircuitJudgeAssignmentDto>();

        return sv.CircuitVersion.JudgeAssignments
            .OrderBy(ja => ja.DisplayOrder)
            .Select(ja => new CircuitJudgeAssignmentDto(
                ja.Id,
                ja.CircuitVersionId,
                ja.JudgeId,
                ja.Judge.FullNameAr,
                ja.JudicialRoleId,
                ja.JudicialRole.NameAr,
                ja.EffectiveFrom,
                ja.EffectiveTo,
                ja.DisplayOrder,
                sv.CircuitVersion.CircuitId,
                sv.CircuitVersion.Circuit?.NameAr ?? string.Empty
            ))
            .ToList();
    }

    // ======================== Geography ========================

    public async Task<List<GeographicAreaDto>> GetGeographicAreasAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GeographicAreas
            .OrderBy(g => g.Code)
            .Select(g => new GeographicAreaDto(
                g.Id,
                g.ParentAreaId,
                g.Code,
                g.NameAr,
                g.AreaKind
            ))
            .ToListAsync();
    }

    // ======================== Cascading & Scoped Selection ========================

    public async Task<List<JudicialStatistics.Application.Courts.CourtUnitDto>> GetAuthorizedCourtsForDisciplineAsync(
        long judicialYearId,
        string disciplineCode,
        long userId,
        long? primaryCourtId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var allowedKinds = StatisticalDisciplines.GetCircuitKinds(disciplineCode);

        var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId);

        // المحاكم والمأموريات العاملة التابعة للمحكمة الابتدائية
        var query = context.CourtUnits
            .AsNoTracking()
            .Where(c => c.IsActive && c.ParentCourtUnitId != null);

        var targetPrimaryId = primaryCourtId ?? scope.PrimaryCourtId;
        if (targetPrimaryId.HasValue && targetPrimaryId.Value > 0)
        {
            query = query.Where(c => c.ParentCourtUnitId == targetPrimaryId.Value);
        }

        if (!scope.IsSystemAdmin && !scope.IsScopeAdmin)
        {
            if (scope.AllowedCourtIds != null)
            {
                query = query.Where(c => scope.AllowedCourtIds.Contains(c.Id));
            }
        }

        // المحكمة يجب أن تحتوي على دوائر/جلسات مطابقة للنوع المحدد للعام القضائي
        if (scope.AllowedSessionIds != null)
        {
            // للمستخدم المقيد بجلسات محددة: يجب أن تحتوي المحكمة على إحدى جلساته المصرح بها والمطابقة للنوع
            query = query.Where(court => context.Sessions.Any(s =>
                scope.AllowedSessionIds.Contains(s.Id) &&
                s.Circuit.CourtUnitId == court.Id &&
                s.Circuit.JudicialYearId == judicialYearId &&
                !s.Circuit.IsClosed &&
                !s.IsClosed &&
                s.Circuit.Versions.Any(cv => allowedKinds.Contains(cv.CircuitKind))));
        }
        else
        {
            query = query.Where(court => context.Circuits.Any(cir =>
                cir.CourtUnitId == court.Id &&
                cir.JudicialYearId == judicialYearId &&
                !cir.IsClosed &&
                cir.Versions.Any(cv => allowedKinds.Contains(cv.CircuitKind))));
        }

        var courts = await query
            .OrderBy(c => c.UnitKind == "PlenaryHeadquarters" ? 1 : (c.UnitKind == "SummaryCourt" ? 2 : 3))
            .ThenBy(c => c.Code)
            .Select(c => new JudicialStatistics.Application.Courts.CourtUnitDto(
                c.Id,
                c.ParentCourtUnitId,
                c.Code,
                c.NameAr,
                c.UnitKind,
                c.IsActive,
                new List<JudicialStatistics.Application.Courts.CourtUnitDto>()
            ))
            .ToListAsync();

        return courts;
    }

    public async Task<List<CircuitDto>> GetAuthorizedCircuitsForDisciplineAsync(
        long judicialYearId,
        long courtUnitId,
        string disciplineCode,
        long userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var allowedKinds = StatisticalDisciplines.GetCircuitKinds(disciplineCode);

        var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId);

        if (!scope.IsSystemAdmin && !scope.IsScopeAdmin)
        {
            if (scope.AllowedCourtIds != null && !scope.AllowedCourtIds.Contains(courtUnitId))
            {
                return new List<CircuitDto>();
            }
        }

        var query = context.Circuits
            .AsNoTracking()
            .Include(c => c.CourtUnit)
            .Include(c => c.Sessions)
            .Where(c => c.JudicialYearId == judicialYearId &&
                        c.CourtUnitId == courtUnitId &&
                        !c.IsClosed &&
                        c.Versions.Any(cv => allowedKinds.Contains(cv.CircuitKind)));

        // إذا كان المستخدم مقيداً بجلسات معينة، لا نعرض سوى الدوائر التي تحوي إحدى جلساته المصرح بها
        if (scope.AllowedSessionIds != null)
        {
            query = query.Where(c => c.Sessions.Any(s => scope.AllowedSessionIds.Contains(s.Id)));
        }

        // تصفية إضافية حسب التخصص للدوائر داخل المحكمة
        if (disciplineCode == StatisticalDisciplines.Civil)
        {
            query = query.Where(c => c.Versions.Any(cv => cv.CircuitKind == "CivilFull") ||
                                     c.Sessions.Any(s => s.Code.Contains("-CIV-") || s.NameAr.Contains("مدني")));
        }
        else if (disciplineCode == StatisticalDisciplines.Criminal)
        {
            query = query.Where(c => c.Versions.Any(cv => cv.CircuitKind == "CriminalAppeal") ||
                                     c.Sessions.Any(s => !s.Code.Contains("-CIV-") && !s.NameAr.Contains("مدني")));
        }

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new CircuitDto(
                c.Id,
                c.JudicialYearId,
                c.CourtUnitId,
                c.CourtUnit.NameAr,
                c.Code,
                c.NameAr,
                c.IsClosed,
                c.Sessions.Count,
                c.Versions.OrderByDescending(v => v.EffectiveFrom).Select(v => v.CircuitKind).FirstOrDefault() ?? ""
            ))
            .ToListAsync();
    }

    public async Task<List<SessionVersionDto>> GetAuthorizedSessionVersionsForDisciplineAsync(
        long judicialYearId,
        long circuitId,
        string disciplineCode,
        long userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var allowedKinds = StatisticalDisciplines.GetCircuitKinds(disciplineCode);

        var circuit = await context.Circuits
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == circuitId);

        if (circuit == null) return new List<SessionVersionDto>();

        var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId);

        if (!scope.IsSystemAdmin && !scope.IsScopeAdmin)
        {
            if (scope.AllowedCourtIds != null && !scope.AllowedCourtIds.Contains(circuit.CourtUnitId))
            {
                return new List<SessionVersionDto>();
            }
        }

        var query = context.SessionVersions
            .AsNoTracking()
            .Include(sv => sv.Session)
            .Include(sv => sv.CircuitVersion).ThenInclude(cv => cv.Circuit)
            .Include(sv => sv.Jurisdictions).ThenInclude(j => j.GeographicArea)
            .Where(sv => sv.CircuitVersion.CircuitId == circuitId &&
                        sv.CircuitVersion.Circuit.JudicialYearId == judicialYearId &&
                        !sv.Session.IsClosed &&
                        allowedKinds.Contains(sv.CircuitVersion.CircuitKind));

        if (disciplineCode == StatisticalDisciplines.Civil)
        {
            query = query.Where(sv => sv.CircuitVersion.CircuitKind == "CivilFull" ||
                                      sv.Session.Code.Contains("-CIV-") ||
                                      sv.Session.NameAr.Contains("مدني"));
        }
        else if (disciplineCode == StatisticalDisciplines.Criminal)
        {
            query = query.Where(sv => sv.CircuitVersion.CircuitKind == "CriminalAppeal" ||
                                      (!sv.Session.Code.Contains("-CIV-") && !sv.Session.NameAr.Contains("مدني") && sv.CircuitVersion.CircuitKind == "Partial"));
        }

        // تطبيق قيد الجلسات المصرح بها حصراً
        if (scope.AllowedSessionIds != null)
        {
            query = query.Where(sv => scope.AllowedSessionIds.Contains(sv.SessionId));
        }

        return await query
            .OrderBy(sv => sv.Session.Code)
            .Select(sv => new SessionVersionDto(
                sv.Id,
                sv.SessionId,
                sv.CircuitVersionId,
                sv.CircuitVersion.Circuit.NameAr,
                sv.EffectiveFrom,
                sv.EffectiveTo,
                sv.ScheduleDescription ?? sv.Session.NameAr,
                sv.Jurisdictions.Select(j => j.GeographicArea.NameAr).ToList()
            ))
            .ToListAsync();
    }

    public async Task<CircuitFormationFullViewDto?> GetCircuitFormationFullViewAsync(long circuitId, long? periodId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var circuit = await context.Circuits
            .AsNoTracking()
            .Include(c => c.CourtUnit)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Employee)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Versions)
                    .ThenInclude(sv => sv.Jurisdictions)
                        .ThenInclude(j => j.GeographicArea)
            .FirstOrDefaultAsync(c => c.Id == circuitId);

        if (circuit == null) return null;

        var latestVersion = circuit.Versions.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault();
        var circuitKind = latestVersion?.CircuitKind ?? "CivilFull";
        short minJudges = latestVersion?.MinJudges ?? 3;
        short maxJudges = latestVersion?.MaxJudges ?? 4;

        // استخراج الجلسات التابعة
        var sessions = new List<SessionVersionDto>();
        foreach (var s in circuit.Sessions.Where(s => !s.IsClosed))
        {
            var sv = s.Versions.OrderByDescending(v => v.EffectiveFrom).FirstOrDefault();
            if (sv != null)
            {
                sessions.Add(new SessionVersionDto(
                    sv.Id,
                    sv.SessionId,
                    sv.CircuitVersionId,
                    circuit.NameAr,
                    sv.EffectiveFrom,
                    sv.EffectiveTo,
                    sv.ScheduleDescription ?? s.NameAr,
                    sv.Jurisdictions.Select(j => j.GeographicArea.NameAr).ToList(),
                    sv.Jurisdictions.Select(j => j.GeographicAreaId).ToList(),
                    null,
                    s.EmployeeId,
                    s.Employee?.FullNameAr,
                    s.Employee?.JobTitle
                ));
            }
        }

        // استخراج كافة القضاة الذين كُلفوا أو عملوا بالدائرة خلال العام
        var judges = new List<CircuitJudgeAssignmentDto>();
        if (latestVersion != null)
        {
            foreach (var ja in latestVersion.JudgeAssignments.OrderBy(a => a.DisplayOrder))
            {
                string status = ja.Judge.IsActive ? "على رأس العمل" : "لا يعمل (إجازة مرضي)";
                string? notes = null;

                judges.Add(new CircuitJudgeAssignmentDto(
                    ja.Id,
                    ja.CircuitVersionId,
                    ja.JudgeId,
                    ja.Judge.FullNameAr,
                    ja.JudicialRoleId,
                    ja.JudicialRole.NameAr,
                    ja.EffectiveFrom,
                    ja.EffectiveTo,
                    ja.DisplayOrder,
                    circuit.Id,
                    circuit.NameAr,
                    status,
                    notes
                ));
            }
        }

        // القرارات والحركات المؤثرة على التشكيل
        var decisions = new List<CircuitDecisionImpactDto>
        {
            new(
                "قرار تشكيل قضائي",
                "قرار رئيس المحكمة الابتدائية رقم 1",
                latestVersion?.EffectiveFrom ?? new DateOnly(2025, 10, 1),
                $"اعتماد التشكيل الرسمي لـ {circuit.NameAr} وتوزيع العمل القضائي للعام القضائي.",
                "ساري ومعتمد"
            )
        };

        var judgeIds = judges.Select(j => j.JudgeId).ToList();
        var movements = await context.JudgeMovements
            .Include(m => m.Judge)
            .Include(m => m.FromCircuit)
            .Include(m => m.ToCircuit)
            .Include(m => m.JudicialRole)
            .Where(m => (m.CircuitId.HasValue && m.CircuitId.Value == circuit.Id)
                     || (m.FromCircuitId.HasValue && m.FromCircuitId.Value == circuit.Id)
                     || (m.ToCircuitId.HasValue && m.ToCircuitId.Value == circuit.Id)
                     || judgeIds.Contains(m.JudgeId))
            .OrderByDescending(m => m.DecisionDate)
            .ThenByDescending(m => m.EffectiveFrom)
            .ToListAsync();

        DateOnly targetStart = new DateOnly(2025, 10, 1);
        DateOnly targetEnd = new DateOnly(2026, 9, 30);
        if (periodId.HasValue)
        {
            var period = await context.StatisticalPeriods.FindAsync(periodId.Value);
            if (period != null)
            {
                targetStart = period.MonthStartDate;
                targetEnd = targetStart.AddMonths(1).AddDays(-1);
            }
        }

        var activeMovForCircuit = movements
            .Where(m => (m.ToCircuitId == circuit.Id || m.FromCircuitId == circuit.Id || m.CircuitId == circuit.Id)
                     && m.EffectiveFrom <= targetEnd
                     && (!m.EffectiveTo.HasValue || m.EffectiveTo.Value >= targetStart))
            .OrderBy(m => m.DecisionDate)
            .ThenBy(m => m.EffectiveFrom)
            .LastOrDefault();

        if (activeMovForCircuit != null)
        {
            string? snapJson = (activeMovForCircuit.ToCircuitId == circuit.Id)
                ? activeMovForCircuit.ToCircuitOrderJson
                : activeMovForCircuit.FromCircuitOrderJson;

            if (!string.IsNullOrWhiteSpace(snapJson))
            {
                var parsed = ParseJudgesOrder(snapJson);
                if (parsed.Count > 0)
                {
                    judges = parsed.OrderBy(p => p.DisplayOrder).Select(p => new CircuitJudgeAssignmentDto(
                        0,
                        latestVersion?.Id ?? 0,
                        p.JudgeId,
                        p.JudgeNameAr,
                        p.JudicialRoleId,
                        p.JudicialRoleNameAr,
                        activeMovForCircuit.EffectiveFrom,
                        activeMovForCircuit.EffectiveTo ?? targetEnd,
                        p.DisplayOrder,
                        circuit.Id,
                        circuit.NameAr,
                        "على رأس العمل (معدل بقرار)",
                        activeMovForCircuit.EffectiveFrom > targetStart ? $"سارٍ من {activeMovForCircuit.EffectiveFrom:M/d}" : null
                    )).ToList();
                }
            }
        }

        foreach (var mov in movements)
        {
            var movTypeAr = mov.MovementType switch
            {
                "Transfer" => "نقل قضائي",
                "Delegation" => "ندب قضائي",
                "Leave" => "إجازة اعتيادية",
                "SickLeave" => "إجازة مرضية",
                _ => mov.MovementType
            };

            var today = DateOnly.FromDateTime(DateTime.Today);
            var isCurrent = mov.EffectiveFrom <= today && (!mov.EffectiveTo.HasValue || mov.EffectiveTo.Value >= today);

            string desc = $"المستشار: {mov.Judge.FullNameAr}";
            if (mov.FromCircuit != null && mov.ToCircuit != null)
            {
                desc += $" — من {mov.FromCircuit.NameAr} إلى {mov.ToCircuit.NameAr}";
            }
            if (mov.JudicialRole != null)
            {
                desc += $" كـ ({mov.JudicialRole.NameAr})";
            }
            desc += $" — {mov.Reason}";
            if (!string.IsNullOrWhiteSpace(mov.Notes)) desc += $" ({mov.Notes})";

            decisions.Add(new(
                movTypeAr,
                $"قرار رقم {mov.DecisionNumber}",
                mov.DecisionDate,
                desc,
                isCurrent ? "ساري ومؤثر على التشكيل" : "منتهي الأثر"
            ));
        }

        return new CircuitFormationFullViewDto(
            circuit.Id,
            circuit.Code,
            circuit.NameAr,
            circuit.CourtUnit?.NameAr ?? "غير محدد",
            circuitKind,
            minJudges,
            maxJudges,
            circuit.IsClosed,
            sessions,
            judges,
            decisions
        );
    }

    // ======================== Geography ========================

    public async Task<List<GeographicAreaDto>> GetGeographicAreasAsync(bool activeOnly = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.GeographicAreas
            .Include(g => g.ParentArea)
            .AsNoTracking()
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(g => g.IsActive);
        }

        return await query
            .OrderBy(g => g.Code)
            .Select(g => new GeographicAreaDto(
                g.Id,
                g.ParentAreaId,
                g.Code,
                g.NameAr,
                g.AreaKind,
                g.IsActive,
                g.ParentArea != null ? g.ParentArea.NameAr : null
            ))
            .ToListAsync();
    }

    public async Task<long> CreateGeographicAreaAsync(GeographicAreaCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var exists = await context.GeographicAreas.AnyAsync(g => g.Code == cmd.Code.Trim());
        if (exists)
        {
            throw new InvalidOperationException($"كود النطاق الجغرافي '{cmd.Code}' مسجل بالفعل.");
        }

        var area = new GeographicArea
        {
            ParentAreaId = cmd.ParentAreaId,
            Code = cmd.Code.Trim(),
            NameAr = cmd.NameAr.Trim(),
            AreaKind = cmd.AreaKind,
            IsActive = true
        };

        context.GeographicAreas.Add(area);
        await context.SaveChangesAsync();
        return area.Id;
    }

    public async Task UpdateGeographicAreaAsync(GeographicAreaUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var area = await context.GeographicAreas.FindAsync(cmd.Id);
        if (area == null) throw new InvalidOperationException("النطاق الجغرافي غير موجود.");

        area.NameAr = cmd.NameAr.Trim();
        area.IsActive = cmd.IsActive;
        await context.SaveChangesAsync();
    }

    public async Task ToggleGeographicAreaActiveAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var area = await context.GeographicAreas.FindAsync(id);
        if (area == null) throw new InvalidOperationException("النطاق الجغرافي غير موجود.");

        area.IsActive = !area.IsActive;
        await context.SaveChangesAsync();
    }

    // ======================== Judge Movements (Immutable) ========================

    public async Task<List<JudgeMovementDto>> GetJudgeMovementsAsync(long? judgeId = null, long? circuitId = null, long? judicialYearId = null, long? userId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JudgeMovements
            .Include(m => m.Judge)
            .Include(m => m.Circuit)
            .Include(m => m.FromCircuit)
            .Include(m => m.ToCircuit)
            .Include(m => m.JudicialRole)
            .AsNoTracking()
            .AsQueryable();

        if (judgeId.HasValue)
            query = query.Where(m => m.JudgeId == judgeId.Value);

        if (circuitId.HasValue)
            query = query.Where(m => m.CircuitId == circuitId.Value || m.FromCircuitId == circuitId.Value || m.ToCircuitId == circuitId.Value);

        if (judicialYearId.HasValue && judicialYearId.Value > 0)
        {
            query = query.Where(m => (m.Circuit != null && m.Circuit.JudicialYearId == judicialYearId.Value) ||
                                     (m.FromCircuit != null && m.FromCircuit.JudicialYearId == judicialYearId.Value) ||
                                     (m.ToCircuit != null && m.ToCircuit.JudicialYearId == judicialYearId.Value));
        }

        if (userId.HasValue && userId.Value > 0)
        {
            var scope = await UserScopeHelper.ResolveUserScopeAsync(context, userId.Value);
            if (!scope.IsSystemAdmin)
            {
                HashSet<long> allowedCircuitIds = new();
                if (scope.AllowedSessionIds != null)
                {
                    var sQuery = context.Sessions.AsNoTracking().Where(s => scope.AllowedSessionIds.Contains(s.Id));
                    if (judicialYearId.HasValue && judicialYearId.Value > 0)
                    {
                        sQuery = sQuery.Where(s => s.Circuit.JudicialYearId == judicialYearId.Value);
                    }
                    var cIds = await sQuery.Select(s => s.CircuitId).Distinct().ToListAsync();
                    allowedCircuitIds = cIds.ToHashSet();
                }
                else if (scope.AllowedCourtIds != null)
                {
                    var cQuery = context.Circuits.AsNoTracking()
                        .Where(c => scope.AllowedCourtIds.Contains(c.CourtUnitId) ||
                                   (c.CourtUnit.ParentCourtUnitId != null && scope.AllowedCourtIds.Contains(c.CourtUnit.ParentCourtUnitId.Value)));
                    if (judicialYearId.HasValue && judicialYearId.Value > 0)
                    {
                        cQuery = cQuery.Where(c => c.JudicialYearId == judicialYearId.Value);
                    }
                    var cIds = await cQuery.Select(c => c.Id).Distinct().ToListAsync();
                    allowedCircuitIds = cIds.ToHashSet();
                }
                else if (scope.PrimaryCourtId.HasValue)
                {
                    var cQuery = context.Circuits.AsNoTracking()
                        .Where(c => c.CourtUnitId == scope.PrimaryCourtId.Value ||
                                   c.CourtUnit.ParentCourtUnitId == scope.PrimaryCourtId.Value);
                    if (judicialYearId.HasValue && judicialYearId.Value > 0)
                    {
                        cQuery = cQuery.Where(c => c.JudicialYearId == judicialYearId.Value);
                    }
                    var cIds = await cQuery.Select(c => c.Id).Distinct().ToListAsync();
                    allowedCircuitIds = cIds.ToHashSet();
                }

                query = query.Where(m => (m.CircuitId.HasValue && allowedCircuitIds.Contains(m.CircuitId.Value)) ||
                                         (m.FromCircuitId.HasValue && allowedCircuitIds.Contains(m.FromCircuitId.Value)) ||
                                         (m.ToCircuitId.HasValue && allowedCircuitIds.Contains(m.ToCircuitId.Value)));
            }
        }

        return await query
            .OrderByDescending(m => m.DecisionDate)
            .ThenByDescending(m => m.EffectiveFrom)
            .ThenByDescending(m => m.CreatedAt)
            .Select(m => new JudgeMovementDto(
                m.Id,
                m.JudgeId,
                m.Judge.FullNameAr,
                m.CircuitId,
                m.Circuit != null ? m.Circuit.NameAr : null,
                m.FromCircuitId,
                m.FromCircuit != null ? m.FromCircuit.NameAr : null,
                m.ToCircuitId,
                m.ToCircuit != null ? m.ToCircuit.NameAr : null,
                m.JudicialRoleId,
                m.JudicialRole != null ? m.JudicialRole.NameAr : null,
                m.FromCircuitOrderJson,
                m.ToCircuitOrderJson,
                m.MovementType,
                m.DecisionNumber,
                m.DecisionDate,
                m.EffectiveFrom,
                m.EffectiveTo,
                m.Reason,
                m.Notes,
                m.CreatedAt
            ))
            .ToListAsync();
    }

    public async Task<long> CreateJudgeMovementAsync(JudgeMovementCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        string? fromOrderJson = cmd.FromCircuitJudges is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(cmd.FromCircuitJudges)
            : null;

        string? toOrderJson = cmd.ToCircuitJudges is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(cmd.ToCircuitJudges)
            : null;

        var targetCircuitId = cmd.ToCircuitId ?? cmd.FromCircuitId ?? cmd.CircuitId;

        var movement = new JudgeMovement
        {
            JudgeId = cmd.JudgeId,
            CircuitId = targetCircuitId,
            FromCircuitId = cmd.FromCircuitId,
            ToCircuitId = cmd.ToCircuitId,
            JudicialRoleId = cmd.JudicialRoleId,
            FromCircuitOrderJson = fromOrderJson,
            ToCircuitOrderJson = toOrderJson,
            MovementType = cmd.MovementType.Trim(),
            DecisionNumber = cmd.DecisionNumber.Trim(),
            DecisionDate = cmd.DecisionDate,
            EffectiveFrom = cmd.EffectiveFrom,
            EffectiveTo = cmd.EffectiveTo,
            Reason = cmd.Reason.Trim(),
            Notes = cmd.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        context.JudgeMovements.Add(movement);

        // تحديث تكليفات الدائرة المنقول منها
        if (cmd.FromCircuitId.HasValue)
        {
            var fromCv = await context.CircuitVersions
                .Include(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
                .Include(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
                .Where(v => v.CircuitId == cmd.FromCircuitId.Value)
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (fromCv != null)
            {
                if (cmd.MovementType.Equals("Transfer", StringComparison.OrdinalIgnoreCase))
                {
                    var transferredAssignment = fromCv.JudgeAssignments.FirstOrDefault(a => a.JudgeId == cmd.JudgeId);
                    if (transferredAssignment != null)
                    {
                        transferredAssignment.EffectiveTo = cmd.EffectiveFrom.AddDays(-1);
                    }
                }

                if (cmd.FromCircuitJudges is { Count: > 0 })
                {
                    foreach (var item in cmd.FromCircuitJudges)
                    {
                        var existing = fromCv.JudgeAssignments.FirstOrDefault(a => a.JudgeId == item.JudgeId);
                        if (existing != null)
                        {
                            existing.DisplayOrder = item.DisplayOrder;
                            existing.JudicialRoleId = item.JudicialRoleId;
                        }
                    }
                }
                else
                {
                    // التحديث والترقية التلقائية للقضاة المتبقين وصعودهم دون الحاجة لتدخل يدوي
                    var remaining = fromCv.JudgeAssignments
                        .Where(a => a.JudgeId != cmd.JudgeId && a.EffectiveTo >= cmd.EffectiveFrom)
                        .OrderBy(a => a.DisplayOrder)
                        .ToList();

                    short ord = 1;
                    var autoList = new List<CircuitJudgeOrderItem>();
                    foreach (var r in remaining)
                    {
                        r.DisplayOrder = ord++;
                        autoList.Add(new CircuitJudgeOrderItem(
                            r.JudgeId,
                            r.Judge?.FullNameAr ?? "قاضٍ",
                            r.JudicialRoleId,
                            r.JudicialRole?.NameAr ?? "عضو",
                            r.DisplayOrder
                        ));
                    }
                    if (string.IsNullOrEmpty(movement.FromCircuitOrderJson))
                    {
                        movement.FromCircuitOrderJson = System.Text.Json.JsonSerializer.Serialize(autoList);
                    }
                }
            }
        }

        // تحديث تكليفات الدائرة المنقول إليها
        if (cmd.ToCircuitId.HasValue && cmd.ToCircuitJudges is { Count: > 0 })
        {
            var toCv = await context.CircuitVersions
                .Include(v => v.JudgeAssignments)
                .Where(v => v.CircuitId == cmd.ToCircuitId.Value)
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (toCv != null)
            {
                foreach (var item in cmd.ToCircuitJudges)
                {
                    var existing = toCv.JudgeAssignments.FirstOrDefault(a => a.JudgeId == item.JudgeId);
                    if (existing != null)
                    {
                        existing.DisplayOrder = item.DisplayOrder;
                        existing.JudicialRoleId = item.JudicialRoleId;
                    }
                    else
                    {
                        toCv.JudgeAssignments.Add(new CircuitJudgeAssignment
                        {
                            CircuitVersionId = toCv.Id,
                            JudgeId = item.JudgeId,
                            JudicialRoleId = item.JudicialRoleId,
                            EffectiveFrom = cmd.EffectiveFrom,
                            EffectiveTo = cmd.EffectiveTo ?? toCv.EffectiveTo,
                            DisplayOrder = item.DisplayOrder
                        });
                    }
                }
            }
        }

        await context.SaveChangesAsync();
        return movement.Id;
    }

    public async Task<List<CircuitJudgeOrderItem>> GetCircuitCurrentJudgesForMovementAsync(long circuitId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cv = await context.CircuitVersions
            .Include(v => v.JudgeAssignments)
                .ThenInclude(ja => ja.Judge)
            .Include(v => v.JudgeAssignments)
                .ThenInclude(ja => ja.JudicialRole)
            .Where(v => v.CircuitId == circuitId)
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (cv == null) return new List<CircuitJudgeOrderItem>();

        return cv.JudgeAssignments
            .OrderBy(ja => ja.DisplayOrder)
            .Select(ja => new CircuitJudgeOrderItem(
                ja.JudgeId,
                ja.Judge.FullNameAr,
                ja.JudicialRoleId,
                ja.JudicialRole?.NameAr ?? "عضو",
                ja.DisplayOrder
            ))
            .ToList();
    }

    public async Task<List<CircuitJudgeAssignmentDto>> GetActiveJudgesForSessionMonthAsync(long sessionVersionId, int year, int month)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sv = await context.SessionVersions
            .Include(s => s.CircuitVersion)
                .ThenInclude(cv => cv.Circuit)
            .Include(s => s.CircuitVersion)
                .ThenInclude(cv => cv.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(s => s.CircuitVersion)
                .ThenInclude(cv => cv.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .FirstOrDefaultAsync(s => s.Id == sessionVersionId);

        if (sv == null) return new List<CircuitJudgeAssignmentDto>();

        var circuitId = sv.CircuitVersion.CircuitId;
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var movements = await context.JudgeMovements
            .Include(m => m.Judge)
            .Include(m => m.JudicialRole)
            .Where(m => (m.CircuitId == circuitId || m.FromCircuitId == circuitId || m.ToCircuitId == circuitId)
                     && m.EffectiveFrom <= monthEnd
                     && (!m.EffectiveTo.HasValue || m.EffectiveTo.Value >= monthStart))
            .OrderBy(m => m.DecisionDate)
            .ThenBy(m => m.EffectiveFrom)
            .ToListAsync();

        var baseAssignments = sv.CircuitVersion.JudgeAssignments
            .OrderBy(ja => ja.DisplayOrder)
            .Select(ja => new CircuitJudgeAssignmentDto(
                ja.Id,
                ja.CircuitVersionId,
                ja.JudgeId,
                ja.Judge.FullNameAr,
                ja.JudicialRoleId,
                ja.JudicialRole?.NameAr ?? "عضو",
                ja.EffectiveFrom,
                ja.EffectiveTo,
                ja.DisplayOrder,
                circuitId,
                sv.CircuitVersion.Circuit?.NameAr ?? string.Empty,
                "على رأس العمل",
                null
            ))
            .ToList();

        if (movements.Count == 0)
            return baseAssignments;

        var lastMov = movements.LastOrDefault();
        if (lastMov != null)
        {
            string? snapshotJson = null;
            if (lastMov.ToCircuitId == circuitId && !string.IsNullOrWhiteSpace(lastMov.ToCircuitOrderJson))
                snapshotJson = lastMov.ToCircuitOrderJson;
            else if (lastMov.FromCircuitId == circuitId && !string.IsNullOrWhiteSpace(lastMov.FromCircuitOrderJson))
                snapshotJson = lastMov.FromCircuitOrderJson;
            else if (lastMov.CircuitId == circuitId && !string.IsNullOrWhiteSpace(lastMov.FromCircuitOrderJson))
                snapshotJson = lastMov.FromCircuitOrderJson;

            if (!string.IsNullOrWhiteSpace(snapshotJson))
            {
                var parsed = ParseJudgesOrder(snapshotJson);
                if (parsed.Count > 0)
                {
                    var result = new List<CircuitJudgeAssignmentDto>();
                    foreach (var item in parsed.OrderBy(p => p.DisplayOrder))
                    {
                        string status = "على رأس العمل";
                        string? notes = null;

                        if (lastMov.EffectiveFrom > monthStart && lastMov.EffectiveFrom <= monthEnd && lastMov.JudgeId == item.JudgeId)
                        {
                            notes = $"من {lastMov.EffectiveFrom:M/d} إلى {monthEnd:M/d}";
                        }

                        result.Add(new CircuitJudgeAssignmentDto(
                            0,
                            sv.CircuitVersionId,
                            item.JudgeId,
                            item.JudgeNameAr,
                            item.JudicialRoleId,
                            item.JudicialRoleNameAr,
                            monthStart,
                            monthEnd,
                            item.DisplayOrder,
                            circuitId,
                            sv.CircuitVersion.Circuit?.NameAr ?? string.Empty,
                            status,
                            notes
                        ));
                    }

                    if (lastMov.EffectiveFrom > monthStart && (lastMov.FromCircuitId == circuitId || (lastMov.MovementType.Contains("Transfer") && lastMov.CircuitId == circuitId)))
                    {
                        var transferredJudge = baseAssignments.FirstOrDefault(b => b.JudgeId == lastMov.JudgeId);
                        if (transferredJudge != null && !result.Any(r => r.JudgeId == transferredJudge.JudgeId))
                        {
                            var earlyEnd = lastMov.EffectiveFrom.AddDays(-1);
                            result.Add(transferredJudge with
                            {
                                Notes = $"من {monthStart:M/d} إلى {earlyEnd:M/d}",
                                StatusThisMonth = "منقول جزئياً خلال الشهر"
                            });
                        }
                    }

                    return result;
                }
            }
        }

        return baseAssignments;
    }

    private static List<CircuitJudgeOrderItem> ParseJudgesOrder(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<CircuitJudgeOrderItem>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    // ======================== Disciplines Management ========================

    public async Task<List<StatisticalDisciplineDto>> GetDisciplinesAsync(bool activeOnly = false)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.StatisticalDisciplines.AsNoTracking().AsQueryable();
            if (activeOnly)
            {
                query = query.Where(d => d.IsActive);
            }

            var list = await query
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.NameAr)
                .Select(d => new StatisticalDisciplineDto(
                    d.Id,
                    d.Code,
                    d.NameAr,
                    d.Icon,
                    d.CircuitKinds,
                    d.EntryFormKind,
                    d.SortOrder,
                    d.IsActive
                ))
                .ToListAsync();

            if (list.Count > 0)
            {
                StatisticalDisciplines.Sync(list);
                return list;
            }
        }
        catch
        {
            // في حال تعذر القراءة أو عدم اكتمال الترحيل، يتم استخدام التخصصات الأساسية المعتمدة
        }

        // التخصصات الافتراضية الأساسية كحماية من توقف الواجهة
        return StatisticalDisciplines.All.Select((d, idx) => new StatisticalDisciplineDto(
            idx + 1,
            d.Code,
            d.NameAr,
            StatisticalDisciplines.GetIcon(d.Code),
            string.Join(",", StatisticalDisciplines.GetCircuitKinds(d.Code)),
            StatisticalDisciplines.GetEntryFormKind(d.Code),
            (short)(idx + 1),
            true
        )).ToList();
    }

    public async Task<long> CreateDisciplineAsync(StatisticalDisciplineCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var code = cmd.Code.Trim();
        if (await context.StatisticalDisciplines.AnyAsync(d => d.Code == code))
        {
            throw new InvalidOperationException($"كود التخصص '{code}' مسجل مسبقاً.");
        }

        var entity = new StatisticalDiscipline
        {
            Code = code,
            NameAr = cmd.NameAr.Trim(),
            Icon = string.IsNullOrWhiteSpace(cmd.Icon) ? "⚖️" : cmd.Icon.Trim(),
            CircuitKinds = cmd.CircuitKinds.Trim(),
            EntryFormKind = string.IsNullOrWhiteSpace(cmd.EntryFormKind) ? "Civil" : cmd.EntryFormKind.Trim(),
            SortOrder = cmd.SortOrder,
            IsActive = true
        };

        context.StatisticalDisciplines.Add(entity);
        await context.SaveChangesAsync();

        var all = await GetDisciplinesAsync(false);
        StatisticalDisciplines.Sync(all);

        return entity.Id;
    }

    public async Task UpdateDisciplineAsync(StatisticalDisciplineUpdateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.StatisticalDisciplines.FindAsync(cmd.Id)
            ?? throw new KeyNotFoundException("التخصص القضائي غير موجود.");

        entity.NameAr = cmd.NameAr.Trim();
        entity.Icon = string.IsNullOrWhiteSpace(cmd.Icon) ? "⚖️" : cmd.Icon.Trim();
        entity.CircuitKinds = cmd.CircuitKinds.Trim();
        entity.EntryFormKind = string.IsNullOrWhiteSpace(cmd.EntryFormKind) ? "Civil" : cmd.EntryFormKind.Trim();
        entity.SortOrder = cmd.SortOrder;
        entity.IsActive = cmd.IsActive;

        await context.SaveChangesAsync();

        var all = await GetDisciplinesAsync(false);
        StatisticalDisciplines.Sync(all);
    }

    public async Task ToggleDisciplineActiveAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.StatisticalDisciplines.FindAsync(id)
            ?? throw new KeyNotFoundException("التخصص القضائي غير موجود.");

        entity.IsActive = !entity.IsActive;
        await context.SaveChangesAsync();

        var all = await GetDisciplinesAsync(false);
        StatisticalDisciplines.Sync(all);
    }

    // ======================== Employees (Court Staff / Session Clerks) ========================

    public async Task<List<EmployeeDto>> GetEmployeesAsync(string? searchTerm = null, long? courtUnitId = null, bool activeOnly = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Employees
            .AsNoTracking()
            .Include(e => e.CourtUnit)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(e => e.IsActive);
        }

        if (courtUnitId.HasValue)
        {
            query = query.Where(e => e.CourtUnitId == null || e.CourtUnitId == courtUnitId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(e => e.FullNameAr.Contains(term) ||
                                     e.Code.Contains(term) ||
                                     (e.NationalId != null && e.NationalId.Contains(term)) ||
                                     (e.JobTitle != null && e.JobTitle.Contains(term)));
        }

        var employees = await query
            .OrderBy(e => e.FullNameAr)
            .ToListAsync();

        return employees.Select(e => new EmployeeDto(
            e.Id,
            e.Code,
            e.FullNameAr,
            e.JobTitle,
            e.PhoneNumber,
            e.NationalId,
            e.CourtUnitId,
            e.CourtUnit?.NameAr,
            e.IsActive
        )).ToList();
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var e = await context.Employees
            .AsNoTracking()
            .Include(x => x.CourtUnit)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (e is null) return null;

        return new EmployeeDto(
            e.Id,
            e.Code,
            e.FullNameAr,
            e.JobTitle,
            e.PhoneNumber,
            e.NationalId,
            e.CourtUnitId,
            e.CourtUnit?.NameAr,
            e.IsActive
        );
    }

    public async Task<long> CreateEmployeeAsync(EmployeeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var code = string.IsNullOrWhiteSpace(cmd.Code) ? $"EMP-{DateTime.UtcNow.Ticks % 1000000:D6}" : cmd.Code.Trim();

        var entity = new Employee
        {
            Code = code,
            FullNameAr = cmd.FullNameAr.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(cmd.JobTitle) ? "سكرتير جلسة" : cmd.JobTitle.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(cmd.PhoneNumber) ? null : cmd.PhoneNumber.Trim(),
            NationalId = string.IsNullOrWhiteSpace(cmd.NationalId) ? null : cmd.NationalId.Trim(),
            CourtUnitId = cmd.CourtUnitId,
            IsActive = cmd.IsActive
        };

        context.Employees.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateEmployeeAsync(long id, EmployeeCreateCommand cmd)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.Employees.FindAsync(id);
        if (entity is null) throw new InvalidOperationException("الموظف غير موجود.");

        if (!string.IsNullOrWhiteSpace(cmd.Code))
        {
            entity.Code = cmd.Code.Trim();
        }
        entity.FullNameAr = cmd.FullNameAr.Trim();
        entity.JobTitle = string.IsNullOrWhiteSpace(cmd.JobTitle) ? "سكرتير جلسة" : cmd.JobTitle.Trim();
        entity.PhoneNumber = string.IsNullOrWhiteSpace(cmd.PhoneNumber) ? null : cmd.PhoneNumber.Trim();
        entity.NationalId = string.IsNullOrWhiteSpace(cmd.NationalId) ? null : cmd.NationalId.Trim();
        entity.CourtUnitId = cmd.CourtUnitId;
        entity.IsActive = cmd.IsActive;

        await context.SaveChangesAsync();
    }

    public async Task DeleteEmployeeAsync(long id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.Employees.FindAsync(id);
        if (entity is null) return;

        var assignedSessions = await context.Sessions.Where(s => s.EmployeeId == id).ToListAsync();
        foreach (var s in assignedSessions)
        {
            s.EmployeeId = null;
        }

        context.Employees.Remove(entity);
        await context.SaveChangesAsync();
    }
}
