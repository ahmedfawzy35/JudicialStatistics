using JudicialStatistics.Domain.Courts;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed أولي للمحاكم ونطاقات التقارير — آمن للتنفيذ المتكرر
/// يُنشئ وحدة محكمة واحدة ونطاق تقرير واحد كبداية
/// </summary>
public static class CourtSeedData
{
    public static async Task SeedAsync(JudicialDbContext context)
    {
        // محكمة بني سويف الابتدائية
        if (!await context.CourtUnits.AnyAsync(c => c.Code == "BNISF-COURT"))
        {
            var rootCourt = new CourtUnit
            {
                Code     = "BNISF-COURT",
                NameAr   = "محكمة بني سويف الابتدائية",
                UnitKind = "Court",
                IsActive = true,
            };
            context.CourtUnits.Add(rootCourt);
            await context.SaveChangesAsync();

            // نطاق تقارير واحد مرتبط بالمحكمة
            if (!await context.ReportingScopes.AnyAsync(r => r.Code == "BNISF-SCOPE"))
            {
                var scope = new ReportingScope
                {
                    Code            = "BNISF-SCOPE",
                    NameAr          = "نطاق محكمة بني سويف الابتدائية",
                    RootCourtUnitId = rootCourt.Id,
                    IsActive        = true,
                };
                context.ReportingScopes.Add(scope);
                await context.SaveChangesAsync();

                // إضافة المحكمة نفسها ضمن نطاق التقرير
                context.ReportingScopeUnits.Add(new ReportingScopeUnit
                {
                    ReportingScopeId = scope.Id,
                    CourtUnitId      = rootCourt.Id,
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
