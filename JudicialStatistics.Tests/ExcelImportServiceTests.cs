using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Persistence.Seed;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace JudicialStatistics.Tests;

public class ExcelImportServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly DbContextOptions<JudicialDbContext> _options;

    public ExcelImportServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;
    }

    private static string GetExcelPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\JudicialStatistics.Web\نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"),
            @"c:\Users\ahmed\source\repos\JudicialStatistics\JudicialStatistics.Web\نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"
        };

        foreach (var c in candidates)
        {
            var fullPath = Path.GetFullPath(c);
            if (File.Exists(fullPath)) return fullPath;
        }

        throw new FileNotFoundException("Excel formation file not found for tests.");
    }

    [Fact]
    public async Task FormationExportService_ShouldGenerateValidWorkbook()
    {
        await using var db = new JudicialDbContext(_options);
        var year = await db.JudicialYears.FirstOrDefaultAsync();
        Assert.NotNull(year);

        var period = await db.StatisticalPeriods.FirstOrDefaultAsync(p => p.JudicialYearId == year.Id);

        var exportService = new FormationExportService(db);
        var bytes = await exportService.ExportFormationWorkbookAsync(year.Id, period?.Id);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        Assert.True(wb.Worksheets.Contains("التشكيل"));
        var ws = wb.Worksheet("التشكيل");
        Assert.True(ws.RightToLeft);

        // Verification of headers at row 4
        Assert.Equal("رقم الدائرة", ws.Cell(4, 1).GetString().Trim());
        Assert.Equal("نوع الجلسة", ws.Cell(4, 11).GetString().Trim());
        Assert.Equal("يوم العمل", ws.Cell(4, 12).GetString().Trim());
        Assert.Equal("رقم القاضي", ws.Cell(4, 17).GetString().Trim());
        Assert.Equal("الاقدمية", ws.Cell(4, 19).GetString().Trim());
        Assert.Equal("العمل بالدائرة", ws.Cell(4, 24).GetString().Trim());

        // Verification of Row 5 (Data)
        // 1. Circuit ID in Col 1 and Col 7 and Col 21
        long cId1 = ws.Cell(5, 1).GetValue<long>();
        long cId7 = ws.Cell(5, 7).GetValue<long>();
        long cId21 = ws.Cell(5, 21).GetValue<long>();
        Assert.True(cId1 > 0);
        Assert.Equal(cId1, cId7);
        Assert.Equal(cId1, cId21);

        // 2. Session ID in Col 9
        long sId = ws.Cell(5, 9).GetValue<long>();
        Assert.True(sId > 0);
        var dbSession = await db.Sessions.FindAsync(sId);
        Assert.NotNull(dbSession);

        // 3. Case Type in Col 11 (not empty and contains expected case type)
        string caseType = ws.Cell(5, 11).GetString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(caseType));
        _output.WriteLine($"Session Case Type at Col 11: {caseType}");

        // 4. Hearing Day in Col 12
        string hearingDay = ws.Cell(5, 12).GetString().Trim();
        var validDays = new[] { "السبت", "الأحد", "الاحد", "الاثنين", "الإثنين", "الثلاثاء", "الأربعاء", "الاربعاء", "الخميس", "الجمعة" };
        Assert.Contains(hearingDay, validDays);
        _output.WriteLine($"Hearing Day at Col 12: {hearingDay}");

        // 5. Judge ID in Col 17
        long jId = ws.Cell(5, 17).GetValue<long>();
        Assert.True(jId > 0);
        var dbJudge = await db.Judges.FindAsync(jId);
        Assert.NotNull(dbJudge);
        _output.WriteLine($"Judge ID at Col 17: {jId} ({dbJudge.FullNameAr})");

        // 6. Seniority in Col 19 (contains DisplayOrder)
        string seniority = ws.Cell(5, 19).GetString().Trim();
        Assert.Equal("1", seniority);

        // 7. Role in Circuit in Col 24
        string roleInCircuit = ws.Cell(5, 24).GetString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(roleInCircuit));
        _output.WriteLine($"Role in Circuit at Col 24: {roleInCircuit}");

        // 8. Status in Col 25
        string status = ws.Cell(5, 25).GetString().Trim();
        Assert.True(status == "يعمل" || status == "لا يعمل");
        _output.WriteLine($"Status at Col 25: {status}");

    }

    [Fact]
    public async Task StatisticsImportService_ShouldPreviewCivilFullSuccessfully()
    {
        await using var db = new JudicialDbContext(_options);
        await ImportTemplateSeedData.SeedAsync(db, NullLogger.Instance);

        var template = await db.ExcelImportTemplates
            .Include(t => t.ColumnMappings)
            .FirstOrDefaultAsync(t => t.DisciplineKind == "CivilFull");
        Assert.NotNull(template);

        var year = await db.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026")
                   ?? await db.JudicialYears.FirstAsync();

        var period = await db.StatisticalPeriods
            .FirstOrDefaultAsync(p => p.JudicialYearId == year.Id && p.MonthStartDate == new DateOnly(2025, 10, 1))
            ?? await db.StatisticalPeriods.FirstAsync();

        var importService = new StatisticsImportService(db, NullLogger<StatisticsImportService>.Instance);
        var excelPath = GetExcelPath();

        await using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var preview = await importService.PreviewImportAsync(stream, template.Id, year.Id, period.Id);

        _output.WriteLine($"Found sessions: {preview.TotalSessionsFound}, Matched: {preview.MatchedSessionsCount}, Balanced: {preview.BalancedSessionsCount}");
        Assert.True(preview.TotalSessionsFound > 0);
        Assert.True(preview.MatchedSessionsCount > 0);
    }
}
