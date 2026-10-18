using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Persistence.Seed;
using JudicialStatistics.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace JudicialStatistics.Tests;

public class FormationAuditTests
{
    private readonly ITestOutputHelper _output;

    public FormationAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetAssembly2026Path()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\JudicialStatistics.Web\الجمعية_العمومية_2026-2027_v2.xlsx"),
            @"c:\Users\ahmed\source\repos\JudicialStatistics\JudicialStatistics.Web\الجمعية_العمومية_2026-2027_v2.xlsx"
        };
        return candidates.First(File.Exists);
    }

    private static string GetOctober2025Path()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\JudicialStatistics.Web\نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"),
            @"c:\Users\ahmed\source\repos\JudicialStatistics\JudicialStatistics.Web\نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"
        };
        return candidates.First(File.Exists);
    }

    [Fact]
    public async System.Threading.Tasks.Task AuditFormationDataAndExport()
    {
        var options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialDbContext(options);

        var reportPath = @"c:\Users\ahmed\source\repos\JudicialStatistics\audit_report.txt";
        using var writer = new StreamWriter(reportPath, false, Encoding.UTF8);

        writer.WriteLine("================================================================================");
        writer.WriteLine("🔍 تقرير الفحص والتدقيق الشامل لشيت التشكيل والجمعية العمومية وقاعدة البيانات");
        writer.WriteLine("================================================================================\n");

        // 1. فحص أنواع القضايا المتاحة في قاعدة البيانات
        writer.WriteLine("--- 1. أنواع القضايا (CaseTypes) المسجلة في قاعدة البيانات ---");
        var caseTypes = await db.CaseTypes.OrderBy(c => c.StatisticalLevelCode).ThenBy(c => c.NameAr).ToListAsync();
        foreach (var ct in caseTypes)
        {
            writer.WriteLine($"Code={ct.Code,-20} | Id={ct.Id,-3} | Level={ct.StatisticalLevelCode,-12} | NameAr={ct.NameAr}");
        }

        // 2. فحص شيت الجمعية العمومية 2026-2027 الأصلي: جلسات مستعجل
        writer.WriteLine("\n--- 2. فحص شيت الجمعية العمومية 2026-2027 الأصلي (البحث عن مستعجل وجلسات أخرى) ---");
        var asmPath = GetAssembly2026Path();
        using (var stream = new FileStream(asmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var ssDoc = XDocument.Load(zip.GetEntry("xl/sharedStrings.xml")!.Open());
            var sharedStrings = ssDoc.Root!.Elements(ns + "si")
                .Select(si => si.Element(ns + "t")?.Value ?? string.Concat(si.Elements(ns + "r").Select(r => r.Element(ns + "t")?.Value ?? "")))
                .ToList();

            var sheetDoc = XDocument.Load(zip.GetEntry("xl/worksheets/sheet3.xml")!.Open());
            var rows = sheetDoc.Descendants(ns + "row");
            int totalSessionsInExcel = 0;
            int excludedCount = 0;

            foreach (var r in rows.Skip(3))
            {
                var cells = r.Elements(ns + "c").ToDictionary(
                    c => System.Text.RegularExpressions.Regex.Replace(c.Attribute("r")?.Value ?? "", @"\d", ""),
                    c => {
                        var v = c.Element(ns + "v")?.Value;
                        if (c.Attribute("t")?.Value == "s" && int.TryParse(v, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                            return sharedStrings[idx];
                        return v ?? "";
                    }
                );

                var sCode = cells.GetValueOrDefault("B")?.Trim();
                var cCode = cells.GetValueOrDefault("C")?.Trim();
                var cScope = cells.GetValueOrDefault("E")?.Trim();
                var day = cells.GetValueOrDefault("F")?.Trim();
                var desc = cells.GetValueOrDefault("H")?.Trim();

                if (string.IsNullOrEmpty(sCode) || string.IsNullOrEmpty(cCode)) continue;
                totalSessionsInExcel++;

                // هل تحتوي على مستعجل؟
                if (desc != null && (desc.Contains("مستعجل") || desc.Contains("المستعجل")))
                {
                    writer.WriteLine($"[Excel Session - مستعجل] Row={r.Attribute("r")?.Value} | sCode={sCode} | cCode={cCode} | Scope={cScope} | Day={day} | Desc={desc}");
                }

                // فحص هل تم استبعادها بـ IsExcluded
                if (desc != null && (desc.Contains("تجديد الحبس") || desc.Contains("غرفة المشورة") || desc.Contains("الأمور الوقتية") || desc.Contains("الامور الوقتية") || desc.Contains("مأذونين") || desc.Contains("موثقين")))
                {
                    excludedCount++;
                    if (desc.Contains("مستعجل"))
                    {
                        writer.WriteLine($"   ⚠️ WARNING: مستعجل was EXCLUDED by IsExcluded rule! Desc={desc}");
                    }
                }
            }
            writer.WriteLine($"Total raw sessions in Excel sheet: {totalSessionsInExcel} | Excluded: {excludedCount}");
        }

        // 3. فحص ملف أكتوبر 2025/2026: كيف كانت جلسات المستعجل الجزئي والمستأنف مستعجل؟
        writer.WriteLine("\n--- 3. فحص شيت أكتوبر 2025/2026 المعتمد (المرجعي) ---");
        var octPath = GetOctober2025Path();
        using (var octStream = new FileStream(octPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var octWb = new XLWorkbook(octStream))
        {
            var octSheet = octWb.Worksheet("التشكيل");
            if (octSheet != null)
            {
                var octLastRow = octSheet.LastRowUsed().RowNumber();
                writer.WriteLine($"October 2025 formation sheet rows: 5 to {octLastRow}");
                for (int r = 5; r <= octLastRow; r++)
                {
                    var circName = octSheet.Cell(r, 6).GetString().Trim();
                    var disc = octSheet.Cell(r, 8).GetString().Trim();
                    var sessName = octSheet.Cell(r, 10).GetString().Trim();
                    var caseType = octSheet.Cell(r, 11).GetString().Trim();
                    var day = octSheet.Cell(r, 12).GetString().Trim();
                    var court = octSheet.Cell(r, 14).GetString().Trim();

                    if (sessName.Contains("مستعجل") || caseType.Contains("مستعجل") || disc.Contains("مستعجل") || circName.Contains("مستعجل"))
                    {
                        writer.WriteLine($"[Oct 2025 مستعجل] Row {r}: Circ={circName} | Disc={disc} | Sess={sessName} | CaseType={caseType} | Day={day} | Court={court}");
                    }
                }
            }
        }

        // تحديث قاعدة البيانات أولاً بالبيانات والتوصيفات المعتمدة
        await Formation2026SeedData.UpdateExistingSessionsAsync(db, null);

        // 4. فحص الجلسات المحفوظة في قاعدة البيانات للعام 2026/2027
        writer.WriteLine("\n--- 4. فحص الجلسات المسجلة بقاعدة البيانات للعام القضائي 2026/2027 ---");
        var dbSessions = await db.Sessions
            .Include(s => s.Circuit)
                .ThenInclude(c => c.CourtUnit)
            .Include(s => s.Circuit)
                .ThenInclude(c => c.Versions)
            .Include(s => s.Versions)
                .ThenInclude(v => v.SessionCaseTypes)
                    .ThenInclude(sct => sct.CaseType)
            .Where(s => s.Circuit.JudicialYearId == 2)
            .OrderBy(s => s.CircuitId)
            .ThenBy(s => s.Id)
            .ToListAsync();

        writer.WriteLine($"Total DB sessions for Year 2: {dbSessions.Count}");

        foreach (var s in dbSessions)
        {
            var sv = s.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            if (s.NameAr.Contains("مستعجل") || (s.Description != null && s.Description.Contains("مستعجل")) || (ct != null && ct.NameAr.Contains("مستعجل")))
            {
                writer.WriteLine($"[DB مستعجل] SessId={s.Id} | CircId={s.CircuitId} ({s.Circuit.NameAr}) | Court={s.Circuit.CourtUnit?.NameAr} | NameAr={s.NameAr} | Desc={s.Description} | CaseType={ct?.NameAr} ({ct?.Code})");
            }
        }

        // 5. فحص ما يتم تصديره في شيت التشكيل الآن عبر FormationExportService
        writer.WriteLine("\n--- 5. فحص شيت التشكيل المصدّر حالياً لعام 2026/2027 (FormationExportService) ---");
        var exportService = new FormationExportService(db);
        var bytes = await exportService.ExportFormationWorkbookAsync(2);
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var sheet = wb.Worksheet("التشكيل");
        var lastRow = sheet.LastRowUsed().RowNumber();

        writer.WriteLine($"Exported rows: 5 to {lastRow}");
        int urgentCount = 0;
        int appealUrgentCount = 0;
        int partialUrgentCount = 0;

        for (int r = 5; r <= lastRow; r++)
        {
            var circNo = sheet.Cell(r, 7).GetString();
            var circName = sheet.Cell(r, 6).GetString();
            var disc = sheet.Cell(r, 8).GetString();
            var sessNo = sheet.Cell(r, 9).GetString();
            var sessName = sheet.Cell(r, 10).GetString();
            var caseType = sheet.Cell(r, 11).GetString();
            var day = sheet.Cell(r, 12).GetString();
            var court = sheet.Cell(r, 14).GetString();

            bool isUrgent = sessName.Contains("مستعجل") || caseType.Contains("مستعجل") || disc.Contains("مستعجل");
            if (isUrgent)
            {
                urgentCount++;
                if (circName.Contains("مستأنف") || disc.Contains("مستأنف") || sessName.Contains("مستأنف")) appealUrgentCount++;
                if (circName.Contains("جزئي") || disc.Contains("جزئي") || sessName.Contains("جزئي")) partialUrgentCount++;
                writer.WriteLine($"[Exported مستعجل] Row {r}: Circ={circName} | Disc={disc} | SessName={sessName} | CaseType={caseType} | Day={day} | Court={court}");
            }
        }
        writer.WriteLine($"Total urgent sessions in export: {urgentCount} (Appeal urgent: {appealUrgentCount}, Partial urgent: {partialUrgentCount})");

        // 6. فحص شامل لكافة صفوف الشيت المصدر لاكتشاف أي أخطاء أو تناقضات أخرى
        writer.WriteLine("\n--- 6. فحص شامل للتناقضات بين التخصص القضائي ونوع الجلسة والاسم ---");
        var distinctPairs = new HashSet<string>();
        for (int r = 5; r <= lastRow; r++)
        {
            var circName = sheet.Cell(r, 6).GetString().Trim();
            var disc = sheet.Cell(r, 8).GetString().Trim();
            var sessName = sheet.Cell(r, 10).GetString().Trim();
            var caseType = sheet.Cell(r, 11).GetString().Trim();

            var pairKey = $"{disc} ==> {caseType}";
            distinctPairs.Add(pairKey);

            // فحص التناقضات المعروفة:
            // 1. مدني مستأنف مستعجل أمامها مدني مستأنف
            if (sessName.Contains("مستعجل") && sessName.Contains("مستأنف") && caseType != "مستأنف مستعجل")
            {
                writer.WriteLine($"[Discrepancy 1: مستأنف مستعجل] Row {r}: SessName='{sessName}' has CaseType='{caseType}' (Expected 'مستأنف مستعجل')");
            }
            // 2. جزئي مستعجل
            if (sessName.Contains("مستعجل") && !sessName.Contains("مستأنف") && caseType != "مستعجل جزئي")
            {
                writer.WriteLine($"[Discrepancy 2: جزئي مستعجل] Row {r}: SessName='{sessName}' has CaseType='{caseType}'");
            }
            // 3. جلسات بدون نوع حقيقي
            if (caseType.StartsWith("جلسة ") || string.IsNullOrEmpty(caseType))
            {
                writer.WriteLine($"[Discrepancy 3: Generic Type] Row {r}: SessName='{sessName}' has generic CaseType='{caseType}'");
            }
            // 4. تناقض التخصص والنوع (مثلاً مدني جزئي مع قضايا عمال أو جنائي)
            if (disc == "مدني جزئي" && (caseType.Contains("جنح") || caseType.Contains("عمال") || caseType.Contains("أسرة")))
            {
                writer.WriteLine($"[Discrepancy 4: Disc/Type mismatch] Row {r}: Disc='{disc}' with CaseType='{caseType}'");
            }
            if (disc == "جنح جزئي" && (caseType.Contains("مدني") || caseType.Contains("صحة توقيع") || caseType.Contains("عمال")))
            {
                writer.WriteLine($"[Discrepancy 5: Disc/Type mismatch] Row {r}: Disc='{disc}' with CaseType='{caseType}'");
            }
        }

        writer.WriteLine("\n--- All Distinct (Discipline ==> CaseType) Combinations in Export ---");
        foreach (var p in distinctPairs.OrderBy(x => x))
        {
            writer.WriteLine($"   {p}");
        }

        // 7. فحص تفصيلي لدوائر الأسرة والعمال والمدني الكلي والجنائي
        writer.WriteLine("\n--- 7. فحص دوائر الأسرة (هل يوجد أسرة مال وحبس أم كلها أسرة نفس؟) ---");
        var famSessions = dbSessions.Where(s => s.Circuit.Versions.Any(v => v.CircuitKind == "Family")).ToList();
        foreach (var s in famSessions)
        {
            var sv = s.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            writer.WriteLine($"Fam SessId={s.Id} | Circ={s.Circuit.NameAr} | NameAr={s.NameAr} | Desc={s.Description} | CaseType={ct?.NameAr} ({ct?.Code})");
        }

        writer.WriteLine("\n--- 8. فحص دوائر العمال (هل يوجد تأمينات اجتماعية وقضايا عمال وجنح عمال؟) ---");
        var labSessions = dbSessions.Where(s => s.Circuit.Versions.Any(v => v.CircuitKind == "Labor")).ToList();
        foreach (var s in labSessions)
        {
            var sv = s.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            writer.WriteLine($"Labor SessId={s.Id} | Circ={s.Circuit.NameAr} | NameAr={s.NameAr} | Desc={s.Description} | CaseType={ct?.NameAr} ({ct?.Code})");
        }

        writer.WriteLine("\n--- 9. فحص دوائر المدني الجزئي بالكامل (C025 - C046) ---");
        var partSessions = dbSessions.Where(s => s.Circuit.Versions.Any(v => v.CircuitKind == "Partial")).ToList();
        var partSummary = partSessions.GroupBy(s => s.Versions.FirstOrDefault()?.SessionCaseTypes.FirstOrDefault()?.CaseType?.NameAr ?? "بدون نوع")
            .Select(g => $"{g.Key}: {g.Count()} sessions");
        writer.WriteLine("توزيع جلسات الجزئي بحسب نوع القضية:");
        foreach (var p in partSummary) writer.WriteLine($"   {p}");

        writer.WriteLine("\n--- 10. فحص دوائر المدني الكلي (C009 - C016) ---");
        var civFullSessions = dbSessions.Where(s => s.Circuit.Versions.Any(v => v.CircuitKind == "CivilFull")).ToList();
        var civSummary = civFullSessions.GroupBy(s => s.Versions.FirstOrDefault()?.SessionCaseTypes.FirstOrDefault()?.CaseType?.NameAr ?? "بدون نوع")
            .Select(g => $"{g.Key}: {g.Count()} sessions");
        writer.WriteLine("توزيع جلسات المدني الكلي بحسب نوع القضية:");
        foreach (var p in civSummary) writer.WriteLine($"   {p}");

        // 12. طباعة كافة التخصصات القضائية StatisticalDisciplines المسجلة في قاعدة البيانات
        writer.WriteLine("\n--- 12. التخصصات القضائية (StatisticalDisciplines) في قاعدة البيانات ---");
        var allDiscs = await db.StatisticalDisciplines.OrderBy(d => d.SortOrder).ToListAsync();
        foreach (var d in allDiscs)
        {
            writer.WriteLine($"Id={d.Id,-2} | Code={d.Code,-15} | NameAr={d.NameAr,-20} | SortOrder={d.SortOrder}");
        }

        // 13. فحص كافة جلسات المدني الكلي والمستأنف (C009 - C016) بالتفصيل
        writer.WriteLine("\n--- 13. كافة جلسات المدني الكلي والمستأنف (C009 - C016) في قاعدة البيانات والشيت المصدر ---");
        var civFullList = dbSessions.Where(s => s.Circuit.Versions.Any(v => v.CircuitKind == "CivilFull")).OrderBy(s => s.Circuit.Code).ThenBy(s => s.Id).ToList();
        foreach (var s in civFullList)
        {
            var sv = s.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            writer.WriteLine($"Circ={s.Circuit.Code} ({s.Circuit.NameAr}) | SessId={s.Id} | NameAr={s.NameAr} | Desc={s.Description} | CaseType={ct?.NameAr} ({ct?.Code})");
        }

        // 14. فحص نصوص جلسات الأسرة في ملف الجمعية العمومية الأصلي 2026-2027
        writer.WriteLine("\n--- 14. نصوص جلسات الأسرة (C017 - C024) في شيت الجمعية العمومية الأصلي ---");
        using (var stream = new FileStream(asmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var ssDoc = XDocument.Load(zip.GetEntry("xl/sharedStrings.xml")!.Open());
            var sharedStrings = ssDoc.Root!.Elements(ns + "si")
                .Select(si => si.Element(ns + "t")?.Value ?? string.Concat(si.Elements(ns + "r").Select(r => r.Element(ns + "t")?.Value ?? "")))
                .ToList();

            var sheetDoc = XDocument.Load(zip.GetEntry("xl/worksheets/sheet3.xml")!.Open());
            var rows = sheetDoc.Descendants(ns + "row");
            foreach (var r in rows.Skip(3))
            {
                var cells = r.Elements(ns + "c").ToDictionary(
                    c => System.Text.RegularExpressions.Regex.Replace(c.Attribute("r")?.Value ?? "", @"\d", ""),
                    c => {
                        var v = c.Element(ns + "v")?.Value;
                        if (c.Attribute("t")?.Value == "s" && int.TryParse(v, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                            return sharedStrings[idx];
                        return v ?? "";
                    }
                );

                var sCode = cells.GetValueOrDefault("B")?.Trim();
                var cCode = cells.GetValueOrDefault("C")?.Trim();
                var desc = cells.GetValueOrDefault("H")?.Trim();
                if (cCode != null && int.TryParse(cCode.Replace("C", ""), out int cNum))
                {
                    if (cNum >= 17 && cNum <= 24)
                    {
                        writer.WriteLine($"[Family Excel Row] cCode={cCode} | sCode={sCode} | Desc={desc}");
                    }
                    if (cNum >= 9 && cNum <= 16)
                    {
                        writer.WriteLine($"[CivFull Excel Row] cCode={cCode} | sCode={sCode} | Desc={desc}");
                    }
                }
            }
        }

        // 15. فحص كافة جلسات المدني الجزئي (C025 - C046)
        writer.WriteLine("\n--- 15. كافة جلسات المدني الجزئي (C025 - C046) بالتفصيل ---");
        var partCivList = dbSessions
            .Where(s => s.Circuit.Versions.Any(v => v.CircuitKind == "Partial") &&
                        (s.NameAr.Contains("مدني") || s.NameAr.Contains("مستعجل") || s.NameAr.Contains("توقيع") || s.NameAr.Contains("حكومة")))
            .OrderBy(s => s.Circuit.Code)
            .ThenBy(s => s.Id)
            .ToList();

        foreach (var s in partCivList)
        {
            var sv = s.Versions.FirstOrDefault();
            var ct = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType;
            writer.WriteLine($"Circ={s.Circuit.Code} ({s.Circuit.NameAr}) | SessId={s.Id} | NameAr={s.NameAr} | CaseType={ct?.NameAr} ({ct?.Code})");
        }
    }
}
