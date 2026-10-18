using JudicialStatistics.Domain.Geography;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed المناطق الجغرافية والصفات القضائية — آمن للتنفيذ المتكرر
/// </summary>
public static class ReferenceSeedData
{
    public static async Task SeedAsync(JudicialDbContext context)
    {
        await SeedGeographicAreasAsync(context);
        await SeedJudicialRolesAsync(context);
    }

    private static async Task SeedGeographicAreasAsync(JudicialDbContext context)
    {
        if (await context.GeographicAreas.AnyAsync(g => g.Code == "BNISF-GOV"))
            return;

        // المحافظة
        var gov = new GeographicArea
        {
            Code     = "BNISF-GOV",
            NameAr   = "محافظة بني سويف",
            AreaKind = "Governorate",
        };
        context.GeographicAreas.Add(gov);
        await context.SaveChangesAsync();

        // المراكز والبنادر
        var centers = new[]
        {
            new { Code = "BNISF-WASITA",  NameAr = "مركز الواسطى",      Kind = "Center" },
            new { Code = "BNISF-NASER",   NameAr = "مركز ناصر",         Kind = "Center" },
            new { Code = "BNISF-CITY",    NameAr = "بندر بني سويف",     Kind = "City"   },
            new { Code = "BNISF-CENTER",  NameAr = "مركز بني سويف",     Kind = "Center" },
            new { Code = "BNISF-IHNASYA", NameAr = "مركز إهناسيا",      Kind = "Center" },
            new { Code = "BNISF-BEBA",    NameAr = "مركز ببا",           Kind = "Center" },
            new { Code = "BNISF-SAMASTA", NameAr = "مركز سمسطا",        Kind = "Center" },
            new { Code = "BNISF-FASHN",   NameAr = "مركز الفشن",        Kind = "Center" },
        };

        foreach (var c in centers)
        {
            context.GeographicAreas.Add(new GeographicArea
            {
                Code         = c.Code,
                NameAr       = c.NameAr,
                AreaKind     = c.Kind,
                ParentAreaId = gov.Id,
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedJudicialRolesAsync(JudicialDbContext context)
    {
        var roles = new[]
        {
            new { Code = "CHIEF",        NameAr = "رئيس الدائرة",     SortOrder = (short)1 },
            new { Code = "MEMBER_RIGHT", NameAr = "عضو يمين",         SortOrder = (short)2 },
            new { Code = "MEMBER_LEFT",  NameAr = "عضو يسار",         SortOrder = (short)3 },
            new { Code = "MEMBER_LEFT2", NameAr = "عضو يسار اليسار", SortOrder = (short)4 },
            new { Code = "MEMBER_4",     NameAr = "عضو رابع",         SortOrder = (short)4 },
            new { Code = "MEMBER_5",     NameAr = "عضو خامس",         SortOrder = (short)5 },
            new { Code = "MEMBER_6",     NameAr = "عضو سادس",         SortOrder = (short)6 },
            new { Code = "MEMBER",       NameAr = "عضو احتياطي",      SortOrder = (short)7 },
        };

        foreach (var r in roles)
        {
            if (!await context.JudicialRoles.AnyAsync(x => x.Code == r.Code))
            {
                context.JudicialRoles.Add(new JudicialRole
                {
                    Code      = r.Code,
                    NameAr    = r.NameAr,
                    SortOrder = r.SortOrder,
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
