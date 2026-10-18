using JudicialStatistics.Application.Courts;
using JudicialStatistics.Application.Organization;
using JudicialStatistics.Application.Reports;
using JudicialStatistics.Application.Rules;
using JudicialStatistics.Application.Statistics;
using JudicialStatistics.Application.Years;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JudicialStatistics.Infrastructure;

/// <summary>
/// تسجيل خدمات Infrastructure و Application في حاوية IoC
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // تسجيل IDbContextFactory وخيارات DbContext كـ Singleton
        services.AddDbContextFactory<JudicialDbContext>(options =>
            options.UseSqlServer(connectionString));

        // تسجيل JudicialDbContext كـ Scoped عبر الـ Factory لتجنب تعارض الـ Lifetime
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<JudicialDbContext>>().CreateDbContext());

        // Application Services Implementations
        services.AddScoped<ICourtsService, CourtsService>();
        services.AddScoped<IJudicialYearsService, JudicialYearsService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IRulesService, RulesService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<JudicialStatistics.Application.Identity.IUsersService, UsersService>();
        services.AddScoped<JudicialStatistics.Application.Import.IFormationExportService, FormationExportService>();
        services.AddScoped<JudicialStatistics.Application.Import.IStatisticsImportService, StatisticsImportService>();

        return services;
    }
}
