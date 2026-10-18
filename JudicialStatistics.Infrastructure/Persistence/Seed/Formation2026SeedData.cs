using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Courts;
using JudicialStatistics.Domain.Geography;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Domain.Years;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// بذار بيانات العام القضائي 2026/2027 ودوائره وجلساته وتشكيلاته القضائية
/// مستخرج ومصفى من ملف الجمعية العمومية 2026-2027 (الجمعية_العمومية_2026-2027_v2.xlsx)
/// </summary>
public static class Formation2026SeedData
{
    private const string YearName = "2026/2027";
    private static readonly DateOnly StartDate = new(2026, 10, 1);
    private static readonly DateOnly EndDate = new(2027, 9, 30);

    public static async Task SeedAsync(JudicialDbContext context, ILogger? logger = null, string? excelPath = null)
    {
        logger?.LogInformation("بدء تهيئة العام القضائي 2026/2027 وتشكيل الجمعية العمومية...");

        // 1. العام القضائي 2026/2027
        var judicialYear = await context.JudicialYears
            .Include(y => y.Periods)
            .FirstOrDefaultAsync(y => y.NameAr == YearName || y.NameAr == "2026-2027");

        if (judicialYear == null)
        {
            var scope = await context.ReportingScopes.FirstOrDefaultAsync(s => s.Code == "BNISF-SCOPE")
                        ?? await context.ReportingScopes.FirstOrDefaultAsync();

            if (scope == null)
            {
                logger?.LogError("لا يوجد نطاق تقارير مسجل لربط العام القضائي 2026/2027 به.");
                return;
            }

            judicialYear = new JudicialYear
            {
                ReportingScopeId = scope.Id,
                NameAr = YearName,
                StartDate = StartDate,
                EndDate = EndDate,
                Status = "Active"
            };

            for (int i = 0; i < 12; i++)
            {
                judicialYear.Periods.Add(new StatisticalPeriod
                {
                    MonthStartDate = StartDate.AddMonths(i),
                    IsOpenForEntry = true
                });
            }

            context.JudicialYears.Add(judicialYear);
            await context.SaveChangesAsync();
            logger?.LogInformation("تم إنشاء العام القضائي 2026/2027 و12 فترة إحصائية شهرية بنجاح.");
        }
        else if (judicialYear.Periods.Count < 12)
        {
            for (int i = 0; i < 12; i++)
            {
                var mStart = StartDate.AddMonths(i);
                if (!judicialYear.Periods.Any(p => p.MonthStartDate == mStart))
                {
                    judicialYear.Periods.Add(new StatisticalPeriod
                    {
                        MonthStartDate = mStart,
                        IsOpenForEntry = true
                    });
                }
            }
            await context.SaveChangesAsync();
        }

        // 2. نسخة التشكيل المعتمدة للعام
        var orgVersion = await context.OrganizationVersions
            .FirstOrDefaultAsync(v => v.JudicialYearId == judicialYear.Id && v.VersionNo == 1);

        if (orgVersion == null)
        {
            orgVersion = new OrganizationVersion
            {
                JudicialYearId = judicialYear.Id,
                VersionNo = 1,
                DecisionReference = "الجمعية العمومية للعام القضائي 2026/2027",
                DecisionDate = StartDate,
                EffectiveFrom = StartDate,
                EffectiveTo = EndDate,
                Status = "Approved"
            };
            context.OrganizationVersions.Add(orgVersion);
            await context.SaveChangesAsync();
        }

        // 3. تهيئة أنواع القضايا الجديدة (الجنائي والجنح والعمال)
        var caseTypeMap = await EnsureCaseTypesAsync(context, logger);

        // 4. سياسات سنوات القيد والضوابط كالعام السابق
        await EnsureCaseYearPoliciesAsync(context, judicialYear.Id, caseTypeMap, logger);

        // 5. تهيئة جدول الموظفين الأوليين (سكرتيري الجلسات وموظفي المحكمة)
        await EnsureEmployeesAsync(context, logger);

        // 6. التحقق مما إذا كانت دوائر العام قد استوردت مسبقاً
        if (await context.Circuits.AnyAsync(c => c.JudicialYearId == judicialYear.Id))
        {
            await UpdateExistingSessionsAsync(context, logger);
            logger?.LogInformation("دوائر العام القضائي 2026/2027 مسجلة بالفعل، تم تحديث مسميات وتوصيفات الجلسات وحالة الظهور.");
            return;
        }

        // 7. العثور على ملف الجمعية العمومية Excel
        excelPath ??= FindExcelFile();
        if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
        {
            logger?.LogWarning("لم يتم العثور على ملف إكسيل الجمعية العمومية 2026-2027 في المسارات الافتراضية.");
            return;
        }

        logger?.LogInformation("جاري استخراج بيانات الجمعية العمومية 2026-2027 من: {Path}", excelPath);

        // 8. قراءة واستخراج البيانات من شيتات الإكسيل بأمان عبر ZipArchive و XDocument
        var parsedData = ExtractAssemblyData(excelPath, logger);

        // 9. ربط واستيراد وحدات المحاكم
        var courts = await context.CourtUnits.ToListAsync();
        var courtMap = courts.ToDictionary(c => c.Code, c => c);
        var rootCourt = courtMap.GetValueOrDefault("BNISF-COURT") ?? courts.First();

        CourtUnit ResolveCourtLocal(string circuitCode) => ResolveCourt(circuitCode, courtMap, rootCourt);

        string ResolveCircuitKind(string code, string category)
        {
            if (category.Contains("جنائي") || (int.TryParse(code.Replace("C", ""), out int num) && num <= 8))
                return "CriminalAppeal";
            if (category.Contains("أسرة") || (num >= 17 && num <= 24))
                return "Family";
            if (category.Contains("عمال") || (num >= 47 && num <= 49))
                return "Labor";
            if (category.Contains("جزئي") || (num >= 25 && num <= 46))
                return "Partial";
            return "CivilFull";
        }

        // 10. حفظ القضاة الجدد
        var existingJudges = await context.Judges.ToListAsync();
        var judgeEntityMap = new Dictionary<string, Judge>();
        int judgeCounter = existingJudges.Count + 1;

        foreach (var jInfo in parsedData.UniqueJudges)
        {
            var existing = existingJudges.FirstOrDefault(x => x.FullNameAr.Trim() == jInfo.Trim());
            if (existing == null)
            {
                var judge = new Judge
                {
                    Code = $"JDG-{judgeCounter:D3}",
                    FullNameAr = jInfo.Trim(),
                    IsActive = true
                };
                context.Judges.Add(judge);
                existingJudges.Add(judge);
                judgeEntityMap[jInfo.Trim()] = judge;
                judgeCounter++;
            }
            else
            {
                judgeEntityMap[jInfo.Trim()] = existing;
            }
        }
        await context.SaveChangesAsync();

        // 11. إنشاء الدوائر ونسخ الدوائر
        var circuitEntityMap = new Dictionary<string, Circuit>();
        var circuitVersionMap = new Dictionary<string, CircuitVersion>();

        foreach (var c in parsedData.Circuits)
        {
            var targetCourt = ResolveCourtLocal(c.Code);
            var circuitKind = ResolveCircuitKind(c.Code, c.Category);

            string circuitName = circuitKind == "Partial"
                ? ResolvePartialCircuitName(c.Code, c.Name, c.Scope)
                : c.Name;

            var circuit = new Circuit
            {
                JudicialYearId = judicialYear.Id,
                CourtUnitId = targetCourt.Id,
                Code = c.Code,
                NameAr = circuitName,
                IsClosed = false
            };

            short minJudges = (short)(circuitKind == "Partial" ? 1 : 3);
            short maxJudges = (short)(circuitKind == "Partial" ? 1 : 4);

            var circuitVersion = new CircuitVersion
            {
                Circuit = circuit,
                OrganizationVersionId = orgVersion.Id,
                CircuitKind = circuitKind,
                EffectiveFrom = StartDate,
                EffectiveTo = EndDate,
                MinJudges = minJudges,
                MaxJudges = maxJudges
            };

            circuit.Versions.Add(circuitVersion);
            context.Circuits.Add(circuit);

            circuitEntityMap[c.Code] = circuit;
            circuitVersionMap[c.Code] = circuitVersion;
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("تم حفظ {Count} دائرة للعام القضائي 2026/2027.", circuitEntityMap.Count);

        // 12. إنشاء تكليفات القضاة بالدوائر (CircuitJudgeAssignment)
        var roles = await context.JudicialRoles.ToListAsync();
        var chiefRole = roles.FirstOrDefault(r => r.Code == "CHIEF") ?? roles.First();
        var memberRightRole = roles.FirstOrDefault(r => r.Code == "MEMBER_RIGHT") ?? chiefRole;
        var memberLeftRole = roles.FirstOrDefault(r => r.Code == "MEMBER_LEFT") ?? chiefRole;
        var memberLeft2Role = roles.FirstOrDefault(r => r.Code == "MEMBER_LEFT2")
                              ?? roles.FirstOrDefault(r => r.NameAr.Contains("اليسار"))
                              ?? memberLeftRole;

        JudicialRole ResolveRoleByOrder(int order, string roleText)
        {
            if (order == 1) return chiefRole;
            if (order == 2) return memberRightRole;
            if (order == 3) return memberLeftRole;
            if (order >= 4) return memberLeft2Role;

            if (roleText.Contains("يمين")) return memberRightRole;
            if (roleText.Contains("يسار اليسار")) return memberLeft2Role;
            if (roleText.Contains("يسار")) return memberLeftRole;
            return chiefRole;
        }

        foreach (var ja in parsedData.JudgeAssignments)
        {
            if (!circuitVersionMap.TryGetValue(ja.CircuitCode, out var cv)) continue;
            if (!judgeEntityMap.TryGetValue(ja.JudgeName.Trim(), out var judge)) continue;

            var role = ResolveRoleByOrder(ja.Order, ja.RoleText);

            var assignment = new CircuitJudgeAssignment
            {
                CircuitVersionId = cv.Id,
                JudgeId = judge.Id,
                JudicialRoleId = role.Id,
                EffectiveFrom = StartDate,
                EffectiveTo = EndDate,
                DisplayOrder = (short)ja.Order
            };

            context.CircuitJudgeAssignments.Add(assignment);
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("تم حفظ {Count} تكليف قضائي للسادة القضاة.", parsedData.JudgeAssignments.Count);

        // 13. إنشاء الجلسات وتحديد اختصاصها المكاني والنوعي وسكرتير الجلسة
        var geoAreas = await context.GeographicAreas.ToListAsync();
        var geoMap = geoAreas.ToDictionary(g => g.Code, g => g.Id);

        List<long> ResolveSpatialJurisdiction(string sessionName, string circuitScope, string circuitCode)
        {
            var matched = new HashSet<long>();
            var text = sessionName + " " + circuitScope;

            void AddGeo(string code)
            {
                if (geoMap.TryGetValue(code, out var id)) matched.Add(id);
            }

            if (text.Contains("بدائرة المحافظة") || text.Contains("المحافظة") || text.Contains("كلي") || text.Contains("عمالية"))
            {
                AddGeo("BNISF-GOV");
            }

            if (text.Contains("البندر والمركز"))
            {
                AddGeo("BNISF-CITY");
                AddGeo("BNISF-CENTER");
            }
            else
            {
                if (text.Contains("بندر")) AddGeo("BNISF-CITY");
                if (text.Contains("مركز بني سويف")) AddGeo("BNISF-CENTER");
            }

            if (text.Contains("الواسط")) AddGeo("BNISF-WASITA");
            if (text.Contains("ناصر")) AddGeo("BNISF-NASER");
            if (text.Contains("اهناسيا") || text.Contains("إهناسيا")) AddGeo("BNISF-IHNASYA");
            if (text.Contains("ببا")) AddGeo("BNISF-BEBA");
            if (text.Contains("الفشن")) AddGeo("BNISF-FASHN");
            if (text.Contains("سمسطا")) AddGeo("BNISF-SAMASTA");

            if (matched.Count == 0)
            {
                // Fallback to circuit scope or court unit default
                if (circuitScope.Contains("بندر")) AddGeo("BNISF-CITY");
                else if (circuitScope.Contains("الواسط")) AddGeo("BNISF-WASITA");
                else if (circuitScope.Contains("ناصر")) AddGeo("BNISF-NASER");
                else if (circuitScope.Contains("اهناسيا") || circuitScope.Contains("إهناسيا")) AddGeo("BNISF-IHNASYA");
                else if (circuitScope.Contains("ببا")) AddGeo("BNISF-BEBA");
                else if (circuitScope.Contains("الفشن")) AddGeo("BNISF-FASHN");
                else if (circuitScope.Contains("سمسطا")) AddGeo("BNISF-SAMASTA");
                else if (circuitScope.Contains("مركز")) AddGeo("BNISF-CENTER");
                else AddGeo("BNISF-GOV");
            }

            return matched.ToList();
        }

        List<long> ResolveCaseTypeIds(string sessionName, string circuitKind, Dictionary<string, CaseType> ctMap)
        {
            var res = new List<long>();

            void TryAdd(string code)
            {
                if (ctMap.TryGetValue(code, out var ct) && !res.Contains(ct.Id))
                    res.Add(ct.Id);
            }

            var text = sessionName;

            if (circuitKind == "Family")
            {
                if (text.Contains("نفس")) TryAdd("FAM-NAFS");
                if (text.Contains("مال")) TryAdd("FAM-MAL");
                if (text.Contains("حبس")) TryAdd("FAM-HABAS");
                if (res.Count == 0)
                {
                    TryAdd("FAM-NAFS");
                    TryAdd("FAM-MAL");
                }
                return res;
            }

            if (circuitKind == "Labor")
            {
                if (text.Contains("تأمين") || text.Contains("تأمينات")) TryAdd("LABOR-INSUR");
                if (text.Contains("تعويض")) TryAdd("LABOR-COMP");
                if (text.Contains("جنح عمال")) TryAdd("LABOR-CRIM");
                if (text.Contains("عمال كلي") || res.Count == 0) TryAdd("LABOR-MAIN");
                return res;
            }

            if (circuitKind == "CivilFull")
            {
                if (text.Contains("مستعجل"))
                {
                    TryAdd("CIV-FULL-APP-URG");
                    TryAdd("CIV-FULL-APP-URGENT");
                }
                else if (text.Contains("أوقاف") && text.Contains("مستأنف"))
                {
                    TryAdd("CIV-FULL-APP-AWQ");
                    TryAdd("CIV-FULL-APP-AWQAF");
                }
                else if (text.Contains("مستأنف"))
                {
                    TryAdd("CIV-FULL-APP-MAIN");
                    TryAdd("CIV-FULL-APP");
                }
                else if (text.Contains("تجاري") && text.Contains("مستأنف")) TryAdd("CV-APP-COMM");
                else if (text.Contains("تجاري")) { TryAdd("CIV-FULL-COMM"); TryAdd("CV-PLEN-COMM"); }
                else if (text.Contains("تعويض")) { TryAdd("CIV-FULL-COMP"); TryAdd("CV-PLEN-COMP"); }
                else if (text.Contains("حكومة")) { TryAdd("CIV-FULL-GOV"); TryAdd("CV-PLEN-GOV"); }
                else if (text.Contains("إيجارات") || text.Contains("ايجارات")) TryAdd("CIV-FULL-RENT");
                else if (text.Contains("أوقاف") || text.Contains("اوقاف")) TryAdd("CIV-FULL-AWQAF");
                else if (text.Contains("مدني كلي") || res.Count == 0) { TryAdd("CIV-FULL-MAIN"); TryAdd("CV-PLEN-CIV"); }
                return res;
            }

            // CriminalAppeal & Partial
            bool isAppeal = circuitKind == "CriminalAppeal";

            if (text.Contains("صحة توقيع") || text.Contains("توقيع"))
            {
                TryAdd("CIV-PART-SIGN");
                return res;
            }

            if (text.Contains("مستعجل"))
            {
                TryAdd("CIV-PART-URG");
                return res;
            }

            if (text.Contains("حكومة"))
            {
                TryAdd("CIV-PART-GOV");
                return res;
            }

            if (text.Contains("أمن البلاد")) TryAdd("CRIM-SECURITY");
            if (text.Contains("بيئة") || text.Contains("البيئة")) TryAdd(isAppeal ? "CRIM-APP-ENV" : "CRIM-PART-ENV");
            if (text.Contains("مرور") || text.Contains("المرور")) TryAdd(isAppeal ? "CRIM-APP-TRAFFIC" : "CRIM-PART-TRAFFIC");
            if (text.Contains("إرهاب") || text.Contains("الإرهاب")) TryAdd(isAppeal ? "CRIM-APP-TERROR" : "CRIM-PART-TERROR");
            if (text.Contains("طفل") || text.Contains("الطفل"))
            {
                if (text.Contains("عنف")) TryAdd(isAppeal ? "CRIM-APP-CHILD-VIOL" : "CRIM-PART-CHILD-VIOL");
                else TryAdd(isAppeal ? "CRIM-CHILD-APP" : "CRIM-CHILD-FIRST");
            }
            if (text.Contains("مخدر") || text.Contains("المخدرة")) TryAdd(isAppeal ? "CRIM-APP-DRUGS" : "CRIM-PART-DRUGS");
            if (text.Contains("هجرة") || text.Contains("الهجرة")) TryAdd(isAppeal ? "CRIM-APP-IMMIG" : "CRIM-PART-IMMIG");
            if (text.Contains("المرأة") || text.Contains("العنف ضد المرأة")) TryAdd("CRIM-VIOLENCE");
            if (text.Contains("بناء") || text.Contains("أملاك الدولة")) TryAdd(isAppeal ? "CRIM-BLDG" : "CRIM-PART-STATE");
            if (text.Contains("جنح عمال")) TryAdd("LABOR-CRIM");

            if (res.Count == 0)
            {
                if (isAppeal)
                {
                    if (text.Contains("بندر")) TryAdd("CRIM-APP-BANDAR");
                    else if (text.Contains("مركز")) TryAdd("CRIM-APP-MARKAZ");
                    else TryAdd("CRIM-APP-BANDAR");
                }
                else
                {
                    if (text.Contains("مدني")) { TryAdd("CIV-PART-MAIN"); TryAdd("CV-PART-CIV"); }
                    else if (text.Contains("تجاري")) { TryAdd("CIV-PART-MAIN"); TryAdd("CV-PART-COMM"); }
                    else TryAdd("CRIM-PART-GEN");
                }
            }

            return res;
        }

        var allEmployees = await context.Employees.Where(e => e.IsActive).ToListAsync();
        int empIdx = 0;

        foreach (var s in parsedData.Sessions)
        {
            if (!circuitEntityMap.TryGetValue(s.CircuitCode, out var circuit) ||
                !circuitVersionMap.TryGetValue(s.CircuitCode, out var cv))
            {
                continue;
            }

            long? assignedClerkId = null;
            if (allEmployees.Count > 0)
            {
                // Assign a clerk to sessions based on court unit
                var courtEmployees = allEmployees.Where(e => e.CourtUnitId == circuit.CourtUnitId).ToList();
                if (courtEmployees.Count > 0)
                {
                    assignedClerkId = courtEmployees[empIdx % courtEmployees.Count].Id;
                    empIdx++;
                }
                else
                {
                    assignedClerkId = allEmployees[empIdx % allEmployees.Count].Id;
                    empIdx++;
                }
            }

            var day = string.IsNullOrWhiteSpace(s.Day) ? (ExtractDay(s.SessionName) ?? "السبت") : s.Day;
            var abbr = GetCircuitAbbreviation(circuit.NameAr);
            var areaName = !string.IsNullOrWhiteSpace(s.CourtScope) ? s.CourtScope : "محافظة بني سويف";

            var cleanName = s.SessionName.Replace(" - إضافة مكتوبة: أول درجة", " أول درجة")
                                         .Replace("بدائرة المحافظة", "")
                                         .Trim();

            bool showInReport = DetermineShowInProgressReport(cleanName, cv.CircuitKind);
            string formattedDesc = FormatDescription(day, cleanName, areaName, abbr);
            string formattedName = FormatNameAr(cleanName, day);

            var session = new Session
            {
                CircuitId = circuit.Id,
                Code = s.SessionCode,
                NameAr = formattedName,
                Description = formattedDesc,
                ShowInProgressReport = showInReport,
                IsClosed = false,
                EmployeeId = assignedClerkId
            };

            var sessionVersion = new SessionVersion
            {
                Session = session,
                CircuitVersionId = cv.Id,
                EffectiveFrom = StartDate,
                EffectiveTo = EndDate,
                ScheduleDescription = formattedDesc
            };

            var areaIds = ResolveSpatialJurisdiction(s.SessionName, s.CourtScope, s.CircuitCode);
            foreach (var aId in areaIds)
            {
                sessionVersion.Jurisdictions.Add(new SessionJurisdiction
                {
                    GeographicAreaId = aId
                });
            }

            var ctIds = ResolveCaseTypeIds(s.SessionName, cv.CircuitKind, caseTypeMap);
            session.Versions.Add(sessionVersion);
            context.Sessions.Add(session);
            await context.SaveChangesAsync();

            foreach (var cId in ctIds)
            {
                context.SessionCaseTypes.Add(new SessionCaseType
                {
                    SessionVersionId = sessionVersion.Id,
                    CaseTypeId = cId
                });
            }
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("تم حفظ {Count} جلسة قضائية وتحديد اختصاصها المكاني والنوعي وسكرتيريها للعام القضائي 2026/2027 بنجاح.", parsedData.Sessions.Count);
    }

    private static async Task<Dictionary<string, CaseType>> EnsureCaseTypesAsync(JudicialDbContext context, ILogger? logger)
    {
        // 1. التأكد من تصنيفات القضايا (Taxonomies)
        var taxNodes = await context.CaseTaxonomyNodes.ToDictionaryAsync(x => x.Code);
        if (!taxNodes.ContainsKey("CRIMINAL"))
        {
            var node = new CaseTaxonomyNode { Code = "CRIMINAL", NameAr = "قضايا الجنح والجنائي", SortOrder = 4 };
            context.CaseTaxonomyNodes.Add(node);
            taxNodes["CRIMINAL"] = node;
            await context.SaveChangesAsync();
        }
        if (!taxNodes.ContainsKey("LABOR"))
        {
            var node = new CaseTaxonomyNode { Code = "LABOR", NameAr = "قضايا العمال والتأمينات", SortOrder = 3 };
            context.CaseTaxonomyNodes.Add(node);
            taxNodes["LABOR"] = node;
            await context.SaveChangesAsync();
        }
        if (!taxNodes.ContainsKey("CIVIL"))
        {
            var node = new CaseTaxonomyNode { Code = "CIVIL", NameAr = "قضايا المدني والتجاري", SortOrder = 1 };
            context.CaseTaxonomyNodes.Add(node);
            taxNodes["CIVIL"] = node;
            await context.SaveChangesAsync();
        }

        var crimTax = taxNodes["CRIMINAL"];
        var laborTax = taxNodes["LABOR"];
        var civilTax = taxNodes["CIVIL"];

        // 2. قائمة أنواع القضايا الجديدة والمطلوبة
        var newDefinitions = new[]
        {
            new { Code = "CRIM-SECURITY",       NameAr = "القضايا الماسة بأمن البلاد",                  TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-ENV",        NameAr = "جنح مستأنف البيئة",                         TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-ENV",       NameAr = "جنح البيئة",                                TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-TRAFFIC",    NameAr = "جنح مستأنف المرور",                         TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-TRAFFIC",   NameAr = "جنح ومخالفات وتظلمات المرور",                TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-TERROR",     NameAr = "جنح مستأنف الإرهاب",                        TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-TERROR",    NameAr = "جنح الإرهاب",                               TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-CHILD-VIOL", NameAr = "جنح مستأنف العنف ضد الطفل",                 TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-CHILD-VIOL",NameAr = "جنح العنف ضد الطفل",                        TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-DRUGS",      NameAr = "جنح مستأنف التعاطي واتجار المواد المخدرة",   TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-DRUGS",     NameAr = "جنح التعاطي والاتجار في المواد المخدرة",     TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-IMMIG",      NameAr = "جنح مستأنف الهجرة غير الشرعية",             TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-IMMIG",     NameAr = "جنح الهجرة غير الشرعية",                    TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-CHILD-FIRST",    NameAr = "جنايات وجنح الطفل - أول درجة",              TaxId = crimTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Juvenile" },
            new { Code = "CRIM-CHILD-APP",      NameAr = "قضايا جنايات وجنح الطفل مستأنف",            TaxId = crimTax.Id, Level = "Appeal",        Proc = "Summary",  Age = "Juvenile" },
            new { Code = "LABOR-CRIM",          NameAr = "جنح عمال",                                  TaxId = laborTax.Id, Level = "FirstInstance", Proc = "Ordinary", Age = "Mixed" },
            new { Code = "CV-PART-CIV",         NameAr = "مدني جزئي",                                 TaxId = civilTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CV-PART-COMM",        NameAr = "تجاري جزئي",                                TaxId = civilTax.Id, Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
        };

        var existingCaseTypes = await context.CaseTypes.ToDictionaryAsync(c => c.Code);

        foreach (var def in newDefinitions)
        {
            if (!existingCaseTypes.TryGetValue(def.Code, out var ct))
            {
                ct = new CaseType
                {
                    TaxonomyNodeId = def.TaxId,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    StatisticalLevelCode = def.Level,
                    ProcedureCode = def.Proc,
                    AgeCategoryCode = def.Age
                };
                context.CaseTypes.Add(ct);
                existingCaseTypes[def.Code] = ct;
            }
        }

        await context.SaveChangesAsync();
        return existingCaseTypes;
    }

    private static async Task EnsureCaseYearPoliciesAsync(
        JudicialDbContext context,
        long judicialYearId,
        Dictionary<string, CaseType> caseTypeMap,
        ILogger? logger)
    {
        foreach (var (code, ct) in caseTypeMap)
        {
            var policy = await context.CaseYearPolicies
                .Include(p => p.Bands)
                .FirstOrDefaultAsync(p => p.JudicialYearId == judicialYearId && p.CaseTypeId == ct.Id);

            if (policy == null)
            {
                policy = new CaseYearPolicy
                {
                    JudicialYearId = judicialYearId,
                    CaseTypeId = ct.Id,
                    VersionNo = 1,
                    DecisionReference = $"ضوابط وسنوات قيد {ct.NameAr} للعام 2026/2027",
                    Status = "Approved"
                };

                if (code.StartsWith("CRIM-"))
                {
                    // سنة قيد واحدة فقط للجنائي
                    policy.Bands.Add(new CaseYearBand
                    {
                        BandKind = "AllYears",
                        LabelAr = "السنة الإحصائية",
                        SortOrder = 1
                    });
                }
                else
                {
                    // شرائح متعددة للأسرة والعمال والمدني كما العام السابق
                    policy.Bands.Add(new CaseYearBand { BandKind = "UpTo", LabelAr = "2023 وما قبلها", SortOrder = 1, ToYear = 2023 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2024", SortOrder = 2, FromYear = 2024, ToYear = 2024 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2025", SortOrder = 3, FromYear = 2025, ToYear = 2025 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2026", SortOrder = 4, FromYear = 2026, ToYear = 2026 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2027", SortOrder = 5, FromYear = 2027, ToYear = 2027 });
                }

                context.CaseYearPolicies.Add(policy);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureEmployeesAsync(JudicialDbContext context, ILogger? logger)
    {
        if (await context.Employees.AnyAsync()) return;

        var courtUnits = await context.CourtUnits.ToListAsync();
        var bnisfPlenary = courtUnits.FirstOrDefault(c => c.Code == "BNISF-PLENARY");
        var wastaPlenary = courtUnits.FirstOrDefault(c => c.Code == "WASTA-PLENARY");
        var bebaPlenary = courtUnits.FirstOrDefault(c => c.Code == "BEBA-PLENARY");
        var bandarPartial = courtUnits.FirstOrDefault(c => c.Code == "BANDAR-PARTIAL");
        var famCourt = courtUnits.FirstOrDefault(c => c.Code == "FAM-COURT");
        var labCourt = courtUnits.FirstOrDefault(c => c.Code == "LAB-COURT");

        var seedEmployees = new[]
        {
            new Employee { Code = "EMP-001", FullNameAr = "محمد عبد العظيم حسن", JobTitle = "سكرتير جلسة أول", CourtUnitId = bnisfPlenary?.Id, PhoneNumber = "01012345601", IsActive = true },
            new Employee { Code = "EMP-002", FullNameAr = "أحمد سيد عبد السلام", JobTitle = "سكرتير جلسة جنائي", CourtUnitId = bnisfPlenary?.Id, PhoneNumber = "01012345602", IsActive = true },
            new Employee { Code = "EMP-003", FullNameAr = "محمود عادل إبراهيم", JobTitle = "سكرتير جلسة مدني", CourtUnitId = bnisfPlenary?.Id, PhoneNumber = "01012345603", IsActive = true },
            new Employee { Code = "EMP-004", FullNameAr = "خالد مصطفى فؤاد", JobTitle = "سكرتير جلسة", CourtUnitId = wastaPlenary?.Id, PhoneNumber = "01012345604", IsActive = true },
            new Employee { Code = "EMP-005", FullNameAr = "ياسر كمال طه", JobTitle = "سكرتير جلسة", CourtUnitId = wastaPlenary?.Id, PhoneNumber = "01012345605", IsActive = true },
            new Employee { Code = "EMP-006", FullNameAr = "حسام الدين صابر عوض", JobTitle = "سكرتير جلسة", CourtUnitId = bebaPlenary?.Id, PhoneNumber = "01012345606", IsActive = true },
            new Employee { Code = "EMP-007", FullNameAr = "عمرو ممدوح سالم", JobTitle = "سكرتير جلسة", CourtUnitId = bebaPlenary?.Id, PhoneNumber = "01012345607", IsActive = true },
            new Employee { Code = "EMP-008", FullNameAr = "عصام نبيل عبد الهادي", JobTitle = "سكرتير جلسة أسرة", CourtUnitId = famCourt?.Id, PhoneNumber = "01012345608", IsActive = true },
            new Employee { Code = "EMP-009", FullNameAr = "وليد مجدي قرني", JobTitle = "سكرتير جلسة أسرة", CourtUnitId = famCourt?.Id, PhoneNumber = "01012345609", IsActive = true },
            new Employee { Code = "EMP-010", FullNameAr = "شريف علاء فاروق", JobTitle = "سكرتير جلسة عمال", CourtUnitId = labCourt?.Id, PhoneNumber = "01012345610", IsActive = true },
            new Employee { Code = "EMP-011", FullNameAr = "أيمن صلاح الدين عثمان", JobTitle = "سكرتير جلسة عمال", CourtUnitId = labCourt?.Id, PhoneNumber = "01012345611", IsActive = true },
            new Employee { Code = "EMP-012", FullNameAr = "طارق إسماعيل رضوان", JobTitle = "سكرتير جلسة جنح بندر", CourtUnitId = bandarPartial?.Id, PhoneNumber = "01012345612", IsActive = true },
            new Employee { Code = "EMP-013", FullNameAr = "سعيد فتحي إمام", JobTitle = "سكرتير جلسة جنح", CourtUnitId = bandarPartial?.Id, PhoneNumber = "01012345613", IsActive = true },
            new Employee { Code = "EMP-014", FullNameAr = "بهاء الدين يوسف زكي", JobTitle = "سكرتير جلسة", CourtUnitId = null, PhoneNumber = "01012345614", IsActive = true },
            new Employee { Code = "EMP-015", FullNameAr = "مروان رفعت عبد الحميد", JobTitle = "سكرتير جلسة", CourtUnitId = null, PhoneNumber = "01012345615", IsActive = true },
        };

        context.Employees.AddRange(seedEmployees);
        await context.SaveChangesAsync();
        logger?.LogInformation("تم تهيئة جدول الموظفين وسكرتيري الجلسات بنجاح ({Count} موظف).", seedEmployees.Length);
    }

    private static AssemblyData ExtractAssemblyData(string excelPath, ILogger? logger)
    {
        var result = new AssemblyData();

        using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
                RelId = s.Attribute(rNs + "id")?.Value ?? ""
            }).ToList();

        var wbRelsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels")!;
        var wbRelsDoc = XDocument.Load(wbRelsEntry.Open());
        XNamespace pkgRelsNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        var relMap = wbRelsDoc.Root!.Elements(pkgRelsNs + "Relationship")
            .ToDictionary(r => r.Attribute("Id")?.Value ?? "", r => r.Attribute("Target")?.Value ?? "");

        string GetVal(XElement c)
        {
            var val = c.Element(ns + "v")?.Value;
            var type = c.Attribute("t")?.Value;
            if (type == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                return sharedStrings[idx];
            return val ?? "";
        }

        List<Dictionary<string, string>> ReadSheetRows(string sheetName)
        {
            var sheet = sheets.FirstOrDefault(s => s.Name == sheetName);
            if (sheet == null) return new();

            var target = relMap[sheet.RelId];
            if (target.StartsWith("/")) target = target[1..];
            else target = "xl/" + target;

            var wsDoc = XDocument.Load(zip.GetEntry(target)!.Open());
            var rows = wsDoc.Root!.Element(ns + "sheetData")!.Elements(ns + "row").ToList();

            var list = new List<Dictionary<string, string>>();
            foreach (var r in rows)
            {
                var d = r.Elements(ns + "c").ToDictionary(
                    c => new string(c.Attribute("r")?.Value?.Where(char.IsLetter).ToArray()),
                    c => GetVal(c)
                );
                list.Add(d);
            }
            return list;
        }

        // 1. الدوائر
        var circuitRows = ReadSheetRows("الدوائر");
        foreach (var r in circuitRows.Skip(3))
        {
            var code = r.GetValueOrDefault("A")?.Trim();
            var name = r.GetValueOrDefault("B")?.Trim();
            var scope = r.GetValueOrDefault("C")?.Trim();
            var cat = r.GetValueOrDefault("D")?.Trim();

            if (!string.IsNullOrEmpty(code) && code.StartsWith("C") && !string.IsNullOrEmpty(name))
            {
                result.Circuits.Add(new ParsedCircuit(code, name, scope ?? "", cat ?? ""));
            }
        }

        // 2. التشكيلات والقضاة
        var judgeRows = ReadSheetRows("التشكيلات");
        var uniqueJudgesSet = new HashSet<string>();
        foreach (var r in judgeRows.Skip(3))
        {
            var cCode = r.GetValueOrDefault("A")?.Trim();
            var orderStr = r.GetValueOrDefault("D")?.Trim();
            var judgeName = r.GetValueOrDefault("E")?.Trim();
            var roleText = r.GetValueOrDefault("F")?.Trim();

            if (!string.IsNullOrEmpty(cCode) && !string.IsNullOrEmpty(judgeName))
            {
                int.TryParse(orderStr, out int order);
                if (order == 0) order = 1;

                uniqueJudgesSet.Add(judgeName);
                result.JudgeAssignments.Add(new ParsedJudgeAssignment(cCode, judgeName, order, roleText ?? ""));
            }
        }
        result.UniqueJudges = uniqueJudgesSet.ToList();

        // 3. الجلسات (مع التصفية الصارمة لاستبعاد تجديد الحبس وغرفة المشورة والأمور الوقتية والمأذونين)
        var sessionRows = ReadSheetRows("الجلسات");
        bool IsExcluded(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return true;
            if (desc == "الجلسة واختصاصها وأرقامها") return true;
            if (desc.Contains("تجديد الحبس") || desc.Contains("تجديد حبس")) return true;
            if (desc.Contains("غرفة المشورة")) return true;
            if (desc.Contains("الأمور الوقتية") || desc.Contains("الامور الوقتية")) return true;
            if (desc.Contains("مأذونين") || desc.Contains("موثقين")) return true;
            return false;
        }

        foreach (var r in sessionRows.Skip(3))
        {
            var sCode = r.GetValueOrDefault("B")?.Trim();
            var cCode = r.GetValueOrDefault("C")?.Trim();
            var cScope = r.GetValueOrDefault("E")?.Trim();
            var day = r.GetValueOrDefault("F")?.Trim();
            var dayOrder = r.GetValueOrDefault("G")?.Trim();
            var desc = r.GetValueOrDefault("H")?.Trim();

            if (!string.IsNullOrEmpty(sCode) && !string.IsNullOrEmpty(cCode) && !string.IsNullOrEmpty(desc))
            {
                if (IsExcluded(desc)) continue;

                result.Sessions.Add(new ParsedSession(sCode, cCode, desc, cScope ?? "", day ?? "", dayOrder ?? "1"));
            }
        }

        logger?.LogInformation("تم استخراج {Circuits} دائرة، {Judges} قاضٍ، و{Sessions} جلسة فعلية صالحة من شيت الإكسيل.",
            result.Circuits.Count, result.UniqueJudges.Count, result.Sessions.Count);

        return result;
    }

    private static string? FindExcelFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "الجمعية_العمومية_2026-2027_v2.xlsx"),
            Path.Combine(Directory.GetCurrentDirectory(), "الجمعية_العمومية_2026-2027_v2.xlsx"),
            @"c:\Users\ahmed\source\repos\JudicialStatistics\JudicialStatistics.Web\الجمعية_العمومية_2026-2027_v2.xlsx",
            Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\JudicialStatistics.Web\الجمعية_العمومية_2026-2027_v2.xlsx")
        };

        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        return null;
    }

    public static CourtUnit ResolveCourt(string circuitCode, Dictionary<string, CourtUnit> courtMap, CourtUnit rootCourt)
    {
        if (circuitCode is "C001" or "C002" or "C003" or "C004" or "C009" or "C010" or "C011" or "C012" or "C016")
            return courtMap.GetValueOrDefault("BNISF-PLENARY") ?? rootCourt;
        if (circuitCode is "C005" or "C013")
            return courtMap.GetValueOrDefault("WASTA-PLENARY") ?? rootCourt;
        if (circuitCode is "C006" or "C007" or "C008" or "C014" or "C015")
            return courtMap.GetValueOrDefault("BEBA-PLENARY") ?? rootCourt;
        if (circuitCode is "C017" or "C018" or "C019" or "C020" or "C021" or "C022" or "C023" or "C024")
            return courtMap.GetValueOrDefault("FAM-COURT") ?? rootCourt;
        if (circuitCode is "C025" or "C026" or "C027" or "C028")
            return courtMap.GetValueOrDefault("BANDAR-PARTIAL") ?? rootCourt;
        if (circuitCode is "C029" or "C030" or "C031")
            return courtMap.GetValueOrDefault("MARKAZ-PARTIAL") ?? rootCourt;
        if (circuitCode is "C032" or "C033" or "C034")
            return courtMap.GetValueOrDefault("WASTA-PARTIAL") ?? rootCourt;
        if (circuitCode is "C035" or "C036")
            return courtMap.GetValueOrDefault("NASSER-PARTIAL") ?? rootCourt;
        if (circuitCode is "C037" or "C038")
            return courtMap.GetValueOrDefault("EHNASSIA-PARTIAL") ?? rootCourt;
        if (circuitCode is "C039" or "C040" or "C041")
            return courtMap.GetValueOrDefault("BEBA-PARTIAL") ?? rootCourt;
        if (circuitCode is "C042" or "C043" or "C044")
            return courtMap.GetValueOrDefault("FASHN-PARTIAL") ?? rootCourt;
        if (circuitCode is "C045" or "C046")
            return courtMap.GetValueOrDefault("SOMOSTA-PARTIAL") ?? rootCourt;
        if (circuitCode is "C047" or "C048" or "C049")
            return courtMap.GetValueOrDefault("LAB-COURT") ?? rootCourt;

        return rootCourt;
    }

    public static string ResolvePartialCircuitName(string code, string name, string? scope = null)
    {
        return code switch
        {
            "C025" => "الدائرة الأولى جزئي بندر بني سويف",
            "C026" => "الدائرة الثانية جزئي بندر بني سويف",
            "C027" => "الدائرة الثالثة جزئي بندر بني سويف",
            "C028" => "الدائرة الرابعة جزئي بندر بني سويف",
            "C029" => "الدائرة الأولى جزئي مركز بني سويف",
            "C030" => "الدائرة الثانية جزئي مركز بني سويف",
            "C031" => "الدائرة الثالثة جزئي مركز بني سويف",
            "C032" => "الدائرة الأولى جزئي مركز الواسطى",
            "C033" => "الدائرة الثانية جزئي مركز الواسطى",
            "C034" => "الدائرة الثالثة جزئي مركز الواسطى",
            "C035" => "الدائرة الأولى جزئي مركز ناصر",
            "C036" => "الدائرة الثانية جزئي مركز ناصر",
            "C037" => "الدائرة الأولى جزئي مركز اهناسيا",
            "C038" => "الدائرة الثانية جزئي مركز اهناسيا",
            "C039" => "الدائرة الأولى جزئي مركز ببا",
            "C040" => "الدائرة الثانية جزئي مركز ببا",
            "C041" => "الدائرة الثالثة جزئي مركز ببا",
            "C042" => "الدائرة الأولى جزئي مركز الفشن",
            "C043" => "الدائرة الثانية جزئي مركز الفشن",
            "C044" => "الدائرة الثالثة جزئي مركز الفشن",
            "C045" => "الدائرة الأولى جزئي مركز سمسطا",
            "C046" => "الدائرة الثانية جزئي مركز سمسطا",
            _ => !string.IsNullOrWhiteSpace(scope) ? $"{name} {scope}".Trim() : name
        };
    }

    public static async Task UpdateExistingSessionsAsync(JudicialDbContext context, ILogger? logger = null)
    {
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr == YearName || y.NameAr == "2026-2027");
        if (year == null) return;

        // 1. تحديث المحاكم والأسماء المعتمدة لدوائر العام القضائي
        var courts = await context.CourtUnits.ToListAsync();
        var courtMap = courts.ToDictionary(c => c.Code, c => c);
        var rootCourt = courtMap.GetValueOrDefault("BNISF-COURT") ?? courts.First();

        var circuits = await context.Circuits
            .Include(c => c.Versions)
            .Where(c => c.JudicialYearId == year.Id)
            .ToListAsync();

        foreach (var c in circuits)
        {
            var targetCourt = ResolveCourt(c.Code, courtMap, rootCourt);
            c.CourtUnitId = targetCourt.Id;

            var cKind = c.Versions.FirstOrDefault()?.CircuitKind;
            if (cKind == "Partial" || (int.TryParse(c.Code.Replace("C", ""), out int num) && num >= 25 && num <= 46))
            {
                c.NameAr = ResolvePartialCircuitName(c.Code, c.NameAr);
            }
        }

        // 2. تحديث الجلسات وأنواع القضايا وتوصيفاتها
        var signCaseType = await context.CaseTypes.FirstOrDefaultAsync(ct => ct.Code == "CIV-PART-SIGN")
                           ?? await context.CaseTypes.FirstOrDefaultAsync(ct => ct.NameAr == "صحة توقيع");

        var partUrgCaseType = await context.CaseTypes.FirstOrDefaultAsync(ct => ct.Code == "CIV-PART-URG")
                              ?? await context.CaseTypes.FirstOrDefaultAsync(ct => ct.NameAr == "مستعجل جزئي");

        var plenAppUrgCaseType = await context.CaseTypes.FirstOrDefaultAsync(ct => ct.Code == "CIV-FULL-APP-URG" || ct.Code == "CIV-FULL-APP-URGENT")
                                 ?? await context.CaseTypes.FirstOrDefaultAsync(ct => ct.NameAr == "مستأنف مستعجل");

        var plenAppMainCaseType = await context.CaseTypes.FirstOrDefaultAsync(ct => ct.Code == "CIV-FULL-APP-MAIN" || ct.Code == "CIV-FULL-APP")
                                  ?? await context.CaseTypes.FirstOrDefaultAsync(ct => ct.NameAr == "مدني مستأنف");

        var plenAppAwqCaseType = await context.CaseTypes.FirstOrDefaultAsync(ct => ct.Code == "CIV-FULL-APP-AWQ" || ct.Code == "CIV-FULL-APP-AWQAF")
                                 ?? await context.CaseTypes.FirstOrDefaultAsync(ct => ct.NameAr == "مستأنف أوقاف");

        var partGovCaseType = await context.CaseTypes.FirstOrDefaultAsync(ct => ct.Code == "CIV-PART-GOV")
                              ?? await context.CaseTypes.FirstOrDefaultAsync(ct => ct.NameAr == "حكومة جزئي");

        void SetSingleCaseType(SessionVersion svItem, CaseType? targetCt)
        {
            if (targetCt == null) return;
            var existing = svItem.SessionCaseTypes.FirstOrDefault(sct => sct.CaseTypeId == targetCt.Id);
            if (existing == null)
            {
                if (svItem.SessionCaseTypes.Count > 0)
                {
                    svItem.SessionCaseTypes.First().CaseTypeId = targetCt.Id;
                    var extra = svItem.SessionCaseTypes.Skip(1).ToList();
                    foreach (var item in extra)
                    {
                        context.SessionCaseTypes.Remove(item);
                        svItem.SessionCaseTypes.Remove(item);
                    }
                }
                else
                {
                    var newSct = new SessionCaseType
                    {
                        SessionVersionId = svItem.Id,
                        CaseTypeId = targetCt.Id
                    };
                    svItem.SessionCaseTypes.Add(newSct);
                    context.SessionCaseTypes.Add(newSct);
                }
            }
        }

        var sessions = await context.Sessions
            .Include(s => s.Circuit)
                .ThenInclude(c => c.Versions)
            .Include(s => s.Versions)
                .ThenInclude(v => v.Jurisdictions)
                    .ThenInclude(j => j.GeographicArea)
            .Include(s => s.Versions)
                .ThenInclude(v => v.SessionCaseTypes)
                    .ThenInclude(sct => sct.CaseType)
            .Where(s => s.Circuit.JudicialYearId == year.Id)
            .ToListAsync();

        foreach (var s in sessions)
        {
            var sv = s.Versions.FirstOrDefault();
            var circuitKind = s.Circuit.Versions.FirstOrDefault()?.CircuitKind ?? "";
            var circName = s.Circuit.NameAr;

            string? day = ExtractDay(sv?.ScheduleDescription ?? "") ?? 
                          ExtractDay(s.Description ?? "") ?? 
                          ExtractDay(s.NameAr);
            if (string.IsNullOrWhiteSpace(day)) day = "السبت";

            string abbr = GetCircuitAbbreviation(circName);
            string area = sv?.Jurisdictions.FirstOrDefault()?.GeographicArea?.NameAr 
                          ?? (circName.Contains("بندر") ? "بندر بني سويف" : "محافظة بني سويف");

            string rawText = s.Description ?? s.NameAr;
            string cleanName = rawText.Replace(" - إضافة مكتوبة: أول درجة", " أول درجة")
                                      .Replace("بدائرة المحافظة", "")
                                      .Trim();

            bool showInReport = DetermineShowInProgressReport(cleanName, circuitKind);
            string formattedDesc = FormatDescription(day, cleanName, area, abbr);
            string formattedName = FormatNameAr(cleanName, day);

            s.NameAr = formattedName;
            s.Description = formattedDesc;
            s.ShowInProgressReport = showInReport;
            if (sv != null)
            {
                sv.ScheduleDescription = formattedDesc;

                if (circuitKind == "Partial" || circuitKind == "Summary")
                {
                    if (rawText.Contains("صحة توقيع") || rawText.Contains("توقيع") || formattedName.Contains("صحة توقيع"))
                    {
                        SetSingleCaseType(sv, signCaseType);
                    }
                    else if (rawText.Contains("مستعجل") || formattedName.Contains("مستعجل"))
                    {
                        SetSingleCaseType(sv, partUrgCaseType);
                    }
                    else if (rawText.Contains("حكومة") || formattedName.Contains("حكومة"))
                    {
                        SetSingleCaseType(sv, partGovCaseType);
                    }
                }
                else if (circuitKind == "CivilFull")
                {
                    if (rawText.Contains("مستعجل") || formattedName.Contains("مستعجل"))
                    {
                        SetSingleCaseType(sv, plenAppUrgCaseType);
                    }
                    else if ((rawText.Contains("أوقاف") || formattedName.Contains("أوقاف")) && (rawText.Contains("مستأنف") || formattedName.Contains("مستأنف")))
                    {
                        SetSingleCaseType(sv, plenAppAwqCaseType);
                    }
                    else if (rawText.Contains("مستأنف") || formattedName.Contains("مستأنف"))
                    {
                        SetSingleCaseType(sv, plenAppMainCaseType);
                    }
                }
            }
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("تم تحديث {Count} جلسة بالتوصيف والمسمى المعتمد وحالة الظهور بالتقرير بنجاح.", sessions.Count);
    }

    public static string GetCircuitAbbreviation(string circuitName)
    {
        if (circuitName.Contains("الأولى") || circuitName.Contains("الأول")) return "د1";
        if (circuitName.Contains("الثانية") || circuitName.Contains("الثاني")) return "د2";
        if (circuitName.Contains("الثالثة") || circuitName.Contains("الثالث")) return "د3";
        if (circuitName.Contains("الرابعة") || circuitName.Contains("الرابع")) return "د4";
        if (circuitName.Contains("الخامسة") || circuitName.Contains("الخامس")) return "د5";
        if (circuitName.Contains("السادسة") || circuitName.Contains("السادس")) return "د6";
        if (circuitName.Contains("السابعة") || circuitName.Contains("السابع")) return "د7";
        if (circuitName.Contains("الثامنة") || circuitName.Contains("الثامن")) return "د8";
        if (circuitName.Contains("التاسعة") || circuitName.Contains("التاسع")) return "د9";
        if (circuitName.Contains("العاشرة") || circuitName.Contains("العاشر")) return "د10";
        return "د";
    }

    public static string? ExtractDay(string text)
    {
        if (text.Contains("السبت")) return "السبت";
        if (text.Contains("الأحد") || text.Contains("الاحد")) return "الأحد";
        if (text.Contains("الاثنين") || text.Contains("الأثنين") || text.Contains("الإثنين")) return "الاثنين";
        if (text.Contains("الثلاثاء")) return "الثلاثاء";
        if (text.Contains("الأربعاء") || text.Contains("الاربعاء")) return "الأربعاء";
        if (text.Contains("الخميس")) return "الخميس";
        if (text.Contains("الجمعة")) return "الجمعة";
        return null;
    }

    public static bool DetermineShowInProgressReport(string text, string circuitKind)
    {
        if (circuitKind == "CriminalAppeal")
        {
            bool isGeneralMisd = text.Contains("جنح ومخالفات مستأنفة");
            bool isBldg = text.Contains("البناء") || text.Contains("أملاك الدولة");
            return isGeneralMisd || isBldg;
        }

        if (circuitKind == "Partial" || circuitKind == "Summary")
        {
            bool isSpecializedCrim = text.Contains("طفل") || 
                                     text.Contains("بيئة") || text.Contains("البيئة") || 
                                     text.Contains("مرور") || text.Contains("المرور") || 
                                     text.Contains("إرهاب") || text.Contains("مخدر") || 
                                     text.Contains("المرأة") || text.Contains("أمن البلاد") || 
                                     text.Contains("هجرة");
            return !isSpecializedCrim;
        }

        return true;
    }

    public static string FormatDescription(string day, string rawName, string spatialArea, string circuitAbbr)
    {
        var text = rawName.Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*-\s*(السبت|الأحد|الاحد|الاثنين|الأثنين|الإثنين|الثلاثاء|الأربعاء|الاربعاء|الخميس|الجمعة)\s*$", "").Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^(السبت|الأحد|الاحد|الاثنين|الأثنين|الإثنين|الثلاثاء|الأربعاء|الاربعاء|الخميس|الجمعة)\s*-\s*", "").Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*-\s*د\d+\s*$", "").Trim();

        if (!string.IsNullOrWhiteSpace(spatialArea) && !text.Contains(spatialArea) && !text.Contains("بندر") && !text.Contains("مركز") && !text.Contains("محافظة"))
        {
            text = $"{text} - {spatialArea}";
        }

        var desc = $"{day} - {text} - {circuitAbbr}";
        desc = System.Text.RegularExpressions.Regex.Replace(desc, @"\s*-\s*-", " - ");
        desc = System.Text.RegularExpressions.Regex.Replace(desc, @"\s+", " ").Trim();
        return desc;
    }

    public static string FormatNameAr(string rawName, string day)
    {
        var text = rawName.Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*-\s*(السبت|الأحد|الاحد|الاثنين|الأثنين|الإثنين|الثلاثاء|الأربعاء|الاربعاء|الخميس|الجمعة)\s*$", "").Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^(السبت|الأحد|الاحد|الاثنين|الأثنين|الإثنين|الثلاثاء|الأربعاء|الاربعاء|الخميس|الجمعة)\s*-\s*", "").Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*-\s*د\d+\s*$", "").Trim();

        text = text.Replace(" - إضافة مكتوبة: أول درجة", " أول درجة")
                   .Replace("بدائرة المحافظة", "")
                   .Trim();

        if (text.Contains("الأرقام:"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"الأرقام:\s*([0-9،\s]+)");
            if (m.Success)
            {
                var nums = m.Groups[1].Value.Trim();
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*-\s*الأرقام:\s*[0-9،\s]+", $" ({nums})");
            }
        }
        else if (text.Contains("أرقام القضايا"))
        {
            text = text.Replace(" - أرقام القضايا ", " ").Replace("أرقام القضايا ", "");
        }

        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*-\s*-", " - ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        return $"{text} - {day}";
    }

    private class AssemblyData
    {
        public List<ParsedCircuit> Circuits { get; } = new();
        public List<string> UniqueJudges { get; set; } = new();
        public List<ParsedJudgeAssignment> JudgeAssignments { get; } = new();
        public List<ParsedSession> Sessions { get; } = new();
    }

    private record ParsedCircuit(string Code, string Name, string Scope, string Category);
    private record ParsedJudgeAssignment(string CircuitCode, string JudgeName, int Order, string RoleText);
    private record ParsedSession(string SessionCode, string CircuitCode, string SessionName, string CourtScope, string Day, string DayOrder);
}
