using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Web.Data;

/// <summary>
/// DbContext الرئيسي لمشروع Web — يرث من JudicialDbContextBase{ApplicationUser}
/// يجمع بين Identity (مع ApplicationUser) وجميع جداول التطبيق
/// يُستخدم في Program.cs ومع AddEntityFrameworkStores{ApplicationDbContext}()
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : JudicialDbContextBase<ApplicationUser>(options)
{
}
