using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using JudicialStatistics.Domain.Geography;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Domain.Years;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// استيراد وتهيئة بيانات الدوائر والجلسات والقضاة والتكليفات من ورقة التشكيل بملف الإكسل
/// للعام القضائي 2025/2026
/// </summary>
public static class FormationSeedData
{
    public static async Task SeedAsync(JudicialDbContext context, ILogger? logger = null, string? excelPath = null)
    {
        // 1. التحقق من وجود العام القضائي
        var judicialYear = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == "2025/2026");
        if (judicialYear != null && await context.Circuits.AnyAsync(c => c.JudicialYearId == judicialYear.Id))
        {
            logger?.LogInformation("دوائر العام القضائي 2025/2026 مسجلة بالفعل، تم التخطي.");
            return;
        }

        // 2. العثور على ملف الإكسل
        excelPath ??= FindExcelFile();
        if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
        {
            logger?.LogWarning("لم يتم العثور على ملف التشكيل Excel، تم تخطي استيراد التشكيل.");
            return;
        }

        logger?.LogInformation("جاري قراءة ورقة التشكيل من الملف: {Path}", excelPath);

        if (judicialYear == null)
        {
            var scope = await context.ReportingScopes.FirstOrDefaultAsync();
            if (scope == null)
            {
                logger?.LogError("لا يوجد نطاق تقارير مسجل لربط العام القضائي به.");
                return;
            }

            judicialYear = new JudicialYear
            {
                ReportingScopeId = scope.Id,
                NameAr = "2025/2026",
                StartDate = new DateOnly(2025, 10, 1),
                EndDate = new DateOnly(2026, 9, 30),
                Status = "Active"
            };
            for (int i = 0; i < 12; i++)
            {
                judicialYear.Periods.Add(new StatisticalPeriod
                {
                    MonthStartDate = judicialYear.StartDate.AddMonths(i),
                    IsOpenForEntry = true
                });
            }
            context.JudicialYears.Add(judicialYear);
            await context.SaveChangesAsync();
        }

        // 3. المحكمة التابعة
        var courtUnit = await context.CourtUnits.FirstOrDefaultAsync(c => c.Code == "BNISF-COURT")
                        ?? await context.CourtUnits.FirstOrDefaultAsync();
        if (courtUnit == null)
        {
            logger?.LogError("لا توجد وحدة محكمة مسجلة لإضافة الدوائر إليها.");
            return;
        }

        // 4. نسخة التشكيل للعام
        var orgVersion = await context.OrganizationVersions
            .FirstOrDefaultAsync(v => v.JudicialYearId == judicialYear.Id && v.VersionNo == 1);
        if (orgVersion == null)
        {
            orgVersion = new OrganizationVersion
            {
                JudicialYearId = judicialYear.Id,
                VersionNo = 1,
                DecisionReference = "قرار التشكيل للعام القضائي 2025/2026",
                DecisionDate = judicialYear.StartDate,
                EffectiveFrom = judicialYear.StartDate,
                EffectiveTo = judicialYear.EndDate,
                Status = "Approved"
            };
            context.OrganizationVersions.Add(orgVersion);
            await context.SaveChangesAsync();
        }

        // 5. الصفات القضائية (Judicial Roles)
        var roles = await context.JudicialRoles.ToListAsync();
        var chiefRole = roles.FirstOrDefault(r => r.Code == "CHIEF") ?? roles.First();
        var memberRightRole = roles.FirstOrDefault(r => r.Code == "MEMBER_RIGHT") ?? chiefRole;
        var memberLeftRole = roles.FirstOrDefault(r => r.Code == "MEMBER_LEFT") ?? chiefRole;
        var memberLeft2Role = roles.FirstOrDefault(r => r.Code == "MEMBER_LEFT2")
                              ?? roles.FirstOrDefault(r => r.NameAr.Contains("اليسار"))
                              ?? memberLeftRole;

