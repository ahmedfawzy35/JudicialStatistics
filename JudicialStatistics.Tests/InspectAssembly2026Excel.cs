using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using JudicialStatistics.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace JudicialStatistics.Tests;

public class InspectAssembly2026Excel
{
    private readonly ITestOutputHelper _output;

    public InspectAssembly2026Excel(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetExcelPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\JudicialStatistics.Web\الجمعية_العمومية_2026-2027_v2.xlsx"),
            @"c:\Users\ahmed\source\repos\JudicialStatistics\JudicialStatistics.Web\الجمعية_العمومية_2026-2027_v2.xlsx"
        };

        foreach (var c in candidates)
        {
            var fullPath = Path.GetFullPath(c);
            if (File.Exists(fullPath)) return fullPath;
        }

        throw new FileNotFoundException("Assembly 2026-2027 excel file not found.");
    }

    [Fact]
    public void ExtractImages()
    {
        var outDir = @"c:\Users\ahmed\source\repos\JudicialStatistics\extracted_images";
        Directory.CreateDirectory(outDir);

        var path = GetExcelPath();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("xl/media/")))
        {
            var fileName = Path.GetFileName(entry.FullName);
            var destPath = Path.Combine(outDir, fileName);
            entry.ExtractToFile(destPath, true);
        }
    }

    [Fact]
    public void InspectSheets()
    {
        var dumpPath = @"c:\Users\ahmed\source\repos\JudicialStatistics\dump_2026_2027.txt";
        using var dumpWriter = new StreamWriter(dumpPath, false, System.Text.Encoding.UTF8);

        var path = GetExcelPath();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        var ssEntry = zip.GetEntry("xl/sharedStrings.xml")!;
        var ssDoc = XDocument.Load(ssEntry.Open());
        var sharedStrings = new List<string>();
        foreach (var si in ssDoc.Root!.Elements(ns + "si"))
        {
            var t = si.Element(ns + "t");
            if (t != null) sharedStrings.Add(t.Value);
            else sharedStrings.Add(string.Concat(si.Elements(ns + "r").Select(r => r.Element(ns + "t")?.Value ?? "")));
        }

        var wbEntry = zip.GetEntry("xl/workbook.xml")!;
        var wbDoc = XDocument.Load(wbEntry.Open());
        var sheets = wbDoc.Root!.Element(ns + "sheets")!.Elements(ns + "sheet")
            .Select(s => new {
                Name = s.Attribute("name")?.Value ?? "",
                SheetId = s.Attribute("sheetId")?.Value ?? "",
                RelId = s.Attribute(rNs + "id")?.Value ?? ""
            }).ToList();

        var wbRelsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels")!;
        var wbRelsDoc = XDocument.Load(wbRelsEntry.Open());
        XNamespace pkgRelsNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        var relMap = wbRelsDoc.Root!.Elements(pkgRelsNs + "Relationship")
            .ToDictionary(r => r.Attribute("Id")?.Value ?? "", r => r.Attribute("Target")?.Value ?? "");

        string GetCellValue(XElement c)
        {
            var val = c.Element(ns + "v")?.Value;
            var type = c.Attribute("t")?.Value;
            if (type == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                return sharedStrings[idx];
            return val ?? "";
        }

        void Log(string s) { _output.WriteLine(s); dumpWriter.WriteLine(s); }

        // Let's dump all circuits
        DumpCircuits(zip, relMap, sheets.First(s => s.Name == "الدوائر").RelId, ns, sharedStrings, Log);
        DumpJudges(zip, relMap, sheets.First(s => s.Name == "التشكيلات").RelId, ns, sharedStrings, Log);
        DumpSessionsSummary(zip, relMap, sheets.First(s => s.Name == "الجلسات").RelId, ns, sharedStrings, Log);
    }

    private void DumpCircuits(ZipArchive zip, Dictionary<string, string> relMap, string relId, XNamespace ns, List<string> sharedStrings, Action<string> Log)
    {
        var target = relMap[relId];
        if (target.StartsWith("/")) target = target.Substring(1); else target = "xl/" + target;
        var wsDoc = XDocument.Load(zip.GetEntry(target)!.Open());
        var rows = wsDoc.Root!.Element(ns + "sheetData")!.Elements(ns + "row").ToList();

        string GetVal(XElement c) {
            var val = c.Element(ns + "v")?.Value;
            var type = c.Attribute("t")?.Value;
            if (type == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count) return sharedStrings[idx];
            return val ?? "";
        }

        Log("=== ALL 53 CIRCUITS ===");
        int cIdx = 0;
        foreach (var r in rows.Skip(3)) // Skip headers
        {
            var d = r.Elements(ns + "c").ToDictionary(c => new string(c.Attribute("r")?.Value?.Where(char.IsLetter).ToArray()), c => GetVal(c));
            if (d.ContainsKey("A") && !string.IsNullOrEmpty(d["A"]))
            {
                cIdx++;
                Log($"#{cIdx} | Code:{d.GetValueOrDefault("A")} | Name:{d.GetValueOrDefault("B")} | Scope:{d.GetValueOrDefault("C")} | Cat:{d.GetValueOrDefault("D")} | SharedScope:{d.GetValueOrDefault("G")} | Notes:{d.GetValueOrDefault("H")} | Page:{d.GetValueOrDefault("E")}");
            }
        }
    }

    private void DumpJudges(ZipArchive zip, Dictionary<string, string> relMap, string relId, XNamespace ns, List<string> sharedStrings, Action<string> Log)
    {
        var target = relMap[relId];
        if (target.StartsWith("/")) target = target.Substring(1); else target = "xl/" + target;
        var wsDoc = XDocument.Load(zip.GetEntry(target)!.Open());
        var rows = wsDoc.Root!.Element(ns + "sheetData")!.Elements(ns + "row").ToList();

        string GetVal(XElement c) {
            var val = c.Element(ns + "v")?.Value;
            var type = c.Attribute("t")?.Value;
            if (type == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count) return sharedStrings[idx];
            return val ?? "";
        }

        Log("=== JUDGES / FORMATIONS ===");
        var judges = new List<string>();
        foreach (var r in rows.Skip(3))
        {
            var d = r.Elements(ns + "c").ToDictionary(c => new string(c.Attribute("r")?.Value?.Where(char.IsLetter).ToArray()), c => GetVal(c));
            if (d.ContainsKey("E") && !string.IsNullOrEmpty(d["E"]))
            {
                judges.Add($"CircuitCode: {d.GetValueOrDefault("A")} | Circuit: {d.GetValueOrDefault("B")} | Scope: {d.GetValueOrDefault("C")} | Order: {d.GetValueOrDefault("D")} | Judge: {d.GetValueOrDefault("E")} | Role: {d.GetValueOrDefault("F")} | Page: {d.GetValueOrDefault("G")}");
            }
        }
        Log($"Total Judge Assignments: {judges.Count}");
        foreach (var j in judges) Log(j);
    }

    private void DumpSessionsSummary(ZipArchive zip, Dictionary<string, string> relMap, string relId, XNamespace ns, List<string> sharedStrings, Action<string> Log)
    {
        var target = relMap[relId];
        if (target.StartsWith("/")) target = target.Substring(1); else target = "xl/" + target;
        var wsDoc = XDocument.Load(zip.GetEntry(target)!.Open());
        var rows = wsDoc.Root!.Element(ns + "sheetData")!.Elements(ns + "row").ToList();

        string GetVal(XElement c) {
            var val = c.Element(ns + "v")?.Value;
            var type = c.Attribute("t")?.Value;
            if (type == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count) return sharedStrings[idx];
            return val ?? "";
        }

        Log("=== ALL SESSIONS ===");
        var allSessions = new List<Dictionary<string, string>>();
        foreach (var r in rows)
        {
            var d = r.Elements(ns + "c").ToDictionary(c => new string(c.Attribute("r")?.Value?.Where(char.IsLetter).ToArray()), c => GetVal(c));
            allSessions.Add(d);
        }
        Log($"Total Raw Rows: {allSessions.Count}");
        for (int i = 0; i < Math.Min(6, allSessions.Count); i++)
        {
            Log($"Row #{i}: {string.Join(" | ", allSessions[i].Select(kv => $"{kv.Key}:{kv.Value}"))}");
        }

        Log("=== SESSION FILTERING ANALYSIS ===");
        int excludedCount = 0;
        int includedCount = 0;
        var excludedDescs = new HashSet<string>();
        var includedDescs = new HashSet<string>();

        bool IsExcluded(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return true;
            if (desc == "الجلسة واختصاصها وأرقامها") return true; // header
            if (desc.Contains("تجديد الحبس") || desc.Contains("تجديد حبس")) return true;
            if (desc.Contains("غرفة المشورة")) return true;
            if (desc.Contains("الأمور الوقتية") || desc.Contains("الامور الوقتية")) return true;
            if (desc.Contains("مأذونين") || desc.Contains("موثقين")) return true;
            return false;
        }

        var includedSessions = new List<Dictionary<string, string>>();
        foreach (var s in allSessions)
        {
            var desc = s.GetValueOrDefault("H")?.Trim() ?? "";
            if (IsExcluded(desc))
            {
                excludedCount++;
                excludedDescs.Add(desc);
            }
            else
            {
                includedCount++;
                includedDescs.Add(desc);
                includedSessions.Add(s);
            }
        }

        Log($"Excluded Sessions Count: {excludedCount} (Unique Descriptions: {excludedDescs.Count})");
        foreach (var ed in excludedDescs) Log($"  [EXCLUDED] {ed}");

        Log($"Included Sessions Count: {includedCount} (Unique Descriptions: {includedDescs.Count})");
        foreach (var id in includedDescs.OrderBy(x => x)) Log($"  [INCLUDED] {id}");
    }

    [Fact]
    public async Task Inspect2025Jurisdictions()
    {
        var options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialDbContext(options);
        var year2025 = await db.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026");
        if (year2025 == null)
        {
            _output.WriteLine("Year 2025/2026 not found in DB.");
            return;
        }

        var sessions = await db.Sessions
            .Where(s => s.Circuit.JudicialYearId == year2025.Id)
            .Include(s => s.Circuit)
            .Include(s => s.Versions)
                .ThenInclude(v => v.Jurisdictions)
                    .ThenInclude(j => j.GeographicArea)
            .Take(30)
            .ToListAsync();

        _output.WriteLine($"Sample 2025 Sessions ({sessions.Count}):");
        foreach (var s in sessions)
        {
            var v = s.Versions.FirstOrDefault();
            var areas = v?.Jurisdictions.Select(j => $"{j.GeographicArea.Code}:{j.GeographicArea.NameAr}") ?? Enumerable.Empty<string>();
            _output.WriteLine($"Session: {s.Code} | Circuit: {s.Circuit.Code} ({s.Circuit.NameAr}) | Name: {s.NameAr} | Areas: {string.Join(", ", areas)}");
        }
    }

    [Fact]
    public async Task TestSeed2026Assembly()
    {
        var options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialDbContext(options);
        var excelPath = GetExcelPath();
        await JudicialStatistics.Infrastructure.Persistence.Seed.Formation2026SeedData.SeedAsync(db, null, excelPath);

        var year2026 = await db.JudicialYears
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.NameAr == "2026/2027");

        Assert.NotNull(year2026);
        Assert.Equal(12, year2026.Periods.Count);

        var circuits = await db.Circuits
            .Where(c => c.JudicialYearId == year2026.Id)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
            .ToListAsync();

        Assert.Equal(49, circuits.Count);

        var assignmentsCount = circuits.SelectMany(c => c.Versions).SelectMany(v => v.JudgeAssignments).Count();
        _output.WriteLine($"Circuits: {circuits.Count}, Assignments: {assignmentsCount}");
        foreach (var c in circuits)
        {
            var cv = c.Versions.FirstOrDefault();
            _output.WriteLine($"Circuit {c.Code} has {cv?.JudgeAssignments.Count} assignments.");
        }
        Assert.Equal(107, assignmentsCount);

        var sessions = await db.Sessions
            .Where(s => s.Circuit.JudicialYearId == year2026.Id)
            .Include(s => s.Employee)
            .Include(s => s.Versions)
                .ThenInclude(v => v.Jurisdictions)
            .ToListAsync();

        Assert.Equal(353, sessions.Count);
        _output.WriteLine($"Successfully verified 2026/2027: 12 periods, {circuits.Count} circuits, {assignmentsCount} judge assignments, {sessions.Count} sessions.");
    }

    [Fact]
    public async Task ExportCircuitsReport()
    {
        var options = new DbContextOptionsBuilder<JudicialDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        await using var db = new JudicialDbContext(options);

        var year2026 = await db.JudicialYears
            .FirstOrDefaultAsync(y => y.NameAr == "2026/2027");

        Assert.NotNull(year2026);

        var circuits = await db.Circuits
            .Where(c => c.JudicialYearId == year2026.Id)
            .Include(c => c.CourtUnit)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .OrderBy(c => c.Code)
            .ToListAsync();

        var sessions = await db.Sessions
            .Where(s => s.Circuit.JudicialYearId == year2026.Id)
            .Include(s => s.Employee)
            .Include(s => s.Versions)
                .ThenInclude(v => v.Jurisdictions)
                    .ThenInclude(j => j.GeographicArea)
            .OrderBy(s => s.Code)
            .ToListAsync();

        var sessionVersionIds = sessions.SelectMany(s => s.Versions).Select(v => v.Id).ToList();
        var sessionCaseTypes = await db.SessionCaseTypes
            .Where(sct => sessionVersionIds.Contains(sct.SessionVersionId))
            .Include(sct => sct.CaseType)
            .ToListAsync();

        var sctLookup = sessionCaseTypes
            .GroupBy(x => x.SessionVersionId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CaseType.NameAr).Distinct().ToList());

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 🏛️ دليل وتشكيل الدوائر والجلسات — العام القضائي 2026-2027");
        sb.AppendLine();
        sb.AppendLine($"> **إجمالي الدوائر:** {circuits.Count} دائرة قضائية | **إجمالي الجلسات:** {sessions.Count} جلسة | **إجمالي القضاة المكلفين:** {circuits.SelectMany(c => c.Versions).SelectMany(v => v.JudgeAssignments).Count()} تكليفاً قضائياً");
        sb.AppendLine();

        int cIndex = 1;
        foreach (var c in circuits)
        {
            var cv = c.Versions.FirstOrDefault();
            var kindAr = cv?.CircuitKind switch
            {
                "CivilFull" => "مدني كلي",
                "CriminalAppeal" => "جنح مستأنف",
                "Partial" => "جزئي",
                "Labor" => "عمالية",
                "Family" => "أسرة",
                _ => cv?.CircuitKind ?? "غير محدد"
            };

            var assignments = cv?.JudgeAssignments.OrderBy(ja => ja.DisplayOrder).ToList() ?? new List<JudicialStatistics.Domain.Organization.CircuitJudgeAssignment>();
            var circuitSessions = sessions.Where(s => s.CircuitId == c.Id).OrderBy(s => s.Code).ToList();

            sb.AppendLine($"## {cIndex++}. دائرة: {c.NameAr} (`{c.Code}`)");
            sb.AppendLine($"- **المحكمة / المأمورية:** {c.CourtUnit?.NameAr ?? "غير محدد"}");
            sb.AppendLine($"- **نوع ومستوى الدائرة:** {kindAr} (كود النوع: `{cv?.CircuitKind}`)");
            sb.AppendLine();

            sb.AppendLine("### 👨‍⚖️ تشكيل هيئة المحكمة والقضاة المكلفين:");
            if (assignments.Count == 0)
            {
                sb.AppendLine("- *لا يوجد قضاة مكلفون مسجلون لهذه الدائرة.*");
            }
            else
            {
                sb.AppendLine("| م | اسم القاضي | كود القاضي | الصفة والعمل بالدائرة | الترتيب |");
                sb.AppendLine("|---|------------|------------|-----------------------|---------|");
                int aIdx = 1;
                foreach (var a in assignments)
                {
                    sb.AppendLine($"| {aIdx++} | **{a.Judge?.FullNameAr}** | `{a.Judge?.Code}` | **{a.JudicialRole?.NameAr}** | {a.DisplayOrder} |");
                }
            }
            sb.AppendLine();

            sb.AppendLine($"### 🗓️ الجلسات التابعة للدائرة ({circuitSessions.Count} جلسة):");
            if (circuitSessions.Count == 0)
            {
                sb.AppendLine("- *لا توجد جلسات مسجلة لهذه الدائرة.*");
            }
            else
            {
                sb.AppendLine("| م | كود الجلسة | اسم الجلسة وميعاد الانعقاد | نوع الجلسة (الدعاوى المصرح بها) | الاختصاص المكاني (النطاق الجغرافي) | سكرتير الجلسة |");
                sb.AppendLine("|---|------------|---------------------------|----------------------------------|------------------------------------|---------------|");
                int sIdx = 1;
                foreach (var s in circuitSessions)
                {
                    var sv = s.Versions.FirstOrDefault();
                    var areas = sv?.Jurisdictions.Select(j => j.GeographicArea?.NameAr).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList() ?? new List<string?>();
                    var areasStr = areas.Count > 0 ? string.Join("، ", areas) : "بدائرة المحافظة";

                    List<string> cts;
                    if (sv != null && sctLookup.TryGetValue(sv.Id, out var foundCts) && foundCts.Count > 0)
                    {
                        cts = foundCts;
                    }
                    else
                    {
                        cts = new List<string> { kindAr };
                    }
                    var ctsStr = string.Join("، ", cts);

                    var sched = !string.IsNullOrWhiteSpace(sv?.ScheduleDescription) ? sv.ScheduleDescription : s.NameAr;
                    var clerk = s.Employee != null ? $"{s.Employee.FullNameAr} ({s.Employee.JobTitle ?? "أمين سر"})" : "غير محدد";

                    sb.AppendLine($"| {sIdx++} | `{s.Code}` | **{s.NameAr}**<br/><small style='color:gray;'>{sched}</small> | {ctsStr} | 📍 {areasStr} | 👨‍💼 {clerk} |");
                }
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        var reportPath = @"C:\Users\ahmed\.gemini\antigravity\brain\afb9bcce-8edd-4f41-aa59-acb5ff0dae46\circuits_2026_2027_report.md";
        File.WriteAllText(reportPath, sb.ToString(), System.Text.Encoding.UTF8);
        _output.WriteLine($"Report generated at {reportPath} with {circuits.Count} circuits.");
    }
}


