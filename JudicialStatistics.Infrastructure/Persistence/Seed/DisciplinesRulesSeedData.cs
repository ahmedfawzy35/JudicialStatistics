using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Domain.Years;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// بذار قواعد وبنود الإحصاء لتخصصات: الأسرة، العمال، والجنائي (جنح مستأنف وجزئي)
/// استناداً لملف إحصاء 2025/2026 والتشكيل الرسمي:
/// - الأسرة: أحكام الإثبات (ندب حكمين)، أنواع قضايا الأسرة وربط الجلسات.
/// - العمال: قضايا عمال وتأمينات وربط الجلسات.
/// - الجنائي: سياسة سنة القيد الواحدة، أنواع الوارد الجنائي، الأحكام الموضوعية والإجرائية، والمؤجلات.
/// آمن للتنفيذ المتكرر (Idempotent).
/// </summary>
public static class DisciplinesRulesSeedData
{
    public static async Task SeedAsync(JudicialDbContext context, ILogger? logger = null)
    {
        logger?.LogInformation("بدء تهيئة قواعد وتصنيفات (الأسرة، العمال، الجنائي)...");

        var ruleSet = await context.RuleSetVersions.FirstOrDefaultAsync(r => r.VersionNo == 1);
        if (ruleSet == null)
        {
            ruleSet = new RuleSetVersion
            {
                VersionNo = 1,
                NameAr = "قواعد التصنيف الأساسية المعتمدة",
                EffectiveFrom = new DateOnly(2025, 10, 1),
                Status = "Approved"
            };
            context.RuleSetVersions.Add(ruleSet);
            await context.SaveChangesAsync();
        }

        var judicialYear = await context.JudicialYears.FirstOrDefaultAsync(y => y.NameAr.Contains("2025"))
                           ?? await context.JudicialYears.FirstAsync();

        // 0. التخصصات القضائية الإحصائية المعتمدة
        await SeedStatisticalDisciplinesAsync(context, logger);

        // 1. بند إنتاج الأسرة المتميز: "ندب حكمين"
        await SeedFamilyProductionItemsAsync(context, logger);

        // 2. تصنيف قضايا الأسرة والعمال والجنائي
        var taxonomyMap = await SeedTaxonomiesAsync(context, logger);
        var caseTypeMap = await SeedCaseTypesAsync(context, taxonomyMap, logger);

        // 3. بنود وقواعد الجنائي (المقدم، الأحكام، المؤجلات)
        await SeedCriminalRulesAsync(context, ruleSet.Id, logger);

        // 4. سياسات شرائح سنوات القيد (متعدد للأسرة والعمال، وشريحة واحدة فقط للجنائي)
        await SeedCaseYearPoliciesAsync(context, judicialYear.Id, caseTypeMap, logger);

        // 5. ربط الجلسات بأنواع القضايا (SessionCaseTypes) لجميع التخصصات
        await SeedSessionCaseTypesAsync(context, caseTypeMap, logger);

        logger?.LogInformation("اكتملت تهيئة قواعد التخصصات القضائية بنجاح.");
    }

