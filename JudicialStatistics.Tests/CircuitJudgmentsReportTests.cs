using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JudicialStatistics.Application.Reports;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JudicialStatistics.Tests;

[Collection("Database")]
public class CircuitJudgmentsReportTests
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
    public async Task ColumnConfigs_DefaultSeedingAndCustomization_WorksCorrectly()
    {
        var factory = CreateFactory();
        await using var context = factory.CreateDbContext();
        if (!await context.Database.CanConnectAsync())
            return;

        var reportsService = new ReportsService(factory);

        // 1. Initial fetch should seed defaults
        var configs = await reportsService.GetCircuitReportColumnConfigsAsync();
        Assert.NotEmpty(configs);
        Assert.Contains(configs, c => c.ColumnKey == "FINAL" && c.IsVisible);
        Assert.Contains(configs, c => c.ColumnKey == "EVID_TOTAL" && c.IsVisible);
        Assert.Contains(configs, c => c.ColumnKey == "TOTAL_JUDGMENTS" && c.IsVisible);

        // 2. Customize a title and visibility
        var modified = configs.Select(c =>
        {
            if (c.ColumnKey == "FINAL")
            {
                return c with { CustomTitleAr = "الأحكام النهائية المنهية", DisplayOrder = 5 };
            }
            if (c.ColumnKey == "EVID_INVEST")
            {
                return c with { IsVisible = true };
            }
            return c;
        }).ToList();

        await reportsService.SaveCircuitReportColumnConfigsAsync(modified);

        // 3. Verify changes persisted
        var updated = await reportsService.GetCircuitReportColumnConfigsAsync();
        var finalCol = updated.First(c => c.ColumnKey == "FINAL");
        Assert.Equal("الأحكام النهائية المنهية", finalCol.CustomTitleAr);
        Assert.Equal("الأحكام النهائية المنهية", finalCol.EffectiveTitleAr);
        Assert.Equal(5, finalCol.DisplayOrder);

        var investCol = updated.First(c => c.ColumnKey == "EVID_INVEST");
        Assert.True(investCol.IsVisible);

        // 4. Reset to defaults
        await reportsService.ResetCircuitReportColumnConfigsToDefaultAsync();
        var reset = await reportsService.GetCircuitReportColumnConfigsAsync();
        var resetFinal = reset.First(c => c.ColumnKey == "FINAL");
        Assert.Equal(string.Empty, resetFinal.CustomTitleAr);
        Assert.Equal("أحكام قطعية", resetFinal.EffectiveTitleAr);
        Assert.Equal(10, resetFinal.DisplayOrder);
    }
}