        JudicialRole ResolveRole(string roleName)
        {
            if (roleName.Contains("يسار اليسار")) return memberLeft2Role;
            if (roleName.Contains("يمين")) return memberRightRole;
            if (roleName.Contains("يسار")) return memberLeftRole;
            return chiefRole;
        }

        // 6. المناطق الجغرافية
        var geoAreas = await context.GeographicAreas.ToListAsync();
        long? ResolveGeoAreaId(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (text.Contains("بندر")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-CITY")?.Id;
            if (text.Contains("الواسط")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-WASITA")?.Id;
            if (text.Contains("ناصر")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-NASER")?.Id;
            if (text.Contains("اهناسيا") || text.Contains("إهناسيا")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-IHNASYA")?.Id;
            if (text.Contains("ببا")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-BEBA")?.Id;
            if (text.Contains("الفشن")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-FASHN")?.Id;
            if (text.Contains("سمسطا")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-SAMASTA")?.Id;
            if (text.Contains("مركز")) return geoAreas.FirstOrDefault(g => g.Code == "BNISF-CENTER")?.Id;
            return null;
        }

        // 7. قراءة ورقة التشكيل من ملف الإكسل
        var parsedData = ExtractFormationFromExcel(excelPath);
        logger?.LogInformation("تم استخراج: {Circuits} دائرة، {Sessions} جلسة، {Judges} قاضي من ورقة التشكيل.",
            parsedData.Circuits.Count, parsedData.Sessions.Count, parsedData.UniqueJudges.Count);

        // 8. حفظ القضاة أولاً
        var existingJudges = await context.Judges.ToListAsync();
        var judgeEntityMap = new Dictionary<string, Judge>();

        int judgeCounter = existingJudges.Count + 1;
        foreach (var jInfo in parsedData.UniqueJudges)
        {
            var existing = existingJudges.FirstOrDefault(x => x.FullNameAr == jInfo.Name);
            if (existing == null)
            {
                var judge = new Judge
                {
                    Code = $"JDG-{judgeCounter:D3}",
                    FullNameAr = jInfo.Name,
                    IsActive = jInfo.IsActive
                };
                context.Judges.Add(judge);
                existingJudges.Add(judge);
                judgeEntityMap[jInfo.Name] = judge;
                judgeCounter++;
            }
            else
            {
                judgeEntityMap[jInfo.Name] = existing;
            }
        }
        await context.SaveChangesAsync();

        // 9. إنشاء الدوائر ونسخ الدوائر
        var circuitVersionMap = new Dictionary<(string Section, string Code), CircuitVersion>();
        var circuitEntityMap = new Dictionary<(string Section, string Code), Circuit>();

        foreach (var c in parsedData.Circuits)
        {
            string circuitCodePrefix = c.Section switch
            {
                "CivilFull" => "CV",
                "Family" => "FAM",
                "CriminalAppeal" => "CRIM",
                "Labor" => "LAB",
                _ => "PART"
            };

            int.TryParse(c.Code, out int cNum);
            string circuitCode = $"{circuitCodePrefix}-{cNum:D2}";

            var circuit = new Circuit
            {
                JudicialYearId = judicialYear.Id,
                CourtUnitId = courtUnit.Id,
                Code = circuitCode,
                NameAr = c.Name,
                IsClosed = false
            };

            short minJudges = (short)(c.Section == "Partial" ? 1 : 3);
            short maxJudges = (short)(c.Section == "Partial" ? 1 : 4);

            var circuitVersion = new CircuitVersion
            {
                Circuit = circuit,
                OrganizationVersionId = orgVersion.Id,
                CircuitKind = c.Section,
                EffectiveFrom = judicialYear.StartDate,
                EffectiveTo = judicialYear.EndDate,
                MinJudges = minJudges,
                MaxJudges = maxJudges
            };

            circuit.Versions.Add(circuitVersion);
            context.Circuits.Add(circuit);

            circuitEntityMap[(c.Section, c.Code)] = circuit;
            circuitVersionMap[(c.Section, c.Code)] = circuitVersion;
        }

        await context.SaveChangesAsync();

        // 10. إنشاء الجلسات ونسخ الجلسات
        foreach (var s in parsedData.Sessions)
        {
            if (!circuitEntityMap.TryGetValue((s.Section, s.CircuitNo), out var circuit) ||
                !circuitVersionMap.TryGetValue((s.Section, s.CircuitNo), out var circuitVersion))
            {
                continue;
            }

            string sPrefix = s.Section switch
            {
                "CivilFull" => "CV-S",
                "Family" => "FAM-S",
                "CriminalAppeal" => "CRIM-S",
                "Labor" => "LAB-S",
                _ => "PART-S"
            };

            int.TryParse(s.SessionNo, out int sNum);
            string sessionCode = $"{sPrefix}{sNum:D2}";

            var session = new Session
            {
                CircuitId = circuit.Id,
                Code = sessionCode,
                NameAr = s.SessionName,
                IsClosed = false
            };

            var sessionVersion = new SessionVersion
            {
                Session = session,
                CircuitVersionId = circuitVersion.Id,
                EffectiveFrom = judicialYear.StartDate,
                EffectiveTo = judicialYear.EndDate,
                ScheduleDescription = $"{s.Day} - {s.SessionType}".Trim(' ', '-')
            };

            var areaId = ResolveGeoAreaId(s.Court) ?? ResolveGeoAreaId(s.SessionName);
            if (areaId.HasValue)
            {
                sessionVersion.Jurisdictions.Add(new SessionJurisdiction
                {
                    GeographicAreaId = areaId.Value
                });
            }

            session.Versions.Add(sessionVersion);
            context.Sessions.Add(session);
        }

        await context.SaveChangesAsync();

        // 11. إنشاء تكليفات القضاة بالدوائر
        short displayOrderCounter = 1;
        foreach (var ja in parsedData.Assignments)
        {
            if (!circuitVersionMap.TryGetValue((ja.Section, ja.CircuitNo), out var cv))
                continue;

            if (!judgeEntityMap.TryGetValue(ja.JudgeName, out var judge))
                continue;

            var role = ResolveRole(ja.Role);

            var assignment = new CircuitJudgeAssignment
            {
                CircuitVersionId = cv.Id,
                JudgeId = judge.Id,
                JudicialRoleId = role.Id,
                EffectiveFrom = judicialYear.StartDate,
                EffectiveTo = judicialYear.EndDate,
                DisplayOrder = displayOrderCounter++
            };

            context.CircuitJudgeAssignments.Add(assignment);
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("اكتمل استيراد تشكيل العام القضائي 2025/2026 بنجاح.");
    }

    private static string? FindExcelFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"),
            Path.Combine(Directory.GetCurrentDirectory(), "نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"),
            @"c:\Users\ahmed\source\repos\JudicialStatistics\JudicialStatistics.Web\نسخة اكتوبر من احصاء 2025 - 2026_استلام_2026-09-21_v1.xlsm"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        // Search directory tree upwards safely
        try
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            if (dir.Exists)
            {
                var match = dir.GetFiles("*.xlsm", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (match != null) return match.FullName;
            }
        }
        catch
        {
            // Ignore access errors on restricted shared hosting
        }

        return null;
    }

    private class ParsedFormation
    {
        public List<ParsedCircuit> Circuits { get; } = [];
        public List<ParsedSession> Sessions { get; } = [];
        public List<ParsedJudgeInfo> UniqueJudges { get; } = [];
        public List<ParsedAssignment> Assignments { get; } = [];
    }

    private record ParsedCircuit(string Section, string Code, string Name, string Court);
    private record ParsedSession(string Section, string CircuitNo, string CircuitName, string SessionNo, string SessionName, string SessionType, string Day, string Court);
    private record ParsedJudgeInfo(string Name, bool IsActive);
    private record ParsedAssignment(string Section, string CircuitNo, string JudgeName, string Role, string Status);

    private static ParsedFormation ExtractFormationFromExcel(string excelPath)
    {
        var result = new ParsedFormation();

        using var zip = ZipFile.OpenRead(excelPath);
        var ssEntry = zip.GetEntry("xl/sharedStrings.xml")!;
        var ssDoc = XDocument.Load(ssEntry.Open());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sharedStrings = new List<string>();
        foreach (var si in ssDoc.Root!.Elements(ns + "si"))
        {
            var t = si.Element(ns + "t");
            if (t != null) sharedStrings.Add(t.Value);
            else sharedStrings.Add(string.Concat(si.Elements(ns + "r").Select(r => r.Element(ns + "t")?.Value ?? "")));
        }

        var sheetDoc = XDocument.Load(zip.GetEntry("xl/worksheets/sheet3.xml")!.Open());

        string GetCellValue(XElement c)
        {
            var val = c.Element(ns + "v")?.Value;
            var type = c.Attribute("t")?.Value;
            if (type == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count)
            {
                return sharedStrings[idx];
            }
            return val ?? "";
        }

        var rows = sheetDoc.Root!.Element(ns + "sheetData")!.Elements(ns + "row").ToList();

        Dictionary<string, string> GetRow(int rNum)
        {
            var r = rows.FirstOrDefault(x => x.Attribute("r")?.Value == rNum.ToString());
            var d = new Dictionary<string, string>();
            if (r != null)
            {
                foreach (var c in r.Elements(ns + "c"))
                {
                    var cellRef = c.Attribute("r")?.Value ?? "";
                    var col = new string(cellRef.Where(char.IsLetter).ToArray());
                    d[col] = GetCellValue(c);
                }
            }
            return d;
        }

        var rawJudges = new List<(string Section, string CircuitNo, string JudgeName, string Role, string Status)>();

        // 1. CivilFull: rows 5 to 53
        for (int r = 5; r <= 53; r++)
        {
            var row = GetRow(r);
            var cNo = row.GetValueOrDefault("A")?.Trim();
            var cName = row.GetValueOrDefault("B")?.Trim();
            if (!string.IsNullOrEmpty(cNo) && !string.IsNullOrEmpty(cName) && int.TryParse(cNo, out _))
                result.Circuits.Add(new ParsedCircuit("CivilFull", cNo, cName, "بني سويف"));

            var sCircuitNo = row.GetValueOrDefault("E")?.Trim();
            var sCircuitName = row.GetValueOrDefault("D")?.Trim();
            var sNo = row.GetValueOrDefault("F")?.Trim();
            var sName = row.GetValueOrDefault("G")?.Trim();
            var sType = row.GetValueOrDefault("H")?.Trim();
            var sDay = row.GetValueOrDefault("I")?.Trim();
            var sCourt = row.GetValueOrDefault("K")?.Trim();
            if (!string.IsNullOrEmpty(sNo) && int.TryParse(sNo, out _))
                result.Sessions.Add(new ParsedSession("CivilFull", sCircuitNo ?? "", sCircuitName ?? "", sNo, sName ?? "", sType ?? "", sDay ?? "", sCourt ?? ""));

            var jName = row.GetValueOrDefault("M")?.Trim();
            var jCircuitNo = row.GetValueOrDefault("P")?.Trim();
            var jRole = row.GetValueOrDefault("R")?.Trim();
            var jStatus = row.GetValueOrDefault("S")?.Trim();
            if (!string.IsNullOrEmpty(jName) && jName != "اسم القاضي")
            {
                rawJudges.Add(("CivilFull", jCircuitNo ?? "", jName, jRole ?? "", jStatus ?? ""));
            }
        }

        // 2. Family: rows 54 to 110
        for (int r = 54; r <= 110; r++)
        {
            var row = GetRow(r);
            var cNo = row.GetValueOrDefault("A")?.Trim();
            var cName = row.GetValueOrDefault("B")?.Trim();
            if (!string.IsNullOrEmpty(cNo) && !string.IsNullOrEmpty(cName) && int.TryParse(cNo, out _))
                result.Circuits.Add(new ParsedCircuit("Family", cNo, cName, "بندر"));

            var sCircuitNo = row.GetValueOrDefault("E")?.Trim();
            var sCircuitName = row.GetValueOrDefault("D")?.Trim();
            var sNo = row.GetValueOrDefault("F")?.Trim();
            var sName = row.GetValueOrDefault("G")?.Trim();
            var sType = row.GetValueOrDefault("H")?.Trim();
            var sDay = row.GetValueOrDefault("I")?.Trim();
            var sCourt = row.GetValueOrDefault("K")?.Trim();
            if (!string.IsNullOrEmpty(sNo) && int.TryParse(sNo, out _))
                result.Sessions.Add(new ParsedSession("Family", sCircuitNo ?? "", sCircuitName ?? "", sNo, sName ?? "", sType ?? "", sDay ?? "", sCourt ?? ""));

            var jNo = row.GetValueOrDefault("L")?.Trim();
            var jName = row.GetValueOrDefault("M")?.Trim();
            var jCircuitNo = row.GetValueOrDefault("P")?.Trim();
            var jRole = row.GetValueOrDefault("R")?.Trim();
            var jStatus = row.GetValueOrDefault("S")?.Trim();
            if (!string.IsNullOrEmpty(jName) && jName != "اسم القاضي" && (int.TryParse(jNo, out _) || !string.IsNullOrEmpty(jRole)))
            {
                rawJudges.Add(("Family", jCircuitNo ?? "", jName, jRole ?? "", jStatus ?? ""));
            }
        }

        // 3. CriminalAppeal: rows 115 to 167
        for (int r = 115; r <= 167; r++)
        {
            var row = GetRow(r);
            var cNo = row.GetValueOrDefault("A")?.Trim();
            var cName = row.GetValueOrDefault("B")?.Trim();
            if (!string.IsNullOrEmpty(cNo) && !string.IsNullOrEmpty(cName) && int.TryParse(cNo, out _))
                result.Circuits.Add(new ParsedCircuit("CriminalAppeal", cNo, cName, "بني سويف"));

            var sCircuitNo = row.GetValueOrDefault("E")?.Trim();
            var sCircuitName = row.GetValueOrDefault("D")?.Trim();
            var sNo = row.GetValueOrDefault("F")?.Trim();
            var sName = row.GetValueOrDefault("G")?.Trim();
            var sType = row.GetValueOrDefault("H")?.Trim();
            var sDay = row.GetValueOrDefault("I")?.Trim();
            var sCourt = row.GetValueOrDefault("K")?.Trim();
            if (!string.IsNullOrEmpty(sNo) && int.TryParse(sNo, out _))
                result.Sessions.Add(new ParsedSession("CriminalAppeal", sCircuitNo ?? "", sCircuitName ?? "", sNo, sName ?? "", sType ?? "", sDay ?? "", sCourt ?? ""));

            var jName = row.GetValueOrDefault("M")?.Trim();
            var jCircuitNo = row.GetValueOrDefault("P")?.Trim();
            var jRole = row.GetValueOrDefault("R")?.Trim();
            var jStatus = row.GetValueOrDefault("S")?.Trim();
            if (!string.IsNullOrEmpty(jName) && jName != "اسم القاضي")
            {
                rawJudges.Add(("CriminalAppeal", jCircuitNo ?? "", jName, jRole ?? "", jStatus ?? ""));
            }
        }

        // 4. Labor: rows 171 to 214
        for (int r = 171; r <= 214; r++)
        {
            var row = GetRow(r);
            var cNo = row.GetValueOrDefault("A")?.Trim();
            var cName = row.GetValueOrDefault("B")?.Trim();
            if (!string.IsNullOrEmpty(cNo) && !string.IsNullOrEmpty(cName) && int.TryParse(cNo, out _))
                result.Circuits.Add(new ParsedCircuit("Labor", cNo, cName, "المحكمة العمالية بني سويف"));

            var sCircuitNo = row.GetValueOrDefault("E")?.Trim();
            var sCircuitName = row.GetValueOrDefault("D")?.Trim();
            var sNo = row.GetValueOrDefault("F")?.Trim();
            var sName = row.GetValueOrDefault("G")?.Trim();
            var sType = row.GetValueOrDefault("H")?.Trim();
            var sDay = row.GetValueOrDefault("I")?.Trim();
            var sCourt = row.GetValueOrDefault("K")?.Trim();
            if (!string.IsNullOrEmpty(sNo) && int.TryParse(sNo, out _))
                result.Sessions.Add(new ParsedSession("Labor", sCircuitNo ?? "", sCircuitName ?? "", sNo, sName ?? "", sType ?? "", sDay ?? "", sCourt ?? ""));

            var jName = row.GetValueOrDefault("M")?.Trim();
            var jCircuitNo = row.GetValueOrDefault("P")?.Trim();
            var jRole = row.GetValueOrDefault("R")?.Trim();
            var jStatus = row.GetValueOrDefault("S")?.Trim();
            if (!string.IsNullOrEmpty(jName) && jName != "اسم القاضي")
            {
                rawJudges.Add(("Labor", jCircuitNo ?? "", jName, jRole ?? "", jStatus ?? ""));
            }
        }

        // 5. Partial: rows 219 to 274
        for (int r = 219; r <= 274; r++)
        {
            var row = GetRow(r);
            var cNo = row.GetValueOrDefault("A")?.Trim();
            var cName = row.GetValueOrDefault("B")?.Trim();
            var cCourt = row.GetValueOrDefault("C")?.Trim();
            if (!string.IsNullOrEmpty(cNo) && !string.IsNullOrEmpty(cName) && int.TryParse(cNo, out _))
                result.Circuits.Add(new ParsedCircuit("Partial", cNo, cName, cCourt ?? ""));

            var sCircuitNo = row.GetValueOrDefault("F")?.Trim();
            var sCircuitName = row.GetValueOrDefault("E")?.Trim();
            var sNo = row.GetValueOrDefault("G")?.Trim();
            var sName = row.GetValueOrDefault("H")?.Trim();
            var sType = row.GetValueOrDefault("I")?.Trim();
            var sDay = row.GetValueOrDefault("J")?.Trim();
            var sCourt = row.GetValueOrDefault("L")?.Trim();
            if (!string.IsNullOrEmpty(sNo) && int.TryParse(sNo, out _))
                result.Sessions.Add(new ParsedSession("Partial", sCircuitNo ?? "", sCircuitName ?? "", sNo, sName ?? "", sType ?? "", sDay ?? "", sCourt ?? ""));

            var jName = row.GetValueOrDefault("N")?.Trim();
            var jCircuitNo = row.GetValueOrDefault("Q")?.Trim();
            var jRole = row.GetValueOrDefault("S")?.Trim();
            var jStatus = row.GetValueOrDefault("T")?.Trim();
            if (!string.IsNullOrEmpty(jName) && jName != "اسم القاضي")
            {
                rawJudges.Add(("Partial", jCircuitNo ?? "", jName, jRole ?? "", jStatus ?? ""));
            }
        }

        // Distinct Judges and Assignments
        // تنقية أسماء القضاة
        string CleanName(string name)
        {
            var n = name.Replace("(مرضي)", "").Replace("(منتدب)", "").Trim();
            return n;
        }

        var uniqueJudgeDict = new Dictionary<string, bool>();
        foreach (var j in rawJudges)
        {
            var cleanName = CleanName(j.JudgeName);
            bool isActive = j.Status != "لا يعمل";
            if (!uniqueJudgeDict.ContainsKey(cleanName) || isActive)
            {
                uniqueJudgeDict[cleanName] = isActive;
            }
        }

        foreach (var kvp in uniqueJudgeDict)
        {
            result.UniqueJudges.Add(new ParsedJudgeInfo(kvp.Key, kvp.Value));
        }

        foreach (var j in rawJudges)
        {
            var cleanName = CleanName(j.JudgeName);
            if (int.TryParse(j.CircuitNo, out int cNo) && cNo > 0)
            {
                result.Assignments.Add(new ParsedAssignment(j.Section, j.CircuitNo, cleanName, j.Role, j.Status));
            }
        }

        return result;
    }
}