    private static async Task SeedStatisticalDisciplinesAsync(JudicialDbContext context, ILogger? logger)
    {
        var standardDisciplines = new[]
        {
            new JudicialStatistics.Domain.Organization.StatisticalDiscipline
            {
                Code = "Civil",
                NameAr = "مدني",
                Icon = "🏛️",
                CircuitKinds = "CivilFull,Partial,CivilPartial",
                EntryFormKind = "Civil",
                SortOrder = 1,
                IsActive = true
            },
            new JudicialStatistics.Domain.Organization.StatisticalDiscipline
            {
                Code = "Criminal",
                NameAr = "جنائي",
                Icon = "⚖️",
                CircuitKinds = "CriminalAppeal,Partial,CriminalPartial",
                EntryFormKind = "Criminal",
                SortOrder = 2,
                IsActive = true
            },
            new JudicialStatistics.Domain.Organization.StatisticalDiscipline
            {
                Code = "Family",
                NameAr = "أسرة",
                Icon = "👨‍👩‍👧",
                CircuitKinds = "Family",
                EntryFormKind = "Civil",
                SortOrder = 3,
                IsActive = true
            },
            new JudicialStatistics.Domain.Organization.StatisticalDiscipline
            {
                Code = "Labor",
                NameAr = "عمال",
                Icon = "👷",
                CircuitKinds = "Labor",
                EntryFormKind = "Civil",
                SortOrder = 4,
                IsActive = true
            }
        };

        foreach (var d in standardDisciplines)
        {
            if (!await context.StatisticalDisciplines.AnyAsync(x => x.Code == d.Code))
            {
                context.StatisticalDisciplines.Add(d);
            }
        }
        await context.SaveChangesAsync();
        logger?.LogInformation("تمت تهيئة التخصصات القضائية في جدول StatisticalDisciplines.");
    }

    private static async Task SeedFamilyProductionItemsAsync(JudicialDbContext context, ILogger? logger)
    {
        if (!await context.ProductionItemTypes.AnyAsync(p => p.Code == "ARBITRATORS_APPOINT"))
        {
            context.ProductionItemTypes.Add(new ProductionItemType
            {
                Code = "ARBITRATORS_APPOINT",
                NameAr = "ندب حكمين",
                ItemKind = "Judgment",
                EntryScreen = "Judgments",
                SortOrder = 20
            });
            await context.SaveChangesAsync();
            logger?.LogInformation("تمت إضافة بند الإنتاج للأسرة: ندب حكمين.");
        }
    }

    private static async Task<Dictionary<string, CaseTaxonomyNode>> SeedTaxonomiesAsync(JudicialDbContext context, ILogger? logger)
    {
        var nodes = new[]
        {
            new { Code = "FAMILY",   NameAr = "قضايا الأسرة",             Sort = (short)2 },
            new { Code = "LABOR",    NameAr = "قضايا العمال والتأمينات",  Sort = (short)3 },
            new { Code = "CRIMINAL", NameAr = "قضايا الجنح والجنائي",     Sort = (short)4 },
        };

        var existing = await context.CaseTaxonomyNodes.ToDictionaryAsync(n => n.Code);
        foreach (var n in nodes)
        {
            if (!existing.TryGetValue(n.Code, out var node))
            {
                node = new CaseTaxonomyNode
                {
                    Code = n.Code,
                    NameAr = n.NameAr,
                    SortOrder = n.Sort
                };
                context.CaseTaxonomyNodes.Add(node);
                existing[n.Code] = node;
            }
        }
        await context.SaveChangesAsync();
        return existing;
    }

