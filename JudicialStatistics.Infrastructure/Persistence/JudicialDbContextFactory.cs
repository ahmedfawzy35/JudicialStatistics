using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace JudicialStatistics.Infrastructure.Persistence;

/// <summary>
/// يُستخدم من أدوات EF Core في وقت التصميم (dotnet ef migrations add / update)
/// يستخدم JudicialDbContext (مع IdentityUser) — لا يحتاج مشروع Web
/// </summary>
public class JudicialDbContextFactory : IDesignTimeDbContextFactory<JudicialDbContext>
{
    public JudicialDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "JudicialStatistics.Web");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var optionsBuilder = new DbContextOptionsBuilder<JudicialDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new JudicialDbContext(optionsBuilder.Options);
    }
}
