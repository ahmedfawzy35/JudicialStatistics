using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using JudicialStatistics.Infrastructure.Persistence.Seed;
using Xunit;

namespace JudicialStatistics.Tests;

public class FormationImportTests
{
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
    public void ExcelFile_ShouldExistAndBeAccessible()
    {
        var path = GetExcelPath();
        Assert.True(File.Exists(path), $"الملف غير موجود في المسار: {path}");
    }

    [Fact]
    public void FormationSeedData_ShouldExtractExpectedCircuitCounts()
    {
        var excelPath = GetExcelPath();
        using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet3.xml"));
        Assert.NotNull(zip.GetEntry("xl/sharedStrings.xml"));
    }

    [Fact]
    public async System.Threading.Tasks.Task FormationExportService_ShouldExportClerkInfo_AndColAJVisibility_AndRealCaseTypes()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(options);
        var exportService = new JudicialStatistics.Infrastructure.Services.FormationExportService(db);

        var bytes = await exportService.ExportFormationWorkbookAsync(2);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var sheet = workbook.Worksheet("التشكيل");
        Assert.NotNull(sheet);

        // Col 15 (O) & 16 (P): Secretary Name and Phone
        Assert.Equal("اسم سكرتير الجلسة", sheet.Cell(4, 15).GetString().Trim());
        Assert.Equal("رقم تليفون سكرتير الجلسة", sheet.Cell(4, 16).GetString().Trim());

        // Col 36 (AJ): ShowInProgressReport
        Assert.Equal("ظهور الجلسة", sheet.Cell(4, 36).GetString().Trim());

        // Verify data rows
        int foundYes = 0;
        int foundNo = 0;
        int foundClerks = 0;

        for (int r = 5; r <= 357; r++)
        {
            var clerkName = sheet.Cell(r, 15).GetString();
            var clerkPhone = sheet.Cell(r, 16).GetString();
            var caseType = sheet.Cell(r, 11).GetString();
            var showVal = sheet.Cell(r, 36).GetString();

            if (!string.IsNullOrWhiteSpace(clerkName)) foundClerks++;
            if (showVal == "نعم") foundYes++;
            if (showVal == "لا") foundNo++;

            // Ensure case type is not empty and does not say "جلسة 1" or "جلسة 2"
            Assert.False(string.IsNullOrWhiteSpace(caseType), $"Row {r} has empty case type");
            Assert.False(caseType.StartsWith("جلسة "), $"Row {r} has generic case type: {caseType}");
        }