    private static async Task<Dictionary<string, CaseType>> SeedCaseTypesAsync(
        JudicialDbContext context,
        Dictionary<string, CaseTaxonomyNode> taxMap,
        ILogger? logger)
    {
        var definitions = new[]
        {
            // الأسرة
            new { Code = "FAM-NAFS",    NameAr = "أسرة نفس",            TaxCode = "FAMILY",   Level = "FirstInstance", Proc = "Family",   Age = "Mixed" },
            new { Code = "FAM-MAL",     NameAr = "أسرة مال",            TaxCode = "FAMILY",   Level = "FirstInstance", Proc = "Family",   Age = "Mixed" },
            new { Code = "FAM-HABAS",   NameAr = "حبس أسرة",            TaxCode = "FAMILY",   Level = "FirstInstance", Proc = "Family",   Age = "Mixed" },

            // العمال
            new { Code = "LABOR-MAIN",  NameAr = "عمال كلي",            TaxCode = "LABOR",    Level = "FirstInstance", Proc = "Ordinary", Age = "Mixed" },
            new { Code = "LABOR-INSUR", NameAr = "تأمينات اجتماعية",     TaxCode = "LABOR",    Level = "FirstInstance", Proc = "Ordinary", Age = "Mixed" },
            new { Code = "LABOR-COMP",  NameAr = "تعويضات عمالية",       TaxCode = "LABOR",    Level = "FirstInstance", Proc = "Ordinary", Age = "Mixed" },

            // الجنائي (جنح مستأنف وجزئي)
            new { Code = "CRIM-APP-BANDAR", NameAr = "جنح مستأنف بندر",      TaxCode = "CRIMINAL", Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-APP-MARKAZ", NameAr = "جنح مستأنف مركز",      TaxCode = "CRIMINAL", Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-BLDG",       NameAr = "مباني",                 TaxCode = "CRIMINAL", Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-VIOLENCE",   NameAr = "قضايا العنف ضد المرأة", TaxCode = "CRIMINAL", Level = "Appeal",        Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-GEN",   NameAr = "جنح ومخالفات",          TaxCode = "CRIMINAL", Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
            new { Code = "CRIM-PART-STATE", NameAr = "مباني وأملاك الدولة",   TaxCode = "CRIMINAL", Level = "FirstInstance", Proc = "Summary",  Age = "Adult" },
        };

        var existing = await context.CaseTypes.ToDictionaryAsync(c => c.Code);
        foreach (var def in definitions)
        {
            if (!existing.TryGetValue(def.Code, out var ct))
            {
                var taxNode = taxMap[def.TaxCode];
                ct = new CaseType
                {
                    TaxonomyNodeId = taxNode.Id,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    StatisticalLevelCode = def.Level,
                    ProcedureCode = def.Proc,
                    AgeCategoryCode = def.Age
                };
                context.CaseTypes.Add(ct);
                existing[def.Code] = ct;
            }
        }
        await context.SaveChangesAsync();
        return existing;
    }

    private static async Task SeedCriminalRulesAsync(JudicialDbContext context, long ruleSetId, ILogger? logger)
    {
        // 1. بنود المقدم الجنائي
        var criminalIncomings = new[]
        {
            new { Code = "CRIM_PREV",      NameAr = "سابق (رصيد مدور)", Source = "Balance", Sort = (short)1 },
            new { Code = "CRIM_RETURN",    NameAr = "معاد من الاستئناف", Source = "Manual",  Sort = (short)2 },
            new { Code = "CRIM_ACCEL",     NameAr = "معجل من الوقف",    Source = "Manual",  Sort = (short)3 },
            new { Code = "CRIM_TRANSFER",  NameAr = "إحالات",           Source = "Manual",  Sort = (short)4 },
            new { Code = "CRIM_NEW",       NameAr = "قضايا جديدة",      Source = "Manual",  Sort = (short)5 },
            new { Code = "CRIM_OPP_NEW",   NameAr = "معارضات جديدة",    Source = "Manual",  Sort = (short)6 },
        };

        var existingInc = await context.IncomingTypes.ToDictionaryAsync(x => x.Code);
        foreach (var inc in criminalIncomings)
        {
            if (!existingInc.ContainsKey(inc.Code))
            {
                var entity = new IncomingType
                {
                    Code = inc.Code,
                    NameAr = inc.NameAr,
                    InputSource = inc.Source,
                    SortOrder = inc.Sort
                };
                context.IncomingTypes.Add(entity);
                existingInc[inc.Code] = entity;
            }
        }
        await context.SaveChangesAsync();

        // 2. بنود الإنتاج والأحكام الجنائية
        var criminalProduction = new[]
        {
            // موضوعي
            new { Code = "CRIM_IMPRIS",     NameAr = "حبس",                             Kind = "Judgment", Screen = "Judgments", Sort = (short)101 },
            new { Code = "CRIM_FINE",       NameAr = "غرامة",                           Kind = "Judgment", Screen = "Judgments", Sort = (short)102 },
            new { Code = "CRIM_ACQUIT",     NameAr = "براءة",                           Kind = "Judgment", Screen = "Judgments", Sort = (short)103 },
            new { Code = "CRIM_PREV_DEC",   NameAr = "سابقة فصل",                       Kind = "Judgment", Screen = "Judgments", Sort = (short)104 },
            new { Code = "CRIM_EXP_TIME",   NameAr = "انقضاء بمضي المدة",               Kind = "Judgment", Screen = "Judgments", Sort = (short)105 },

            // أوامر وتدابير
            new { Code = "CRIM_ORDER",      NameAr = "الأوامر الجنائية",                 Kind = "Judgment", Screen = "Judgments", Sort = (short)106 },
            new { Code = "CRIM_CARE",       NameAr = "التدابير والإيداع بمؤسسات الرعاية", Kind = "Judgment", Screen = "Judgments", Sort = (short)107 },

            // إجرائي
            new { Code = "CRIM_RECON",       NameAr = "انقضاء بالتصالح",                 Kind = "Judgment", Screen = "Judgments", Sort = (short)108 },
            new { Code = "CRIM_DEATH",       NameAr = "انقضاء بالوفاة",                  Kind = "Judgment", Screen = "Judgments", Sort = (short)109 },
            new { Code = "CRIM_OPP_VOID",    NameAr = "اعتبار المعارضة كأن لم تكن",     Kind = "Judgment", Screen = "Judgments", Sort = (short)110 },
            new { Code = "CRIM_OPP_INAD",    NameAr = "عدم جواز المعارضة",              Kind = "Judgment", Screen = "Judgments", Sort = (short)111 },
            new { Code = "CRIM_INAD",        NameAr = "عدم القبول",                      Kind = "Judgment", Screen = "Judgments", Sort = (short)112 },
            new { Code = "CRIM_APP_FORFEIT", NameAr = "سقوط الحق فى الاستئناف",         Kind = "Judgment", Screen = "Judgments", Sort = (short)113 },
            new { Code = "CRIM_ORDER_FINAL", NameAr = "اعتبار الامر الجنائي نهائي",      Kind = "Judgment", Screen = "Judgments", Sort = (short)114 },
            new { Code = "CRIM_NO_JURIS",    NameAr = "عدم اختصاص",                     Kind = "Judgment", Screen = "Judgments", Sort = (short)115 },

            // تمهيدي ووقف وإشكالات
            new { Code = "CRIM_EXP_NEW",     NameAr = "ندب خبير جنائي",                 Kind = "Judgment", Screen = "Judgments", Sort = (short)116 },
            new { Code = "CRIM_EXP_RECALL",  NameAr = "إعادة للخبير جنائي",              Kind = "Judgment", Screen = "Judgments", Sort = (short)117 },
            new { Code = "CRIM_SUSP_RECON",  NameAr = "وقف للتصالح",                    Kind = "Judgment", Screen = "Judgments", Sort = (short)118 },
            new { Code = "CRIM_SUSP",        NameAr = "وقف تعليقي جنائي",               Kind = "Judgment", Screen = "Judgments", Sort = (short)119 },
            new { Code = "CRIM_STAY",        NameAr = "وقف تنفيذ",                      Kind = "Judgment", Screen = "Judgments", Sort = (short)120 },
            new { Code = "CRIM_RESUME",      NameAr = "استمرار تنفيذ",                  Kind = "Judgment", Screen = "Judgments", Sort = (short)121 },
            new { Code = "CRIM_OTHER",       NameAr = "أخرى",                          Kind = "Judgment", Screen = "Judgments", Sort = (short)122 },
            new { Code = "CRIM_CARE_DISMISS",NameAr = "انقضاء (منازعات/تدابير)",         Kind = "Judgment", Screen = "Judgments", Sort = (short)123 },

            // الفئات الإجمالية لأحكام الجنائي (للحفظ والتوزيع على القضاة)
            new { Code = "CRIM_CAT_SUBST",  NameAr = "أحكام موضوعية",                  Kind = "Judgment", Screen = "Judgments", Sort = (short)124 },
            new { Code = "CRIM_CAT_PROC",   NameAr = "أحكام إجرائية",                  Kind = "Judgment", Screen = "Judgments", Sort = (short)125 },
            new { Code = "CRIM_CAT_ORDER",  NameAr = "أوامر جنائية",                   Kind = "Judgment", Screen = "Judgments", Sort = (short)126 },
            new { Code = "CRIM_CAT_PREP",   NameAr = "أحكام تمهيدية",                  Kind = "Judgment", Screen = "Judgments", Sort = (short)127 },
            new { Code = "CRIM_CAT_STAY",   NameAr = "أحكام الوقف",                    Kind = "Judgment", Screen = "Judgments", Sort = (short)128 },
            new { Code = "CRIM_CAT_EXEC",   NameAr = "إشكالات التنفيذ",                Kind = "Judgment", Screen = "Judgments", Sort = (short)129 },
            new { Code = "CRIM_CAT_OTHER",  NameAr = "أحكام أخرى",                     Kind = "Judgment", Screen = "Judgments", Sort = (short)130 },

            // مؤجلات جنائية
            new { Code = "CRIM_DEF_NOTICE",  NameAr = "مؤجل للاعلان",                   Kind = "Deferral", Screen = "Deferrals", Sort = (short)131 },
            new { Code = "CRIM_DEF_REP",     NameAr = "مؤجل لورود التقرير",             Kind = "Deferral", Screen = "Deferrals", Sort = (short)132 },
            new { Code = "CRIM_DEF_DOC",     NameAr = "مؤجل لضم المفردات",              Kind = "Deferral", Screen = "Deferrals", Sort = (short)133 },
            new { Code = "CRIM_DEF_OTHER",   NameAr = "المؤجل لأسباب أخرى",             Kind = "Deferral", Screen = "Deferrals", Sort = (short)134 },
            new { Code = "CRIM_DEF_RES",     NameAr = "محجوز للحكم خارج الشهر",         Kind = "Deferral", Screen = "Deferrals", Sort = (short)135 },
            new { Code = "CRIM_DEF_EXT",     NameAr = "مد أجل خارج الشهر",              Kind = "Deferral", Screen = "Deferrals", Sort = (short)136 },
            new { Code = "CRIM_DEF_REARG",   NameAr = "إعادة للمرافعة جنائي",           Kind = "Deferral", Screen = "Deferrals", Sort = (short)137 },
        };

        var existingProd = await context.ProductionItemTypes.ToDictionaryAsync(x => x.Code);
        foreach (var prod in criminalProduction)
        {
            if (!existingProd.ContainsKey(prod.Code))
            {
                var entity = new ProductionItemType
                {
                    Code = prod.Code,
                    NameAr = prod.NameAr,
                    ItemKind = prod.Kind,
                    EntryScreen = prod.Screen,
                    SortOrder = prod.Sort
                };
                context.ProductionItemTypes.Add(entity);
                existingProd[prod.Code] = entity;
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedCaseYearPoliciesAsync(
        JudicialDbContext context,
        long judicialYearId,
        Dictionary<string, CaseType> caseTypeMap,
        ILogger? logger)
    {
        // في الجنائي: سنة قيد واحدة فقط (AllYears / SingleYear)
        // في الأسرة والعمال: شرائح متعددة (2023 وما قبلها، 2024، 2025، 2026)
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
                    DecisionReference = $"سياسة شرائح القيد لنوع القضية {ct.NameAr}",
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
                    // شرائح متعددة للأسرة والعمال
                    policy.Bands.Add(new CaseYearBand { BandKind = "UpTo", LabelAr = "2023 وما قبلها", SortOrder = 1, ToYear = 2023 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2024", SortOrder = 2, FromYear = 2024, ToYear = 2024 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2025", SortOrder = 3, FromYear = 2025, ToYear = 2025 });
                    policy.Bands.Add(new CaseYearBand { BandKind = "Exact", LabelAr = "2026", SortOrder = 4, FromYear = 2026, ToYear = 2026 });
                }

                context.CaseYearPolicies.Add(policy);
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedSessionCaseTypesAsync(
        JudicialDbContext context,
        Dictionary<string, CaseType> caseTypeMap,
        ILogger? logger)
    {
        var sessionVersions = await context.SessionVersions
            .Include(sv => sv.Session)
            .Include(sv => sv.CircuitVersion)
            .ToListAsync();

        var existingLinks = await context.SessionCaseTypes
            .Select(s => new { s.SessionVersionId, s.CaseTypeId })
            .ToHashSetAsync();

        bool added = false;
        foreach (var sv in sessionVersions)
        {
            var kind = sv.CircuitVersion.CircuitKind;
            var sName = sv.Session.NameAr;

            var targetTypes = new List<CaseType>();

            if (kind == "Family")
            {
                if (sName.Contains("مال"))
                {
                    targetTypes.Add(caseTypeMap["FAM-MAL"]);
                }
                else
                {
                    targetTypes.Add(caseTypeMap["FAM-NAFS"]);
                    targetTypes.Add(caseTypeMap["FAM-HABAS"]);
                }
            }
            else if (kind == "Labor")
            {
                targetTypes.Add(caseTypeMap["LABOR-MAIN"]);
                targetTypes.Add(caseTypeMap["LABOR-INSUR"]);
                targetTypes.Add(caseTypeMap["LABOR-COMP"]);
            }
            else if (kind == "CriminalAppeal")
            {
                if (sName.Contains("مباني"))
                    targetTypes.Add(caseTypeMap["CRIM-BLDG"]);
                else if (sName.Contains("العنف") || sName.Contains("مرأة"))
                    targetTypes.Add(caseTypeMap["CRIM-VIOLENCE"]);
                else if (sName.Contains("مركز"))
                    targetTypes.Add(caseTypeMap["CRIM-APP-MARKAZ"]);
                else
                    targetTypes.Add(caseTypeMap["CRIM-APP-BANDAR"]);
            }
            else if (kind == "Partial")
            {
                if (sName.Contains("مباني") || sName.Contains("أملاك"))
                    targetTypes.Add(caseTypeMap["CRIM-PART-STATE"]);
                else if (sName.Contains("العنف") || sName.Contains("مرأة"))
                    targetTypes.Add(caseTypeMap["CRIM-VIOLENCE"]);
                else
                    targetTypes.Add(caseTypeMap["CRIM-PART-GEN"]);
            }
            else if (kind == "CivilFull")
            {
                // ربط جلسات المدني الكلي بأنواعها
                var civCode = sName switch
                {
                    var s when s.Contains("اوقاف") || s.Contains("أوقاف") => "CIV-FULL-AWQAF",
                    var s when s.Contains("تجاري") => "CIV-FULL-COMM",
                    var s when s.Contains("حكومة") => "CIV-FULL-GOV",
                    var s when s.Contains("ايجارات") || s.Contains("إيجارات") => "CIV-FULL-RENT",
                    var s when s.Contains("تعويضات") => "CIV-FULL-COMP",
                    _ => "CIV-FULL-MAIN"
                };

                var ct = await context.CaseTypes.FirstOrDefaultAsync(c => c.Code == civCode);
                if (ct != null) targetTypes.Add(ct);
            }

            foreach (var ct in targetTypes)
            {
                var key = new { SessionVersionId = sv.Id, CaseTypeId = ct.Id };
                if (!existingLinks.Contains(key))
                {
                    context.SessionCaseTypes.Add(new SessionCaseType
                    {
                        SessionVersionId = sv.Id,
                        CaseTypeId = ct.Id
                    });
                    existingLinks.Add(key);
                    added = true;
                }
            }
        }

        if (added)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم ربط جميع الجلسات بأنواع القضايا (SessionCaseTypes).");
        }
    }
}
