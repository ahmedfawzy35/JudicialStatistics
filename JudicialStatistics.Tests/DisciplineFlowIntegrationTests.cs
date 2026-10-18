using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JudicialStatistics.Application.Organization;
using JudicialStatistics.Application.Rules;
using JudicialStatistics.Application.Statistics;
using JudicialStatistics.Application.Years;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JudicialStatistics.Tests;

[Collection("Database")]
public class DisciplineFlowIntegrationTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true";

    private class TestDbContextFactory : IDbContextFactory<JudicialDbContext>
    {
        private readonly DbContextOptions<JudicialDbContext> _options;
        public TestDbContextFactory(DbContextOptions<JudicialDbContext> options) => _options = options;
        public JudicialDbContext CreateDbContext() => new JudicialDbContext(_options);
        public Task<JudicialDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new JudicialDbContext(_options));
    }

    private TestDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new TestDbContextFactory(options);
    }

    [Fact]
    public async Task DisciplineFlow_AuthorizedCourtsAndCircuits_ShouldFilterAccurately()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return; // Skip if database is not local

        // تطبيق هيكل المحاكم والمأموريات الجديد
        await JudicialStatistics.Infrastructure.Persistence.Seed.CourtStructureSeedData.SeedAsync(context, null);

        var orgService = new OrganizationService(factory);

        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await context.JudicialYears.FirstAsync();

        // 1. Civil
        var civilCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(year.Id, StatisticalDisciplines.Civil, 1);
        Assert.NotEmpty(civilCourts);

        // مأمورية بني سويف الكلية (5 دوائر مدني: 1-4 مدني كلي، 8 مدني مستأنف)
        var bnisfPlenary = civilCourts.First(c => c.Code == "BNISF-PLENARY");
        var bnisfCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, bnisfPlenary.Id, StatisticalDisciplines.Civil, 1);
        Assert.Equal(5, bnisfCircuits.Count);

        // مأمورية الواسطى الكلية (دائرة 5 مدني كلي)
        var wastaPlenary = civilCourts.First(c => c.Code == "WASTA-PLENARY");
        var wastaCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, wastaPlenary.Id, StatisticalDisciplines.Civil, 1);
        Assert.Single(wastaCircuits);

        // مأمورية ببا الكلية (دائرتي 6 و 7 مدني كلي)
        var bebaPlenary = civilCourts.First(c => c.Code == "BEBA-PLENARY");
        var bebaCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, bebaPlenary.Id, StatisticalDisciplines.Civil, 1);
        Assert.Equal(2, bebaCircuits.Count);

        // إجمالي دوائر المدني الكلي بالمأموريات الثلاث = 8 دوائر
        Assert.Equal(8, bnisfCircuits.Count + wastaCircuits.Count + bebaCircuits.Count);

        // 2. Criminal
        var criminalCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(year.Id, StatisticalDisciplines.Criminal, 1);
        Assert.NotEmpty(criminalCourts);

        // دوائر جنح مستأنف بالمأموريات الثلاث (بني سويف 4 + الواسطى 1 + ببا 3 = 8)
        var bnisfCrim = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, bnisfPlenary.Id, StatisticalDisciplines.Criminal, 1);
        Assert.Equal(4, bnisfCrim.Count);
        var wastaCrim = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, wastaPlenary.Id, StatisticalDisciplines.Criminal, 1);
        Assert.Single(wastaCrim);
        var bebaCrim = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, bebaPlenary.Id, StatisticalDisciplines.Criminal, 1);
        Assert.Equal(3, bebaCrim.Count);
        Assert.Equal(8, bnisfCrim.Count + wastaCrim.Count + bebaCrim.Count);

        // 3. Family (محكمة الأسرة فقط)
        var familyCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(year.Id, StatisticalDisciplines.Family, 1);
        Assert.Single(familyCourts);
        Assert.Equal("FAM-COURT", familyCourts[0].Code);
        var familyCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, familyCourts[0].Id, StatisticalDisciplines.Family, 1);
        Assert.Equal(8, familyCircuits.Count);

        // 4. Labor (المحكمة العمالية فقط)
        var laborCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(year.Id, StatisticalDisciplines.Labor, 1);
        Assert.Single(laborCourts);
        Assert.Equal("LAB-COURT", laborCourts[0].Code);
        var laborCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, laborCourts[0].Id, StatisticalDisciplines.Labor, 1);
        Assert.Equal(2, laborCircuits.Count);
    }

    [Fact]
    public async Task DisciplineRules_CaseTypesAndPolicies_ShouldBeConfigured()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        var rulesService = new RulesService(factory);

        // Verify Arbitrators Appointment (ندب حكمين) exists
        var productionTypes = await rulesService.GetProductionItemTypesAsync();
        Assert.Contains(productionTypes, pt => pt.Code == "ARBITRATORS_APPOINT");

        // Verify Criminal incoming types exist
        var incomingTypes = await rulesService.GetIncomingTypesAsync();
        Assert.Contains(incomingTypes, it => it.Code == "CRIM_PREV");
        Assert.Contains(incomingTypes, it => it.Code == "CRIM_RETURN");
        Assert.Contains(incomingTypes, it => it.Code == "CRIM_NEW");

        // Verify Criminal production types exist
        Assert.Contains(productionTypes, pt => pt.Code == "CRIM_IMPRIS");
        Assert.Contains(productionTypes, pt => pt.Code == "CRIM_ACQUIT");
        Assert.Contains(productionTypes, pt => pt.Code == "CRIM_RECON");
    }

    [Fact]
    public async Task CriminalMatrix_ShouldUseSingleYearBand()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        await JudicialStatistics.Infrastructure.Persistence.Seed.CourtStructureSeedData.SeedAsync(context, null);

        var orgService = new OrganizationService(factory);
        var statsService = new StatisticsService(factory);
        var rulesService = new RulesService(factory);

        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await context.JudicialYears.FirstAsync();

        var period = await context.StatisticalPeriods.FirstOrDefaultAsync(p => p.JudicialYearId == year.Id)
                     ?? await context.StatisticalPeriods.FirstAsync();

        var crimCourts = await orgService.GetAuthorizedCourtsForDisciplineAsync(year.Id, StatisticalDisciplines.Criminal, 1);
        var crimCircuits = await orgService.GetAuthorizedCircuitsForDisciplineAsync(year.Id, crimCourts[0].Id, StatisticalDisciplines.Criminal, 1);
        var crimSessions = await orgService.GetAuthorizedSessionVersionsForDisciplineAsync(year.Id, crimCircuits[0].Id, StatisticalDisciplines.Criminal, 1);
        Assert.NotEmpty(crimSessions);

        var matrix = await statsService.GetOrCreateMatrixAsync(crimSessions[0].Id, period.Id, 1);
        Assert.NotNull(matrix);
        Assert.Equal(StatisticalDisciplines.Criminal, matrix.Header.DisciplineCode);

        // Check policy for criminal case type
        var caseTypes = await rulesService.GetCaseTypesForSessionVersionAsync(crimSessions[0].Id);
        Assert.NotEmpty(caseTypes);
        var policy = await rulesService.GetPolicyForCaseTypeAsync(year.Id, caseTypes[0].Id);
        Assert.NotNull(policy);
        Assert.Single(policy.Bands);
        Assert.Equal("AllYears", policy.Bands[0].BandKind);
        Assert.Equal("السنة الإحصائية", policy.Bands[0].LabelAr);
    }

    [Fact]
    public async Task CourtHierarchy_BeniSuefStructure_ValidatesStrictPhilosophy()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        await JudicialStatistics.Infrastructure.Persistence.Seed.CourtStructureSeedData.SeedAsync(context, null);

        var orgService = new OrganizationService(factory);
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await context.JudicialYears.FirstAsync();

        // 1. التحقق من المأموريات الكلية الثلاث وتبعيتها لمحكمة بني سويف الابتدائية
        var plenaryCourts = await context.CourtUnits
            .Where(c => c.UnitKind == "PlenaryHeadquarters")
            .ToListAsync();
        Assert.Equal(3, plenaryCourts.Count);
        Assert.Contains(plenaryCourts, c => c.Code == "BNISF-PLENARY");
        Assert.Contains(plenaryCourts, c => c.Code == "WASTA-PLENARY");
        Assert.Contains(plenaryCourts, c => c.Code == "BEBA-PLENARY");

        // قاعدة صارمة: دوائر المأموريات الكلية تشكيل ثلاثي على الأقل ولا تحوي أي دائرة جزئية
        foreach (var plenary in plenaryCourts)
        {
            var circuits = await orgService.GetCircuitsAsync(year.Id, plenary.Id);
            Assert.NotEmpty(circuits);
            foreach (var cir in circuits)
            {
                var detail = await orgService.GetCircuitDetailAsync(cir.Id);
                Assert.NotNull(detail);
                var ver = detail.Versions.First();
                Assert.True(ver.MinJudges >= 3, $"الدائرة {cir.NameAr} في المأمورية يجب أن تتكون من 3 قضاة على الأقل");
                Assert.NotEqual("Partial", ver.CircuitKind);
            }
        }

        // 2. التحقق من المحاكم الجزئية الثمانية وقاعدتها الصارمة (قاضٍ فردي واحد فقط)
        var summaryCourts = await context.CourtUnits
            .Where(c => c.UnitKind == "SummaryCourt")
            .ToListAsync();
        Assert.Equal(8, summaryCourts.Count);

        foreach (var summary in summaryCourts)
        {
            var circuits = await orgService.GetCircuitsAsync(year.Id, summary.Id);
            Assert.NotEmpty(circuits);
            foreach (var cir in circuits)
            {
                var detail = await orgService.GetCircuitDetailAsync(cir.Id);
                Assert.NotNull(detail);
                var ver = detail.Versions.First();
                Assert.Equal(1, ver.MinJudges);
                Assert.Equal(1, ver.MaxJudges);
            }
        }

        // 3. التحقق من محكمة الأسرة (8 دوائر بتشكيل ثلاثي) والمحكمة العمالية (دائرتين بتشكيل ثلاثي)
        var famCourt = await context.CourtUnits.FirstAsync(c => c.Code == "FAM-COURT");
        var famCircuits = await orgService.GetCircuitsAsync(year.Id, famCourt.Id);
        Assert.Equal(8, famCircuits.Count);

        var labCourt = await context.CourtUnits.FirstAsync(c => c.Code == "LAB-COURT");
        var labCircuits = await orgService.GetCircuitsAsync(year.Id, labCourt.Id);
        Assert.Equal(2, labCircuits.Count);
    }

    [Fact]
    public async Task CaseYearPolicies_MultiTieredYears_ValidatesRefinedBands()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        await JudicialStatistics.Infrastructure.Persistence.Seed.CourtStructureSeedData.SeedAsync(context, null);

        var rulesService = new RulesService(factory);
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await context.JudicialYears.FirstAsync();

        // 1. مدني كلي: 5 سنوات قيد (2022 وما قبلها، 2023، 2024، 2025، 2026)
        var civType = await context.CaseTypes.FirstAsync(c => c.Code == "CIV-FULL-MAIN");
        var civPolicy = await rulesService.GetPolicyForCaseTypeAsync(year.Id, civType.Id);
        Assert.NotNull(civPolicy);
        Assert.Equal(5, civPolicy.Bands.Count);
        Assert.Equal("2022 وما قبلها", civPolicy.Bands[0].LabelAr);
        Assert.Equal("2023", civPolicy.Bands[1].LabelAr);
        Assert.Equal("2024", civPolicy.Bands[2].LabelAr);
        Assert.Equal("2025", civPolicy.Bands[3].LabelAr);
        Assert.Equal("2026", civPolicy.Bands[4].LabelAr);

        // 2. أسرة وعمال: 3 سنوات قيد (2024 وما قبلها، 2025، 2026)
        var famType = await context.CaseTypes.FirstAsync(c => c.Code == "FAM-NAFS");
        var famPolicy = await rulesService.GetPolicyForCaseTypeAsync(year.Id, famType.Id);
        Assert.NotNull(famPolicy);
        Assert.Equal(3, famPolicy.Bands.Count);
        Assert.Equal("2024 وما قبلها", famPolicy.Bands[0].LabelAr);
        Assert.Equal("2025", famPolicy.Bands[1].LabelAr);
        Assert.Equal("2026", famPolicy.Bands[2].LabelAr);

        var labType = await context.CaseTypes.FirstAsync(c => c.Code == "LABOR-MAIN");
        var labPolicy = await rulesService.GetPolicyForCaseTypeAsync(year.Id, labType.Id);
        Assert.NotNull(labPolicy);
        Assert.Equal(3, labPolicy.Bands.Count);

        // 3. جنائي: سنة واحدة موحدة ("السنة الإحصائية")
        var crimType = await context.CaseTypes.FirstAsync(c => c.Code == "CRIM-APP-BANDAR");
        var crimPolicy = await rulesService.GetPolicyForCaseTypeAsync(year.Id, crimType.Id);
        Assert.NotNull(crimPolicy);
        Assert.Single(crimPolicy.Bands);
        Assert.Equal("السنة الإحصائية", crimPolicy.Bands[0].LabelAr);
    }

    [Fact]
    public async Task CircuitFormationFullView_ShouldReturnJudgesWithMonthlyStatusAndDecisions()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        await JudicialStatistics.Infrastructure.Persistence.Seed.CourtStructureSeedData.SeedAsync(context, null);

        var orgService = new OrganizationService(factory);
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await context.JudicialYears.FirstAsync();

        var period = await context.StatisticalPeriods.FirstOrDefaultAsync(p => p.JudicialYearId == year.Id)
                     ?? await context.StatisticalPeriods.FirstAsync();

        var civCircuit = await context.Circuits.FirstAsync(c => c.Code == "CV-01" && c.JudicialYearId == year.Id);

        var fullView = await orgService.GetCircuitFormationFullViewAsync(civCircuit.Id, period.Id);
        Assert.NotNull(fullView);
        Assert.Equal(civCircuit.Code, fullView.CircuitCode);
        Assert.NotEmpty(fullView.Sessions);
        Assert.NotEmpty(fullView.Judges);
        Assert.All(fullView.Judges, j => Assert.False(string.IsNullOrWhiteSpace(j.StatusThisMonth)));
        Assert.NotEmpty(fullView.Decisions);
    }

    [Fact]
    public async Task NewSchema_JudgeJudgmentsAndPostponements_ShouldSaveAndRetrieveAccurately()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        var statsService = new StatisticsService(factory);
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await context.JudicialYears.FirstAsync();

        var periods = await context.StatisticalPeriods
            .Where(p => p.JudicialYearId == year.Id)
            .OrderBy(p => p.MonthStartDate)
            .Take(2)
            .ToListAsync();

        Assert.True(periods.Count >= 2, "Need at least 2 periods for postponement/shortfall test");
        var currentPeriod = periods[0];
        var nextPeriod = periods[1];

        var sessionVersion = await context.SessionVersions
            .Include(sv => sv.CircuitVersion)
            .FirstOrDefaultAsync(sv => sv.CircuitVersion.CircuitKind == "CivilFull")
            ?? await context.SessionVersions.FirstAsync();

        var judge = await context.Judges.FirstAsync();
        var caseType = await context.CaseTypes.FirstAsync();
        var incomingType = await context.IncomingTypes.FirstAsync();

        var matrix = await statsService.GetOrCreateMatrixAsync(sessionVersion.Id, currentPeriod.Id, 1);
        Assert.NotNull(matrix);

        var bandId = matrix.OpeningBalances.FirstOrDefault()?.CaseYearBandId
            ?? (await context.CaseYearBands.FirstAsync()).Id;

        // 1. Prepare command with new tables
        var cmd = new SaveStatisticsDataCommand(
            matrix.Header.Id,
            matrix.Header.RowVersion,
            new List<OpeningBalanceValue> { new(caseType.Id, bandId, 50) },
            new List<IncomingRowValue> { new(caseType.Id, bandId, incomingType.Id, nextPeriod.Id, 12) },
            new List<ProductionRowValue>(),
            new List<JudgeJudgmentValue>
            {
                new(judge.Id, bandId, "FORMAL_REJECT", 4),
                new(judge.Id, bandId, "SUBST_ACCEPT", 9)
            },
            new List<PostponementValue>
            {
                new(bandId, "DEF_RESERVED", nextPeriod.Id, 15)
            }
        );

        // 2. Save
        await statsService.SaveDraftAsync(cmd, 1);

        // 3. Retrieve and verify
        var reloaded = await statsService.GetMatrixByIdAsync(matrix.Header.Id, 1);
        Assert.NotNull(reloaded);

        // Check IncomingRow SourcePeriodId
        var reloadedInc = Assert.Single(reloaded.IncomingRows);
        Assert.Equal(12, reloadedInc.Count);
        Assert.Equal(nextPeriod.Id, reloadedInc.SourcePeriodId);

        // Check JudgeJudgments
        Assert.Equal(2, reloaded.JudgeJudgments.Count);
        var rej = reloaded.JudgeJudgments.First(j => j.JudgmentKind == "FORMAL_REJECT");
        Assert.Equal(4, rej.Count);
        Assert.Equal(judge.Id, rej.JudgeId);

        var acc = reloaded.JudgeJudgments.First(j => j.JudgmentKind == "SUBST_ACCEPT");
        Assert.Equal(9, acc.Count);
        Assert.Equal(judge.Id, acc.JudgeId);

        // Check Postponements
        var post = Assert.Single(reloaded.Postponements);
        Assert.Equal("DEF_RESERVED", post.PostponementKind);
        Assert.Equal(15, post.Count);
        Assert.Equal(nextPeriod.Id, post.TargetPeriodId);
    }

    [Fact]
    public async Task JudgeGovernance_UniqueNameAndCourtSpecificEdit_ShouldEnforceRules()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        var orgService = new OrganizationService(factory);

        var courtA = await context.CourtUnits.FirstAsync(c => c.Code == "BNISF-PLENARY");
        var courtB = await context.CourtUnits.FirstAsync(c => c.Code == "BEBA-PLENARY");

        var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        var judgeCode1 = "J-GOV1-" + uniqueSuffix;
        var judgeCode2 = "J-GOV2-" + uniqueSuffix;
        var judgeName = "المستشار فحص الحوكمة " + uniqueSuffix;

        // 1. Create Judge in Court A
        var judgeId = await orgService.CreateJudgeAsync(new JudgeCreateCommand(judgeCode1, judgeName, courtA.Id));
        Assert.True(judgeId > 0);

        // 2. Prevent Duplicate Judge Name in Court B
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await orgService.CreateJudgeAsync(new JudgeCreateCommand(judgeCode2, judgeName, courtB.Id));
        });

        // 3. Prevent Edit by different court (Court B)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await orgService.UpdateJudgeAsync(new JudgeUpdateCommand(
                judgeId,
                judgeCode1,
                judgeName + " معدل",
                true,
                courtB.Id,
                IsSuperAdmin: false
            ));
        });

        // 4. Allow Edit by creating court (Court A)
        var updatedName = judgeName + " المعتمد";
        await orgService.UpdateJudgeAsync(new JudgeUpdateCommand(
            judgeId,
            judgeCode1,
            updatedName,
            true,
            courtA.Id,
            IsSuperAdmin: false
        ));

        // 5. Verify update succeeded
        var judges = await orgService.GetJudgesAsync(false);
        var updated = judges.First(j => j.Id == judgeId);
        Assert.Equal(updatedName, updated.FullNameAr);
        Assert.Equal(courtA.Id, updated.CreatedCourtUnitId);
        Assert.Equal(courtA.NameAr, updated.CreatedCourtUnitNameAr);

        // Cleanup test judge to keep database clean
        var toRemove = await context.Judges.FindAsync(judgeId);
        if (toRemove != null)
        {
            context.Judges.Remove(toRemove);
            await context.SaveChangesAsync();
        }
    }
}


