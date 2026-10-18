using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure;

/// <summary>
/// نقطة تنفيذ الـ Seed عند بدء التطبيق
/// يُستدعى من Program.cs بعد البناء
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope  = serviceProvider.CreateScope();
        var logger       = scope.ServiceProvider.GetRequiredService<ILogger<JudicialDbContext>>();

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<JudicialDbContext>();

            // تطبيق الـ Migrations تلقائياً عند الحاجة (في بيئة التطوير فقط)
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("تطبيق {Count} migration(s) معلّقة...", pendingMigrations.Count());
                await context.Database.MigrateAsync();
            }

            // تنفيذ الـ Seed
            logger.LogInformation("بدء تنفيذ Seed Data...");
            await IdentitySeedData.SeedAsync(context);
            await CourtSeedData.SeedAsync(context);
            await ReferenceSeedData.SeedAsync(context);
            await FormationSeedData.SeedAsync(context, logger);
            await Formation2026SeedData.SeedAsync(context, logger);
            await CivilFullRulesSeedData.SeedAsync(context, logger);
            await DisciplinesRulesSeedData.SeedAsync(context, logger);
            await CourtStructureSeedData.SeedAsync(context, logger);
            await EnsureStatisticalPeriodsAsync(context, logger);
            await October2025StatisticsSeed.SeedAsync(context, logger);
            await ImportTemplateSeedData.SeedAsync(context, logger);
            logger.LogInformation("Seed Data اكتمل.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "تحذير: حدث استثناء أثناء تهيئة قاعدة البيانات.");
            // عدم رمي الاستثناء لمنع توقف السيرفر، حيث قد تكون قاعدة البيانات مهيأة مسبقاً
        }
    }

    private static async Task EnsureStatisticalPeriodsAsync(JudicialDbContext context, ILogger logger)
    {
        var yearsWithoutPeriods = await context.JudicialYears
            .Include(y => y.Periods)
            .Where(y => !y.Periods.Any())
            .ToListAsync();

        if (yearsWithoutPeriods.Count > 0)
        {
            foreach (var year in yearsWithoutPeriods)
            {
                logger.LogInformation("توليد 12 فترة شهرية للعام القضائي {Year}...", year.NameAr);
                for (int i = 0; i < 12; i++)
                {
                    year.Periods.Add(new Domain.Years.StatisticalPeriod
                    {
                        MonthStartDate = year.StartDate.AddMonths(i),
                        IsOpenForEntry = true
                    });
                }
            }
            await context.SaveChangesAsync();
            logger.LogInformation("تم حفظ الفترات الإحصائية الشهرية للأعوام القضائية.");
        }
    }
}
