using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JudicialStatistics.Application.Organization;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace JudicialStatistics.Tests;

[Collection("Database")]
public class SessionJurisdictionIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly DbContextOptions<JudicialDbContext> _options;

    public SessionJurisdictionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;
    }

    [Fact]
    public async Task CreateSession_WithMultipleGeographicScopes_PersistsAndLoadsAllScopes()
    {
        var factory = new TestDbContextFactory(_options);
        var orgService = new OrganizationService(factory);
        await using var db = new JudicialDbContext(_options);

        // Find a circuit and at least two active geographic areas
        var circuit = await db.Circuits.Include(c => c.Versions).FirstOrDefaultAsync();
        Assert.NotNull(circuit);

        var geoAreas = await db.GeographicAreas.Where(a => a.IsActive).Take(2).ToListAsync();
        Assert.True(geoAreas.Count >= 2, "Need at least 2 active geographic areas for test");

        var area1 = geoAreas[0];
        var area2 = geoAreas[1];

        var testSessionCode = $"TEST-GEO-{Guid.NewGuid():N}"[..15];
        var testSessionName = $"جلسة اختبارية متعددة النطاق {DateTime.UtcNow.Ticks}";

        // 1. Create session covering multiple geographic areas
        var cmd = new SessionCreateCommand(
            CircuitId: circuit.Id,
            Code: testSessionCode,
            NameAr: testSessionName,
            CircuitVersionId: circuit.Versions.First().Id,
            EffectiveFrom: new DateOnly(2025, 10, 1),
            EffectiveTo: new DateOnly(2026, 9, 30),
            ScheduleDescription: "السبت والأحد أسبوع 1",
            GeographicAreaIds: new List<long> { area1.Id, area2.Id }
        );

        var sessionId = await orgService.CreateSessionAsync(cmd);
        Assert.True(sessionId > 0);

        try
        {
            // 2. Fetch session detail and verify multiple geographic areas
            var detail = await orgService.GetSessionDetailAsync(sessionId);
            Assert.NotNull(detail);
            var version = detail.Versions.FirstOrDefault();
            Assert.NotNull(version);

            Assert.Contains(area1.NameAr, version.JurisdictionNamesAr);
            Assert.Contains(area2.NameAr, version.JurisdictionNamesAr);
            Assert.NotNull(version.GeographicAreaIds);
            Assert.Contains(area1.Id, version.GeographicAreaIds);
            Assert.Contains(area2.Id, version.GeographicAreaIds);

            // 3. Fetch from GetSessionsAsync and verify JurisdictionNamesAr is populated
            var sessions = await orgService.GetSessionsAsync(circuitId: circuit.Id);
            var sessionDto = sessions.FirstOrDefault(s => s.Id == sessionId);
            Assert.NotNull(sessionDto);
            Assert.NotNull(sessionDto.JurisdictionNamesAr);
            Assert.Contains(area1.NameAr, sessionDto.JurisdictionNamesAr);
            Assert.Contains(area2.NameAr, sessionDto.JurisdictionNamesAr);

            // 4. Update session to keep only area1
            await orgService.UpdateSessionAsync(new SessionUpdateCommand(
                Id: sessionId,
                NameAr: testSessionName + " معدلة",
                IsClosed: false,
                ScheduleDescription: "السبت فقط",
                GeographicAreaIds: new List<long> { area1.Id }
            ));

            var updatedDetail = await orgService.GetSessionDetailAsync(sessionId);
            Assert.NotNull(updatedDetail);
            var updatedVersion = updatedDetail.Versions.FirstOrDefault();
            Assert.NotNull(updatedVersion);
            Assert.Contains(area1.NameAr, updatedVersion.JurisdictionNamesAr);
            Assert.DoesNotContain(area2.NameAr, updatedVersion.JurisdictionNamesAr);
        }
        finally
        {
            // Cleanup test session
            await using var cleanupDb = new JudicialDbContext(_options);
            var testSession = await cleanupDb.Sessions
                .Include(s => s.Versions).ThenInclude(v => v.Jurisdictions)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (testSession != null)
            {
                foreach (var v in testSession.Versions)
                {
                    cleanupDb.SessionJurisdictions.RemoveRange(v.Jurisdictions);
                    cleanupDb.SessionVersions.Remove(v);
                }
                cleanupDb.Sessions.Remove(testSession);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }

    private class TestDbContextFactory : IDbContextFactory<JudicialDbContext>
    {
        private readonly DbContextOptions<JudicialDbContext> _options;
        public TestDbContextFactory(DbContextOptions<JudicialDbContext> options) => _options = options;
        public JudicialDbContext CreateDbContext() => new(_options);
        public Task<JudicialDbContext> CreateDbContextAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new JudicialDbContext(_options));
    }
}