        Assert.True(foundClerks > 300, $"Expected >300 clerks, found {foundClerks}");
        Assert.True(foundYes > 200, $"Expected >200 visible sessions, found {foundYes}");
        Assert.True(foundNo > 100, $"Expected >100 hidden specialized sessions, found {foundNo}");
    }

    [Fact]
    public async System.Threading.Tasks.Task InspectExportedSessionsForSignatures()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(options);
        var exportService = new JudicialStatistics.Infrastructure.Services.FormationExportService(db);

        // Check Year 2 (2026/2027)
        var bytes2 = await exportService.ExportFormationWorkbookAsync(2);
        using var ms2 = new MemoryStream(bytes2);
        using var wb2 = new ClosedXML.Excel.XLWorkbook(ms2);
        var sheet2 = wb2.Worksheet("التشكيل");
        var lastRow2 = sheet2.LastRowUsed().RowNumber();

        var dumpFile = @"c:\Users\ahmed\source\repos\JudicialStatistics\exported_sessions_dump.txt";
        using var writer = new StreamWriter(dumpFile, false, System.Text.Encoding.UTF8);
        writer.WriteLine($"=== YEAR 2 SESSIONS (5 to {lastRow2}) ===");

        for (int r = 5; r <= lastRow2; r++)
        {
            var circNo = sheet2.Cell(r, 7).GetString();
            var circName = sheet2.Cell(r, 6).GetString();
            var disc = sheet2.Cell(r, 8).GetString();
            var sessNo = sheet2.Cell(r, 9).GetString();
            var sessName = sheet2.Cell(r, 10).GetString();
            var caseType = sheet2.Cell(r, 11).GetString();
            var day = sheet2.Cell(r, 12).GetString();
            var court = sheet2.Cell(r, 14).GetString();

            if (sessName.Contains("صحة توقيع") || caseType.Contains("صحة توقيع") || disc.Contains("صحة توقيع") || sessName.Contains("توقيع"))
            {
                writer.WriteLine($"Row {r}: CircId={circNo} | CircName={circName} | Disc={disc} | SessId={sessNo} | SessName={sessName} | CaseType={caseType} | Day={day} | Court={court}");
            }
        }

        // Check all rows in year 2 to see how many sessions have "صحة توقيع"
        int signCount = 0;
        for (int r = 5; r <= lastRow2; r++)
        {
            var sessName = sheet2.Cell(r, 10).GetString();
            if (sessName.Contains("صحة توقيع") || sessName.Contains("توقيع"))
            {
                signCount++;
            }
        }
        writer.WriteLine($"Total sessions with صحة توقيع in Year 2: {signCount}");
    }

    [Fact]
    public async System.Threading.Tasks.Task InspectDatabaseCircuitsAndSessions()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<JudicialStatistics.Infrastructure.Persistence.JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        var dumpFile = @"c:\Users\ahmed\source\repos\JudicialStatistics\db_circuits_dump.txt";
        using var writer = new StreamWriter(dumpFile, false, System.Text.Encoding.UTF8);

        await using var db = new JudicialStatistics.Infrastructure.Persistence.JudicialDbContext(options);
        var allCourts = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.CourtUnits);
        writer.WriteLine("--- CourtUnits in DB ---");
        foreach (var cu in allCourts)
        {
            writer.WriteLine($"CourtId={cu.Id} | Code={cu.Code} | NameAr={cu.NameAr}");
        }

        var circuits1 = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Circuits
                .Include(c => c.CourtUnit)
                .Include(c => c.Versions)
                .Where(c => c.JudicialYearId == 1)
                .OrderBy(c => c.Id)
        );

        writer.WriteLine($"\nTotal circuits for Year 1: {circuits1.Count}");
        foreach (var c in circuits1)
        {
            var cv = c.Versions.FirstOrDefault();
            writer.WriteLine($"Id={c.Id} | Code={c.Code} | NameAr={c.NameAr} | CourtUnitId={c.CourtUnitId} | CourtName={c.CourtUnit?.NameAr} | CourtCode={c.CourtUnit?.Code} | Kind={cv?.CircuitKind}");
        }

        // 1. Run UpdateExistingSessionsAsync
        await JudicialStatistics.Infrastructure.Persistence.Seed.Formation2026SeedData.UpdateExistingSessionsAsync(db);

        var circuits = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Circuits
                .Include(c => c.CourtUnit)
                .Include(c => c.Versions)
                .Where(c => c.JudicialYearId == 2)
                .OrderBy(c => c.Id)
        );

        writer.WriteLine($"Total circuits for Year 2: {circuits.Count}");
        foreach (var c in circuits)
        {
            var cv = c.Versions.FirstOrDefault();
            writer.WriteLine($"Id={c.Id} | Code={c.Code} | NameAr={c.NameAr} | CourtUnitId={c.CourtUnitId} | CourtName={c.CourtUnit?.NameAr} | CourtCode={c.CourtUnit?.Code} | Kind={cv?.CircuitKind}");
        }

        // Check sessions for Circuit 73 (C025)
        var sessions73 = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Sessions
                .Include(s => s.Versions)
                    .ThenInclude(v => v.SessionCaseTypes)
                        .ThenInclude(sct => sct.CaseType)
                .Where(s => s.CircuitId == 73)
        );

        writer.WriteLine("\n--- Sessions for Circuit 73 ---");
        foreach (var s in sessions73)
        {
            var sv = s.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            writer.WriteLine($"SessId={s.Id} | Name={s.NameAr} | Desc={s.Description} | CaseType={ct?.NameAr} ({ct?.Code})");
        }

        // Verify Circuit 73 properties
        var c73 = circuits.First(c => c.Id == 73);
        Assert.Equal("الدائرة الأولى جزئي بندر بني سويف", c73.NameAr);
        Assert.Equal("BANDAR-PARTIAL", c73.CourtUnit?.Code);

        // Verify signature sessions in Circuit 73
        var signSess = sessions73.Where(s => s.NameAr.Contains("صحة توقيع")).ToList();
        Assert.Equal(2, signSess.Count);
        foreach (var ss in signSess)
        {
            var sv = ss.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            Assert.Equal("CIV-PART-SIGN", ct?.Code);
            Assert.Equal("صحة توقيع", ct?.NameAr);
        }

        // Verify exported workbook
        var exportService = new JudicialStatistics.Infrastructure.Services.FormationExportService(db);
        var bytes = await exportService.ExportFormationWorkbookAsync(2);
        using var ms = new MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var sheet = wb.Worksheet("التشكيل");
        var lastRow = sheet.LastRowUsed().RowNumber();

        // 1. Verify Lookup Tables (Cols 28 & 29) do NOT contain the mother primary court
        for (int r = 5; r <= 30; r++)
        {
            var courtCode = sheet.Cell(r, 28).GetString().Trim();
            var courtName = sheet.Cell(r, 29).GetString().Trim();
            if (!string.IsNullOrEmpty(courtCode))
            {
                Assert.NotEqual("BNISF-COURT", courtCode);
                Assert.NotEqual("FYM-COURT", courtCode);
                Assert.NotEqual("محكمة بني سويف الابتدائية", courtName);
                Assert.NotEqual("محكمة الفيوم الابتدائية", courtName);
            }
        }

        // 2. Verify Table 1 (جدول الدوائر) contains full circuit names and affiliated courts
        int circuitsCount = circuits.Count;
        for (int i = 0; i < circuitsCount; i++)
        {
            int r = 5 + i;
            var cId = sheet.Cell(r, 1).GetValue<long>();
            var cName = sheet.Cell(r, 2).GetString().Trim();
            var cCourt = sheet.Cell(r, 3).GetString().Trim();

            Assert.False(string.IsNullOrWhiteSpace(cName), $"Circuit at row {r} has empty name");
            Assert.False(string.IsNullOrWhiteSpace(cCourt), $"Circuit at row {r} has empty court");
            Assert.NotEqual("محكمة بني سويف الابتدائية", cCourt);

            if (cId == 73) // C025
            {
                Assert.Equal("الدائرة الأولى جزئي بندر بني سويف", cName);
                Assert.Equal("محكمة بندر بني سويف الجزئية", cCourt);
            }
        }

        // 3. Verify signature sessions classification
        int signRowCount = 0;
        for (int r = 5; r <= lastRow; r++)
        {
            var sessName = sheet.Cell(r, 10).GetString();
            var caseType = sheet.Cell(r, 11).GetString();
            var disc = sheet.Cell(r, 8).GetString();

            if (sessName.Contains("صحة توقيع"))
            {
                signRowCount++;
                Assert.Equal("مدني جزئي", disc);
                Assert.Equal("صحة توقيع", caseType);
            }
        }
        Assert.Equal(25, signRowCount);
    }
}
