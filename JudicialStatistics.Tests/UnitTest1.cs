using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace JudicialStatistics.Tests;

[Collection("Database")]
public class UnitTest1
{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExecuteAndVerifyOctober2025Seed()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(options);

        // Run Seeder
        await JudicialStatistics.Infrastructure.Persistence.Seed.October2025StatisticsSeed.SeedAsync(db);

        // Verify seeded statistics
        var headers = await db.StatisticsHeaders
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.CircuitVersion)
            .Include(h => h.SessionVersion)
                .ThenInclude(sv => sv.OpeningBalances)
            .Include(h => h.IncomingRows)
            .Include(h => h.JudgeJudgments)
            .Include(h => h.Postponements)
            .Where(h => h.StatisticalPeriodId == 1)
            .ToListAsync();

        _output.WriteLine($"Total October 2025 StatisticsHeaders: {headers.Count}");
        Assert.True(headers.Count > 0, "No headers were seeded for October 2025");

        var byKind = headers.GroupBy(h => h.SessionVersion.CircuitVersion.CircuitKind);
        foreach (var grp in byKind)
        {
            var openSum = grp.SelectMany(h => h.SessionVersion.OpeningBalances).Sum(o => o.Count);
            var incSum = grp.SelectMany(h => h.IncomingRows).Sum(i => i.Count);
            var judSum = grp.SelectMany(h => h.JudgeJudgments).Sum(j => j.Count);
            var postSum = grp.SelectMany(h => h.Postponements).Sum(p => p.Count);
            _output.WriteLine($"Discipline [{grp.Key}]: Headers={grp.Count()}, Openings={openSum}, Incomings={incSum}, Judgments={judSum}, Postponements={postSum}");
        }

        // Test ReportsService for all 7 disciplines
        var factoryMock = new TestDbContextFactory(options);
        var reportsService = new JudicialStatistics.Infrastructure.Services.ReportsService(factoryMock);

        string[] reportTypes = ["CivilFull", "Family", "Labor", "LaborMisdemeanor", "PartialMisdemeanor", "AppealMisdemeanor", "PartialCivil"];
        foreach (var rType in reportTypes)
        {
            var filter = new JudicialStatistics.Application.Reports.CircuitRankingFilter(
                JudicialYearId: 1,
                StatisticalPeriodIds: [1],
                ReportType: rType,
                CourtUnitId: null,
                DeductSuspendedInMisdemeanors: false
            );

            var report = await reportsService.GetCircuitRankingReportAsync(filter);
            _output.WriteLine($"\nReport [{rType}]: Title='{report.ReportTitleAr}', Rows={report.Rows.Count}, TotalIncoming={report.Totals.TotalIncoming}, TotalFinalJudgments={report.Totals.FinalJudgmentsCount}, CompletionRate={report.Totals.CompletionRate}%");

            Assert.True(report.Rows.Count > 0, $"Report for {rType} returned 0 rows");
            Assert.True(report.Totals.TotalIncoming > 0, $"Report for {rType} has 0 incoming");
            Assert.True(report.Totals.FinalJudgmentsCount > 0, $"Report for {rType} has 0 final judgments");

            var excelBytes = await reportsService.ExportCircuitRankingToExcelAsync(filter);
            Assert.NotNull(excelBytes);
            Assert.True(excelBytes.Length > 1000, $"Excel export for {rType} produced invalid length {excelBytes.Length}");

            foreach (var row in report.Rows.Take(3))
            {
                var judgesStr = string.Join(", ", row.JudgesAllocations.Select(ja => $"{ja.JudgeNameAr}: {ja.FinalJudgmentsCount}"));
                _output.WriteLine($"  Rank {row.Rank}: [{row.CircuitCode}] {row.CircuitNameAr} | Head: {row.PresidentJudgeNameAr} | Inc: {row.TotalIncoming} | Final: {row.FinalJudgmentsCount} | Rate: {row.CompletionRate}% | Judges: {judgesStr}");
            }
        }
    }

    [Fact]
    public async Task TestPeriodOpenCloseAndLockValidation()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        var yearsService = new JudicialStatistics.Infrastructure.Services.JudicialYearsService(factoryMock);
        var statsService = new JudicialStatistics.Infrastructure.Services.StatisticsService(factoryMock);

        // Pick period 12 (or a period without existing header)
        long testPeriodId = 12;

        long testSessionVersionId = 1;
        await using (var ctx = factoryMock.CreateDbContext())
        {
            var existingSessionIds = await ctx.StatisticsHeaders
                .Where(h => h.StatisticalPeriodId == testPeriodId)
                .Select(h => h.SessionVersionId)
                .ToListAsync();

            var availableSession = await ctx.SessionVersions
                .FirstOrDefaultAsync(sv => !existingSessionIds.Contains(sv.Id));

            if (availableSession != null)
            {
                testSessionVersionId = availableSession.Id;
            }
        }

        // Ensure it's open first
        await yearsService.SetPeriodEntryStatusAsync(testPeriodId, true);
        var periods = await yearsService.GetPeriodsForYearAsync(1);
        var p = periods.FirstOrDefault(x => x.Id == testPeriodId);
        Assert.NotNull(p);
        Assert.True(p.IsOpenForEntry);

        // Now close the period
        await yearsService.SetPeriodEntryStatusAsync(testPeriodId, false);
        periods = await yearsService.GetPeriodsForYearAsync(1);
        p = periods.FirstOrDefault(x => x.Id == testPeriodId);
        Assert.NotNull(p);
        Assert.False(p.IsOpenForEntry);

        // Attempting to create a new statistics header for a closed period must throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await statsService.GetOrCreateMatrixAsync(testSessionVersionId, testPeriodId, 1);
        });
        Assert.Contains("مغلق للإدخال", ex.Message);

        // Restore period to open
        await yearsService.SetPeriodEntryStatusAsync(testPeriodId, true);
        periods = await yearsService.GetPeriodsForYearAsync(1);
        Assert.True(periods.First(x => x.Id == testPeriodId).IsOpenForEntry);
    }

    [Fact]
    public async Task TestCourtLevelPeriodStatusControl()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        var yearsService = new JudicialStatistics.Infrastructure.Services.JudicialYearsService(factoryMock);
        var statsService = new JudicialStatistics.Infrastructure.Services.StatisticsService(factoryMock);

        await using var context = factoryMock.CreateDbContext();
        var courts = await context.CourtUnits.Where(c => c.ParentCourtUnitId != null && c.IsActive).Take(2).ToListAsync();
        Assert.True(courts.Count >= 2, "Expected at least 2 child courts");

        var courtA = courts[0];
        var courtB = courts[1];
        long testPeriodId = 11;

        // Ensure both courts are initially open
        await yearsService.SetCourtPeriodEntryStatusAsync(testPeriodId, courtA.Id, true);
        await yearsService.SetCourtPeriodEntryStatusAsync(testPeriodId, courtB.Id, true);

        Assert.True(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtA.Id));
        Assert.True(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtB.Id));

        // Close courtA only
        await yearsService.SetCourtPeriodEntryStatusAsync(testPeriodId, courtA.Id, false);

        // Court A is now closed, Court B remains open
        Assert.False(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtA.Id));
        Assert.True(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtB.Id));

        // Find a session version belonging to Court A
        var sessionA = await context.SessionVersions
            .Include(sv => sv.CircuitVersion).ThenInclude(cv => cv.Circuit)
            .FirstOrDefaultAsync(sv => sv.CircuitVersion.Circuit.CourtUnitId == courtA.Id);

        if (sessionA != null)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await statsService.GetOrCreateMatrixAsync(sessionA.Id, testPeriodId, 1);
            });
            Assert.Contains("مغلق للإدخال", ex.Message);
        }

        // Re-open courtA
        await yearsService.SetCourtPeriodEntryStatusAsync(testPeriodId, courtA.Id, true);
        Assert.True(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtA.Id));

        // Test SetAllCourtsPeriodEntryStatusAsync
        long primaryCourtId = courtA.ParentCourtUnitId!.Value;
        await yearsService.SetAllCourtsPeriodEntryStatusAsync(testPeriodId, primaryCourtId, false);
        Assert.False(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtA.Id));
        Assert.False(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtB.Id));

        // Restore all courts to open
        await yearsService.SetAllCourtsPeriodEntryStatusAsync(testPeriodId, primaryCourtId, true);
        Assert.True(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtA.Id));
        Assert.True(await yearsService.IsPeriodOpenForCourtAsync(testPeriodId, courtB.Id));
    }

    [Fact]
    public async Task TestStatisticsEntryProgressReport()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        var reportsService = new JudicialStatistics.Infrastructure.Services.ReportsService(factoryMock);

        var report = await reportsService.GetStatisticsEntryProgressReportAsync(1, 1, null);

        Assert.NotNull(report);
        _output.WriteLine($"Report Period: {report.PeriodMonthNameAr} ({report.JudicialYearNameAr})");
        _output.WriteLine($"Total Sessions: {report.TotalSessionsCount}");
        _output.WriteLine($"Total Entered: {report.TotalEnteredCount} ({report.OverallEntryRatePercent}%)");
        _output.WriteLine($"Total Balanced: {report.TotalBalancedCount} ({report.OverallBalancedRatePercent}%)");
        _output.WriteLine($"Courts Count: {report.CourtsProgress.Count}");

        Assert.True(report.TotalSessionsCount > 0, "No sessions found for period 1");
        Assert.True(report.TotalEnteredCount > 0, "No entered sessions found for period 1");
        Assert.True(report.OverallEntryRatePercent > 0, "Overall entry rate should be > 0%");
        Assert.True(report.CourtsProgress.Count > 0, "Courts progress should have items");
        Assert.True(report.DisciplineGroups.Count > 0, "Discipline groups should have items");

        foreach (var cp in report.CourtsProgress)
        {
            _output.WriteLine($"Court: {cp.CourtUnitNameAr} - Total: {cp.TotalSessionsCount}, Entered: {cp.EnteredSessionsCount}, Rate: {cp.EntryRatePercent}%");
            Assert.True(cp.TotalSessionsCount >= cp.EnteredSessionsCount);
        }
    }

    [Fact]
    public async Task TestDataEntrySessionScopingAndAuthorization()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(options);

        var user = await db.AppUsers
            .Include(u => u.CourtGrants).ThenInclude(g => g.SessionGrants)
            .FirstOrDefaultAsync(u => u.Email == "adminbsn@court.gov.eg");

        if (user == null)
        {
            _output.WriteLine("adminbsn@court.gov.eg user not in DB, skipping specific user test");
            return;
        }

        var factory = new TestDbContextFactory(options);
        var orgService = new JudicialStatistics.Infrastructure.Services.OrganizationService(factory);
        var statsService = new JudicialStatistics.Infrastructure.Services.StatisticsService(factory);

        // 1. Civil courts for this user should be empty since he only has Labor sessions
        var civilCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(1, JudicialStatistics.Application.Organization.StatisticalDisciplines.Civil, user.Id, 1);
        _output.WriteLine($"Civil courts count for scoped user: {civilCourts.Count}");
        Assert.Empty(civilCourts);

        // 2. Labor courts for this user should contain ONLY the labor court
        var laborCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(1, JudicialStatistics.Application.Organization.StatisticalDisciplines.Labor, user.Id, 1);
        _output.WriteLine($"Labor courts count for scoped user: {laborCourts.Count}");
        Assert.Single(laborCourts);
        Assert.Contains("العمالية", laborCourts[0].NameAr);

        // 3. User circuits should only be circuits having assigned sessions
        var laborCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(1, laborCourts[0].Id, JudicialStatistics.Application.Organization.StatisticalDisciplines.Labor, user.Id);
        _output.WriteLine($"Labor circuits count for scoped user: {laborCircuits.Count}");
        Assert.NotEmpty(laborCircuits);

        // 4. User sessions should only be the assigned sessions
        var laborSessions = await orgService.GetAuthorizedSessionVersionsForDisciplineAsync(1, laborCircuits[0].Id, JudicialStatistics.Application.Organization.StatisticalDisciplines.Labor, user.Id);
        _output.WriteLine($"Labor sessions count for scoped user: {laborSessions.Count}");
        Assert.NotEmpty(laborSessions);
        var assignedSessionIds = user.CourtGrants.SelectMany(g => g.SessionGrants.Select(sg => sg.SessionId)).ToHashSet();
        foreach (var s in laborSessions)
        {
            Assert.Contains(s.SessionId, assignedSessionIds);
        }

        // 5. Trying to access/create matrix for an unauthorized session should throw UnauthorizedAccessException
        var civilSession = await db.SessionVersions
            .Include(sv => sv.CircuitVersion)
            .FirstOrDefaultAsync(sv => sv.CircuitVersion.CircuitKind == "CivilFull");

        if (civilSession != null)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await statsService.GetOrCreateMatrixAsync(civilSession.Id, 1, user.Id);
            });
        }

        // 6. Table headers for user should only contain assigned sessions
        var headers = await statsService.GetHeadersAsync(1, 1, null, null, user.Id, 1);
        _output.WriteLine($"Headers count for user: {headers.Count}");
        foreach (var h in headers)
        {
            var sv = await db.SessionVersions.FirstOrDefaultAsync(x => x.Id == h.SessionVersionId);
            if (sv != null)
            {
                Assert.Contains(sv.SessionId, assignedSessionIds);
            }
        }
    }

    [Fact]
    public async Task TestOrganizationScopingTabsAndJudicialYear()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(options);

        var scopedUser = await db.AppUsers
            .Include(u => u.CourtGrants).ThenInclude(g => g.SessionGrants)
            .FirstOrDefaultAsync(u => u.Email == "adminbsn@court.gov.eg");

        if (scopedUser == null)
        {
            _output.WriteLine("adminbsn@court.gov.eg not found, skipping");
            return;
        }

        var adminUser = await db.AppUsers
            .Include(u => u.CourtGrants).ThenInclude(g => g.Role)
            .FirstOrDefaultAsync(u => u.CourtGrants.Any(g => g.Role != null && g.Role.Code == "SYSTEM_ADMIN"));

        var factory = new TestDbContextFactory(options);
        var orgService = new JudicialStatistics.Infrastructure.Services.OrganizationService(factory);

        var assignedSessionIds = scopedUser.CourtGrants
            .Where(g => g.IsActive)
            .SelectMany(g => g.SessionGrants.Select(sg => sg.SessionId))
            .ToHashSet();

        long yearId = 1;

        // 1. Tab 2: Circuits scoping
        var scopedCircuits = await orgService.GetCircuitsAsync(yearId, null, scopedUser.Id);
        _output.WriteLine($"Scoped circuits count: {scopedCircuits.Count}");
        Assert.NotEmpty(scopedCircuits);
        foreach (var c in scopedCircuits)
        {
            Assert.Contains("عمال", c.CourtUnitNameAr);
        }

        if (adminUser != null)
        {
            var allCircuits = await orgService.GetCircuitsAsync(yearId, null, adminUser.Id);
            _output.WriteLine($"Admin circuits count: {allCircuits.Count}");
            Assert.True(allCircuits.Count > scopedCircuits.Count);
        }

        // 2. Tab 3: Sessions scoping
        var scopedSessions = await orgService.GetSessionsAsync(null, yearId, scopedUser.Id);
        _output.WriteLine($"Scoped sessions count: {scopedSessions.Count}");
        Assert.Equal(assignedSessionIds.Count, scopedSessions.Count);
        foreach (var s in scopedSessions)
        {
            Assert.Contains(s.Id, assignedSessionIds);
        }

        if (adminUser != null)
        {
            var allSessions = await orgService.GetSessionsAsync(null, yearId, adminUser.Id);
            _output.WriteLine($"Admin sessions count: {allSessions.Count}");
            Assert.True(allSessions.Count > scopedSessions.Count);
        }

        // 3. Tab 4: Judges scoping (working in available sessions/circuits for this year)
        var scopedJudges = await orgService.GetJudgesAsync(false, yearId, scopedUser.Id);
        _output.WriteLine($"Scoped judges count: {scopedJudges.Count}");
        Assert.NotEmpty(scopedJudges);

        var allJudges = await orgService.GetJudgesAsync(false, yearId, adminUser?.Id);
        _output.WriteLine($"Admin judges count: {allJudges.Count}");
        Assert.True(allJudges.Count >= scopedJudges.Count);

        // 4. Tab 5: Assignments scoping
        var scopedAssignments = await orgService.GetAssignmentsForYearAsync(yearId, scopedUser.Id);
        _output.WriteLine($"Scoped assignments count: {scopedAssignments.Count}");
        var scopedCircuitIds = scopedCircuits.Select(c => c.Id).ToHashSet();
        foreach (var a in scopedAssignments)
        {
            Assert.Contains(a.CircuitId, scopedCircuitIds);
        }

        if (adminUser != null)
        {
            var allAssignments = await orgService.GetAssignmentsForYearAsync(yearId, adminUser.Id);
            _output.WriteLine($"Admin assignments count: {allAssignments.Count}");
            Assert.True(allAssignments.Count > scopedAssignments.Count);
        }
    }

    [Fact]
    public async Task VerifyMonthlyInventoryReportAndExcelExport()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        var reportsService = new JudicialStatistics.Infrastructure.Services.ReportsService(factoryMock);

        await using var db = factoryMock.CreateDbContext();
        var year = await db.JudicialYears.FirstOrDefaultAsync();
        Assert.NotNull(year);
        var basePeriod = await db.StatisticalPeriods.Where(p => p.JudicialYearId == year.Id).OrderBy(p => p.MonthStartDate).FirstOrDefaultAsync();
        Assert.NotNull(basePeriod);

        // 1. Get Monthly Inventory Report
        var filter = new JudicialStatistics.Application.Reports.MonthlyInventoryFilter(
            year.Id,
            basePeriod.Id
        );

        var report = await reportsService.GetMonthlyInventoryReportAsync(filter);
        Assert.NotNull(report);
        Assert.False(string.IsNullOrWhiteSpace(report.BasePeriodNameAr));
        Assert.NotNull(report.GrandTotal);
        _output.WriteLine($"Monthly Inventory Report: TargetMonths={report.TargetMonths.Count}, CumulativeBands={report.CumulativeBandTotals.Count}, TotalAvailableBalance={report.GrandTotal.TotalAvailableBalance}");

        // 2. Export to Excel
        var excelBytes = await reportsService.ExportMonthlyInventoryToExcelAsync(filter);
        Assert.NotNull(excelBytes);
        Assert.True(excelBytes.Length > 0);

        // Verify valid Excel package with ClosedXML
        using var ms = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(ms);
        Assert.True(workbook.Worksheets.Count > 0);
        var sheet = workbook.Worksheet(1);
        Assert.True(sheet.RightToLeft);
        _output.WriteLine($"Monthly Inventory Excel generated successfully. Sheet title: '{sheet.Name}', Rows used: {sheet.RowsUsed().Count()}");
    }

    [Fact]
    public async Task VerifyJudgeSessionJudgmentsReportAndExcelExport()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        var reportsService = new JudicialStatistics.Infrastructure.Services.ReportsService(factoryMock);

        await using var db = factoryMock.CreateDbContext();
        var judge = await db.Judges.FirstOrDefaultAsync(j => j.IsActive);
        Assert.NotNull(judge);
        var year = await db.JudicialYears.FirstOrDefaultAsync();
        Assert.NotNull(year);
        var periodIds = await db.StatisticalPeriods.Where(p => p.JudicialYearId == year.Id).Select(p => p.Id).ToListAsync();

        // 1. Get Judge Session Judgments Report
        var filter = new JudicialStatistics.Application.Reports.JudgeSessionJudgmentsFilter(
            judge.Id,
            year.Id,
            periodIds
        );

        var report = await reportsService.GetJudgeSessionJudgmentsReportAsync(filter);
        Assert.NotNull(report);
        Assert.Equal(judge.FullNameAr, report.JudgeFullNameAr);
        Assert.NotNull(report.GrandTotals);
        _output.WriteLine($"Judge Session Report: Judge={report.JudgeFullNameAr}, Sessions={report.SessionRows.Count}, GrandTotalJudgments={report.GrandTotals.GrandTotalJudgmentsCount}");

        // 2. Export to Excel
        var excelBytes = await reportsService.ExportJudgeSessionJudgmentsToExcelAsync(filter);
        Assert.NotNull(excelBytes);
        Assert.True(excelBytes.Length > 0);

        // Verify valid Excel package with ClosedXML
        using var ms = new MemoryStream(excelBytes);
        using var workbook = new XLWorkbook(ms);
        Assert.True(workbook.Worksheets.Count > 0);
        var sheet = workbook.Worksheet(1);
        Assert.True(sheet.RightToLeft);
        _output.WriteLine($"Judge Sessions Excel generated successfully. Sheet title: '{sheet.Name}', Rows used: {sheet.RowsUsed().Count()}");
    }

    [Fact]
    public async Task VerifyStatisticalDisciplinesTableAndSeed()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        await using var db = factoryMock.CreateDbContext();

        await JudicialStatistics.Infrastructure.Persistence.Seed.DisciplinesRulesSeedData.SeedAsync(db);

        var disciplines = await db.StatisticalDisciplines.ToListAsync();
        _output.WriteLine($"StatisticalDisciplines count: {disciplines.Count}");
        foreach (var d in disciplines)
        {
            _output.WriteLine($"Discipline: {d.Code} - {d.NameAr} - {d.Icon}");
        }
        Assert.NotEmpty(disciplines);
    }

    [Fact]
    public async Task CleanupTestGovernanceJudges()
    {
        var options = new DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var factoryMock = new TestDbContextFactory(options);
        await using var db = factoryMock.CreateDbContext();

        var testJudges = await db.Judges
            .Where(j => j.FullNameAr.Contains("فحص الحوكمة") || j.Code.StartsWith("J-GOV"))
            .ToListAsync();

        _output.WriteLine($"Found {testJudges.Count} test judges to remove.");
        foreach (var j in testJudges)
        {
            _output.WriteLine($"Removing: {j.Id} - {j.Code} - {j.FullNameAr}");
            var movements = await db.JudgeMovements.Where(m => m.JudgeId == j.Id).ToListAsync();
            db.JudgeMovements.RemoveRange(movements);

            var assignments = await db.CircuitJudgeAssignments.Where(a => a.JudgeId == j.Id).ToListAsync();
            db.CircuitJudgeAssignments.RemoveRange(assignments);

            var allocs = await db.JudgmentAllocations.Where(a => a.JudgeId == j.Id).ToListAsync();
            db.JudgmentAllocations.RemoveRange(allocs);

            var jJudg = await db.JudgeJudgments.Where(jj => jj.JudgeId == j.Id).ToListAsync();
            db.JudgeJudgments.RemoveRange(jJudg);

            db.Judges.Remove(j);
        }

        await db.SaveChangesAsync();
        _output.WriteLine("Cleanup completed.");
    }

    private class TestDbContextFactory : IDbContextFactory<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>
    {
        private readonly DbContextOptions<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext> _options;
        public TestDbContextFactory(DbContextOptions<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext> options) => _options = options;
        public JudicialStatistics.Infrastructure.Persistence.JudicialDbContext CreateDbContext() => new(_options);
        public Task<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(_options));
    }
}
