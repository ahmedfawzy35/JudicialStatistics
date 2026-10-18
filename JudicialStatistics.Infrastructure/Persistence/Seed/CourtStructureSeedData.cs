using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Courts;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Domain.Years;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// تحديث وتهيئة هيكل المحاكم، المأموريات، الدوائر، الجلسات، وسياسات سنوات القيد
/// طبقاً لفلسفة التنظيم القضائي لمحكمة بني سويف الابتدائية:
/// 1- المحكمة الابتدائية الأم (محكمة بني سويف الابتدائية)
/// 2- المأموريات الكلية الـ 3 (بني سويف الكلية، الواسطى الكلية، ببا الكلية)
/// 3- المحاكم الجزئية الـ 8 (بندر بني سويف، مركز بني سويف، الواسطى، ناصر، اهناسيا، ببا، سمسطا، الفشن)
/// 4- محكمة الأسرة
/// 5- المحكمة العمالية
/// </summary>
public static class CourtStructureSeedData
{
    public static async Task SeedAsync(JudicialDbContext context, ILogger? logger)
    {
        logger?.LogInformation("بدء تهيئة وتحديث هيكل المحاكم والمأموريات والدوائر...");

        // 1. المحكمة الابتدائية الأم
        var rootCourt = await context.CourtUnits.FirstOrDefaultAsync(c => c.Code == "BNISF-COURT")
                        ?? await context.CourtUnits.FirstOrDefaultAsync(c => c.ParentCourtUnitId == null);

        if (rootCourt == null)
        {
            rootCourt = new CourtUnit
            {
                Code = "BNISF-COURT",
                NameAr = "محكمة بني سويف الابتدائية",
                UnitKind = "Court",
                IsActive = true
            };
            context.CourtUnits.Add(rootCourt);
            await context.SaveChangesAsync();
        }

        // إضافة محكمة ابتدائية ثانية لاختبار وتفعيل نظام SaaS متعدد المحاكم الابتدائية
        var secondPrimaryCourt = await context.CourtUnits.FirstOrDefaultAsync(c => c.Code == "FYM-COURT");
        if (secondPrimaryCourt == null)
        {
            secondPrimaryCourt = new CourtUnit
            {
                Code = "FYM-COURT",
                NameAr = "محكمة الفيوم الابتدائية",
                UnitKind = "Court",
                IsActive = true
            };
            context.CourtUnits.Add(secondPrimaryCourt);
            await context.SaveChangesAsync();
        }

        // 2. المأموريات الكلية والمحاكم الجزئية والمحاكم المتخصصة
        var courtDefinitions = new[]
        {
            // المأموريات الكلية (Plenary Courts)
            new { Code = "BNISF-PLENARY", NameAr = "مأمورية بني سويف الكلية", Kind = "PlenaryHeadquarters" },
            new { Code = "WASTA-PLENARY", NameAr = "مأمورية الواسطى الكلية", Kind = "PlenaryHeadquarters" },
            new { Code = "BEBA-PLENARY",  NameAr = "مأمورية ببا الكلية",     Kind = "PlenaryHeadquarters" },

            // المحاكم الجزئية (Summary Courts)
            new { Code = "BANDAR-PARTIAL",   NameAr = "محكمة بندر بني سويف الجزئية", Kind = "SummaryCourt" },
            new { Code = "MARKAZ-PARTIAL",   NameAr = "محكمة مركز بني سويف الجزئية", Kind = "SummaryCourt" },
            new { Code = "WASTA-PARTIAL",    NameAr = "محكمة الواسطى الجزئية",       Kind = "SummaryCourt" },
            new { Code = "NASSER-PARTIAL",   NameAr = "محكمة ناصر الجزئية",          Kind = "SummaryCourt" },
            new { Code = "EHNASSIA-PARTIAL", NameAr = "محكمة اهناسيا الجزئية",       Kind = "SummaryCourt" },
            new { Code = "BEBA-PARTIAL",     NameAr = "محكمة ببا الجزئية",           Kind = "SummaryCourt" },
            new { Code = "FASHN-PARTIAL",    NameAr = "محكمة الفشن الجزئية",         Kind = "SummaryCourt" },
            new { Code = "SOMOSTA-PARTIAL",  NameAr = "محكمة سمسطا الجزئية",         Kind = "SummaryCourt" },

            // محكمة الأسرة والمحكمة العمالية
            new { Code = "FAM-COURT", NameAr = "محكمة الأسرة بني سويف",   Kind = "FamilyCourt" },
            new { Code = "LAB-COURT", NameAr = "المحكمة العمالية بني سويف", Kind = "LaborCourt" },
        };

        var courtMap = await context.CourtUnits.ToDictionaryAsync(c => c.Code);
        bool courtUnitsAdded = false;

        foreach (var def in courtDefinitions)
        {
            if (!courtMap.TryGetValue(def.Code, out var cUnit))
            {
                cUnit = new CourtUnit
                {
                    ParentCourtUnitId = rootCourt.Id,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    UnitKind = def.Kind,
                    IsActive = true
                };
                context.CourtUnits.Add(cUnit);
                courtMap[def.Code] = cUnit;
                courtUnitsAdded = true;
            }
            else
            {
                if (cUnit.ParentCourtUnitId != rootCourt.Id || cUnit.UnitKind != def.Kind)
                {
                    cUnit.ParentCourtUnitId = rootCourt.Id;
                    cUnit.UnitKind = def.Kind;
                    courtUnitsAdded = true;
                }
            }
        }

        if (courtUnitsAdded)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم حفظ وتحديث الوحدات القضائية والمأموريات بنجاح.");
        }

        // 3. إعادة ربط الدوائر بالمحاكم والمأموريات التابعة لها
        var circuits = await context.Circuits.ToListAsync();
        bool circuitsUpdated = false;

        foreach (var c in circuits)
        {
            long targetCourtId = rootCourt.Id;

            // أ- مأمورية بني سويف الكلية
            if (c.Code is "CV-01" or "CV-02" or "CV-03" or "CV-04" or "CV-08" or
                          "CRIM-01" or "CRIM-02" or "CRIM-03" or "CRIM-04")
            {
                targetCourtId = courtMap["BNISF-PLENARY"].Id;
            }
            // ب- مأمورية الواسطى الكلية
            else if (c.Code is "CV-05" or "CRIM-05")
            {
                targetCourtId = courtMap["WASTA-PLENARY"].Id;
            }
            // ج- مأمورية ببا الكلية
            else if (c.Code is "CV-06" or "CV-07" or "CRIM-06" or "CRIM-07" or "CRIM-08")
            {
                targetCourtId = courtMap["BEBA-PLENARY"].Id;
            }
            // د- المحاكم الجزئية
            else if (c.Code is "PART-01" or "PART-02" or "PART-03" or "PART-04")
            {
                targetCourtId = courtMap["BANDAR-PARTIAL"].Id;
            }
            else if (c.Code is "PART-05" or "PART-06" or "PART-07")
            {
                targetCourtId = courtMap["MARKAZ-PARTIAL"].Id;
            }
            else if (c.Code is "PART-08" or "PART-09" or "PART-10")
            {
                targetCourtId = courtMap["WASTA-PARTIAL"].Id;
            }
            else if (c.Code is "PART-11" or "PART-12")
            {
                targetCourtId = courtMap["NASSER-PARTIAL"].Id;
            }
            else if (c.Code is "PART-13" or "PART-14")
            {
                targetCourtId = courtMap["EHNASSIA-PARTIAL"].Id;
            }
            else if (c.Code is "PART-15" or "PART-16" or "PART-17")
            {
                targetCourtId = courtMap["BEBA-PARTIAL"].Id;
            }
            else if (c.Code is "PART-18" or "PART-19" or "PART-20")
            {
                targetCourtId = courtMap["FASHN-PARTIAL"].Id;
            }
            else if (c.Code is "PART-21" or "PART-22")
            {
                targetCourtId = courtMap["SOMOSTA-PARTIAL"].Id;
            }
            // هـ- محكمة الأسرة
            else if (c.Code.StartsWith("FAM-"))
            {
                targetCourtId = courtMap["FAM-COURT"].Id;
            }
            // و- المحكمة العمالية
            else if (c.Code.StartsWith("LAB-"))
            {
                targetCourtId = courtMap["LAB-COURT"].Id;
            }

            if (c.CourtUnitId != targetCourtId)
            {
                c.CourtUnitId = targetCourtId;
                circuitsUpdated = true;
            }
        }

        if (circuitsUpdated)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث ربط كافة الدوائر بالمأموريات والمحاكم التابعة لها بدقة.");
        }

        // 4. إنشاء وتأكيد وجود كافة أنواع القضايا (CaseTypes) والتصنيفات
        await SeedCompleteCaseTaxonomyAsync(context, logger);

        // 5. إضافة جلسات المدني الجزئي للدوائر الجزئية الـ 22
        await SeedPartialCivilSessionsAsync(context, logger);

        // 6. تهيئة سياسات سنوات القيد (مدني كلي: 5 سنوات، أسرة: 3 سنوات، عمال: 3 سنوات، جنائي: سنة واحدة)
        await SeedRefinedCaseYearBandsAsync(context, logger);
    }

    private static async Task SeedCompleteCaseTaxonomyAsync(JudicialDbContext context, ILogger? logger)
    {
        // 1. عقد شجرة التصنيف (Taxonomy Nodes)
        var rootCivil = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CIVIL");
        if (rootCivil == null)
        {
            rootCivil = new CaseTaxonomyNode { Code = "CIVIL", NameAr = "القضاء المدني", SortOrder = 1 };
            context.CaseTaxonomyNodes.Add(rootCivil);
            await context.SaveChangesAsync();
        }

        var civFullNode = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CIVIL_FULL");
        if (civFullNode == null)
        {
            civFullNode = new CaseTaxonomyNode { ParentId = rootCivil.Id, Code = "CIVIL_FULL", NameAr = "مدني كلي", SortOrder = 1 };
            context.CaseTaxonomyNodes.Add(civFullNode);
            await context.SaveChangesAsync();
        }

        var civPartNode = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CIVIL_PARTIAL");
        if (civPartNode == null)
        {
            civPartNode = new CaseTaxonomyNode { ParentId = rootCivil.Id, Code = "CIVIL_PARTIAL", NameAr = "مدني جزئي", SortOrder = 2 };
            context.CaseTaxonomyNodes.Add(civPartNode);
            await context.SaveChangesAsync();
        }

        var rootCrim = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CRIMINAL");
        if (rootCrim == null)
        {
            rootCrim = new CaseTaxonomyNode { Code = "CRIMINAL", NameAr = "القضاء الجنائي", SortOrder = 2 };
            context.CaseTaxonomyNodes.Add(rootCrim);
            await context.SaveChangesAsync();
        }

        var crimAppealNode = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CRIM_APPEAL");
        if (crimAppealNode == null)
        {
            crimAppealNode = new CaseTaxonomyNode { ParentId = rootCrim.Id, Code = "CRIM_APPEAL", NameAr = "جنح مستأنف", SortOrder = 1 };
            context.CaseTaxonomyNodes.Add(crimAppealNode);
            await context.SaveChangesAsync();
        }

        var crimPartNode = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CRIM_PARTIAL");
        if (crimPartNode == null)
        {
            crimPartNode = new CaseTaxonomyNode { ParentId = rootCrim.Id, Code = "CRIM_PARTIAL", NameAr = "جنح جزئي", SortOrder = 2 };
            context.CaseTaxonomyNodes.Add(crimPartNode);
            await context.SaveChangesAsync();
        }

        var crimJuvNode = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CRIM_JUVENILE");
        if (crimJuvNode == null)
        {
            crimJuvNode = new CaseTaxonomyNode { ParentId = rootCrim.Id, Code = "CRIM_JUVENILE", NameAr = "أحداث", SortOrder = 3 };
            context.CaseTaxonomyNodes.Add(crimJuvNode);
            await context.SaveChangesAsync();
        }

        var rootFam = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "FAMILY");
        if (rootFam == null)
        {
            rootFam = new CaseTaxonomyNode { Code = "FAMILY", NameAr = "قضاء الأسرة", SortOrder = 3 };
            context.CaseTaxonomyNodes.Add(rootFam);
            await context.SaveChangesAsync();
        }

        var rootLab = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "LABOR");
        if (rootLab == null)
        {
            rootLab = new CaseTaxonomyNode { Code = "LABOR", NameAr = "القضاء العمالي", SortOrder = 4 };
            context.CaseTaxonomyNodes.Add(rootLab);
            await context.SaveChangesAsync();
        }

        // 2. قائمة أنواع القضايا المعتمدة تفصيلياً طبقاً للمتطلبات
        var caseTypeDefinitions = new[]
        {
            // مدني كلي (3 قضاة)
            new { Code = "CIV-FULL-MAIN",     NameAr = "مدني كلي",            NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },
            new { Code = "CIV-FULL-COMP",     NameAr = "تعويضات",             NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },
            new { Code = "CIV-FULL-COMM",     NameAr = "تجاري",               NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },
            new { Code = "CIV-FULL-AWQAF",    NameAr = "أوقاف",               NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },
            new { Code = "CIV-FULL-GOV",      NameAr = "حكومة",               NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },
            new { Code = "CIV-FULL-APP-MAIN", NameAr = "مدني مستأنف",         NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },
            new { Code = "CIV-FULL-APP-URG",  NameAr = "مستأنف مستعجل",       NodeId = civFullNode.Id, Level = "Plenary", Proc = "Urgent",  Age = "Any" },
            new { Code = "CIV-FULL-APP-AWQ",  NameAr = "مستأنف أوقاف",        NodeId = civFullNode.Id, Level = "Plenary", Proc = "General", Age = "Any" },

            // مدني جزئي (قاض واحد)
            new { Code = "CIV-PART-MAIN",     NameAr = "مدني جزئي",           NodeId = civPartNode.Id, Level = "Partial", Proc = "General", Age = "Any" },
            new { Code = "CIV-PART-SIGN",     NameAr = "صحة توقيع",           NodeId = civPartNode.Id, Level = "Partial", Proc = "General", Age = "Any" },
            new { Code = "CIV-PART-GOV",      NameAr = "حكومة جزئي",          NodeId = civPartNode.Id, Level = "Partial", Proc = "General", Age = "Any" },
            new { Code = "CIV-PART-URG",      NameAr = "مستعجل جزئي",         NodeId = civPartNode.Id, Level = "Partial", Proc = "Urgent",  Age = "Any" },

            // جنح مستأنف (3 قضاة)
            new { Code = "CRIM-APP-MISD",     NameAr = "جنح ومخالفات مستأنفة",    NodeId = crimAppealNode.Id, Level = "Appeal", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-APP-WOMEN",    NameAr = "جنح العنف ضد المرأة مستأنفة", NodeId = crimAppealNode.Id, Level = "Appeal", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-APP-BLDG",     NameAr = "جنح المباني مستأنفة",     NodeId = crimAppealNode.Id, Level = "Appeal", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-APP-ENV",      NameAr = "جنح البيئة مستأنفة",      NodeId = crimAppealNode.Id, Level = "Appeal", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-APP-SEC",      NameAr = "جنح ماسة بأمن البلاد مستأنفة", NodeId = crimAppealNode.Id, Level = "Appeal", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-APP-FEES",     NameAr = "لجنة الإعفاء من الرسوم القضائية", NodeId = crimAppealNode.Id, Level = "Appeal", Proc = "General", Age = "Adult" },

            // جنح جزئي (قاض واحد)
            new { Code = "CRIM-PART-MISD",    NameAr = "جنح ومخالفات جزئي",       NodeId = crimPartNode.Id, Level = "Partial", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-PART-WOMEN",   NameAr = "جنح العنف ضد المرأة",    NodeId = crimPartNode.Id, Level = "Partial", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-PART-BLDG",    NameAr = "جنح المباني وأملاك الدولة", NodeId = crimPartNode.Id, Level = "Partial", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-PART-ENV",     NameAr = "جنح البيئة جزئي",         NodeId = crimPartNode.Id, Level = "Partial", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-PART-TRAFFIC", NameAr = "جنح المرور جزئي",         NodeId = crimPartNode.Id, Level = "Partial", Proc = "General", Age = "Adult" },
            new { Code = "CRIM-PART-SEC",     NameAr = "قضايا ماسة بأمن البلاد جزئي", NodeId = crimPartNode.Id, Level = "Partial", Proc = "General", Age = "Adult" },

            // أحداث (تنظرها دوائر الجنح المستأنف فقط)
            new { Code = "CRIM-JUV-MISD1",    NameAr = "جنح طفل أول درجة",        NodeId = crimJuvNode.Id, Level = "Juvenile", Proc = "General", Age = "Juvenile" },
            new { Code = "CRIM-JUV-FEL1",     NameAr = "جنايات طفل أول درجة",     NodeId = crimJuvNode.Id, Level = "Juvenile", Proc = "General", Age = "Juvenile" },
            new { Code = "CRIM-JUV-APP-MISD", NameAr = "جنح طفل مستأنف",         NodeId = crimJuvNode.Id, Level = "Juvenile", Proc = "General", Age = "Juvenile" },
            new { Code = "CRIM-JUV-APP-FEL",  NameAr = "جنايات طفل مستأنف",      NodeId = crimJuvNode.Id, Level = "Juvenile", Proc = "General", Age = "Juvenile" },

            // أسرة (3 قضاة)
            new { Code = "FAM-HABAS-NAFS",    NameAr = "أسرة نفس وحبس",          NodeId = rootFam.Id, Level = "Family", Proc = "General", Age = "Any" },
            new { Code = "FAM-MAL",           NameAr = "أسرة مال",               NodeId = rootFam.Id, Level = "Family", Proc = "General", Age = "Any" },

            // عمال (3 قضاة)
            new { Code = "LABOR-WORK",        NameAr = "قضايا عمال",             NodeId = rootLab.Id, Level = "Labor", Proc = "General", Age = "Any" },
            new { Code = "LABOR-INSUR",       NameAr = "تأمينات اجتماعية",        NodeId = rootLab.Id, Level = "Labor", Proc = "General", Age = "Any" },
            new { Code = "LABOR-CRIM",        NameAr = "جنح عمال",               NodeId = rootLab.Id, Level = "Labor", Proc = "General", Age = "Any" },
        };

        var existingCaseTypes = await context.CaseTypes.ToDictionaryAsync(c => c.Code);
        bool caseTypesAdded = false;

        foreach (var def in caseTypeDefinitions)
        {
            if (!existingCaseTypes.TryGetValue(def.Code, out var ct))
            {
                ct = new CaseType
                {
                    TaxonomyNodeId = def.NodeId,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    StatisticalLevelCode = def.Level,
                    ProcedureCode = def.Proc,
                    AgeCategoryCode = def.Age
                };
                context.CaseTypes.Add(ct);
                existingCaseTypes[def.Code] = ct;
                caseTypesAdded = true;
            }
        }

        if (caseTypesAdded)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم حفظ كامل تصنيف وأنواع القضايا المعتمدة بنجاح.");
        }
    }

    private static async Task SeedPartialCivilSessionsAsync(JudicialDbContext context, ILogger? logger)
    {
        // إضافة جلسات المدني الجزئي للدوائر الجزئية الـ 22 المستخرجة من شيت 27
        var partialCivilSessionsDef = new[]
        {
            // بندر بني سويف
            new { CirCode = "PART-01", SessCode = "PART-CIV-01A", Name = "د/1 بندر مدني جزئي (الأحد)", Day = "الأحد" },
            new { CirCode = "PART-01", SessCode = "PART-CIV-01B", Name = "د/1 بندر مدني جزئي (الأثنين)", Day = "الأثنين" },
            new { CirCode = "PART-02", SessCode = "PART-CIV-02",  Name = "د/2 بندر مدني جزئي (الأحد)", Day = "الأحد" },
            new { CirCode = "PART-03", SessCode = "PART-CIV-03A", Name = "د/3 بندر مدني جزئي (الأربعاء)", Day = "الأربعاء" },
            new { CirCode = "PART-03", SessCode = "PART-CIV-03B", Name = "د/3 بندر مدني حكومة (الخميس)", Day = "الخميس" },
            new { CirCode = "PART-04", SessCode = "PART-CIV-04A", Name = "د/4 بندر مدني جزئي (الثلاثاء)", Day = "الثلاثاء" },
            new { CirCode = "PART-04", SessCode = "PART-CIV-04B", Name = "د/4 بندر مدني مستعجل بندر ومركز (الخميس)", Day = "الخميس" },

            // مركز بني سويف
            new { CirCode = "PART-05", SessCode = "PART-CIV-05A", Name = "د/1 مركز مدني جزئي (الأحد)", Day = "الأحد" },
            new { CirCode = "PART-05", SessCode = "PART-CIV-05B", Name = "د/1 مركز مدني جزئي (الأثنين)", Day = "الأثنين" },
            new { CirCode = "PART-06", SessCode = "PART-CIV-06",  Name = "د/2 مركز مدني جزئي (الأربعاء)", Day = "الأربعاء" },
            new { CirCode = "PART-07", SessCode = "PART-CIV-07",  Name = "د/3 مركز مدني جزئي (الأربعاء)", Day = "الأربعاء" },

            // الواسطى
            new { CirCode = "PART-08", SessCode = "PART-CIV-08",  Name = "د/1 الواسطي مدني جزئي (الأحد)", Day = "الأحد" },
            new { CirCode = "PART-09", SessCode = "PART-CIV-09",  Name = "د/2 الواسطى مدني جزئي ومستعجل (الأربعاء)", Day = "الأربعاء" },
            new { CirCode = "PART-10", SessCode = "PART-CIV-10",  Name = "د/3 الواسطي مدني جزئي (الثلاثاء)", Day = "الثلاثاء" },

            // ناصر
            new { CirCode = "PART-11", SessCode = "PART-CIV-11",  Name = "د/1 ناصر مدني جزئي (الأحد)", Day = "الأحد" },
            new { CirCode = "PART-12", SessCode = "PART-CIV-12",  Name = "د/2 ناصر مدني جزئي ومستعجل (الثلاثاء)", Day = "الثلاثاء" },

            // اهناسيا
            new { CirCode = "PART-13", SessCode = "PART-CIV-13",  Name = "د/1 إهناسيا مدني جزئي (الأثنين)", Day = "الأثنين" },
            new { CirCode = "PART-14", SessCode = "PART-CIV-14",  Name = "د/2 اهناسيا مدني جزئي ومستعجل (الخميس)", Day = "الخميس" },

            // ببا
            new { CirCode = "PART-15", SessCode = "PART-CIV-15",  Name = "د/1 ببا مدني جزئي (الأحد)", Day = "الأحد" },
            new { CirCode = "PART-16", SessCode = "PART-CIV-16",  Name = "د/2 ببا مدني جزئي ومستعجل (الأربعاء)", Day = "الأربعاء" },
            new { CirCode = "PART-17", SessCode = "PART-CIV-17",  Name = "د/3 ببا مدني جزئي (الثلاثاء)", Day = "الثلاثاء" },

            // الفشن
            new { CirCode = "PART-18", SessCode = "PART-CIV-18",  Name = "د/1 الفشن مدني جزئي (الأثنين)", Day = "الأثنين" },
            new { CirCode = "PART-19", SessCode = "PART-CIV-19",  Name = "د/2 الفشن مدني جزئي (السبت)", Day = "السبت" },
            new { CirCode = "PART-20", SessCode = "PART-CIV-20",  Name = "د/3 الفشن مدني جزئي ومستعجل (الأربعاء)", Day = "الأربعاء" },

            // سمسطا
            new { CirCode = "PART-21", SessCode = "PART-CIV-21",  Name = "د/1 سمسطا مدني جزئي ومستعجل (الأثنين)", Day = "الأثنين" },
            new { CirCode = "PART-22", SessCode = "PART-CIV-22",  Name = "د/2 سمسطا مدني جزئي (الخميس)", Day = "الخميس" },
        };

        var judicialYear = await context.JudicialYears.FirstOrDefaultAsync(y => y.Status == "Active")
                           ?? await context.JudicialYears.FirstAsync();

        var circuits = await context.Circuits
            .Include(c => c.Versions)
            .ToDictionaryAsync(c => c.Code);

        var existingSessions = await context.Sessions.ToDictionaryAsync(s => s.Code);
        var civPartType = await context.CaseTypes.FirstOrDefaultAsync(c => c.Code == "CIV-PART-MAIN")
                          ?? await context.CaseTypes.FirstAsync();
        var civUrgType = await context.CaseTypes.FirstOrDefaultAsync(c => c.Code == "CIV-PART-URG") ?? civPartType;
        var civGovType = await context.CaseTypes.FirstOrDefaultAsync(c => c.Code == "CIV-PART-GOV") ?? civPartType;

        bool addedSessions = false;

        foreach (var def in partialCivilSessionsDef)
        {
            if (!circuits.TryGetValue(def.CirCode, out var circuit)) continue;

            var circuitVersion = circuit.Versions.FirstOrDefault();
            if (circuitVersion == null) continue;

            if (!existingSessions.TryGetValue(def.SessCode, out var session))
            {
                session = new Session
                {
                    CircuitId = circuit.Id,
                    Code = def.SessCode,
                    NameAr = def.Name,
                    IsClosed = false
                };

                var sessionVersion = new SessionVersion
                {
                    Session = session,
                    CircuitVersionId = circuitVersion.Id,
                    EffectiveFrom = judicialYear.StartDate,
                    EffectiveTo = judicialYear.EndDate,
                    ScheduleDescription = $"{def.Day} - {def.Name}"
                };

                session.Versions.Add(sessionVersion);
                context.Sessions.Add(session);
                existingSessions[def.SessCode] = session;

                // ربط نوع القضية المدني
                var targetType = def.Name.Contains("حكومة") ? civGovType : (def.Name.Contains("مستعجل") ? civUrgType : civPartType);
                if (targetType != null)
                {
                    context.SessionCaseTypes.Add(new SessionCaseType
                    {
                        SessionVersion = sessionVersion,
                        CaseTypeId = targetType.Id
                    });
                }
                addedSessions = true;
            }
        }

        if (addedSessions)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم إضافة وتحديث جلسات المدني الجزئي للدوائر الجزئية بنجاح.");
        }
    }

    private static async Task SeedRefinedCaseYearBandsAsync(JudicialDbContext context, ILogger? logger)
    {
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.Status == "Active")
                   ?? await context.JudicialYears.FirstAsync();

        var caseTypes = await context.CaseTypes.ToListAsync();

        // 1. سياسات المدني كلي (5 سنوات قيد: 2022 وما قبلها، 2023، 2024، 2025، 2026)
        var civilFullDesired = new List<(string Kind, string LabelAr, short SortOrder, short? From, short? To)>
        {
            ("UpTo", "2022 وما قبلها", 1, null, 2022),
            ("Exact", "2023", 2, 2023, 2023),
            ("Exact", "2024", 3, 2024, 2024),
            ("Exact", "2025", 4, 2025, 2025),
            ("Exact", "2026", 5, 2026, 2026)
        };

        var civilFullTypes = caseTypes.Where(c => c.Code.StartsWith("CIV-FULL")).ToList();
        foreach (var ct in civilFullTypes)
        {
            var policy = await context.CaseYearPolicies
                .Include(p => p.Bands)
                .FirstOrDefaultAsync(p => p.JudicialYearId == year.Id && p.CaseTypeId == ct.Id);

            if (policy == null)
            {
                policy = new CaseYearPolicy
                {
                    JudicialYearId = year.Id,
                    CaseTypeId = ct.Id,
                    VersionNo = 1,
                    DecisionReference = "قرار سياسة تصنيف شرائح سنوات القيد للعام القضائي 2025/2026",
                    Status = "Approved"
                };
                context.CaseYearPolicies.Add(policy);
            }

            await SyncBandsAsync(context, policy, civilFullDesired);
        }

        // 2. سياسات الأسرة والعمال (3 سنوات قيد: 2024 وما قبلها، 2025، 2026)
        var famAndLaborDesired = new List<(string Kind, string LabelAr, short SortOrder, short? From, short? To)>
        {
            ("UpTo", "2024 وما قبلها", 1, null, 2024),
            ("Exact", "2025", 2, 2025, 2025),
            ("Exact", "2026", 3, 2026, 2026)
        };

        var famAndLaborTypes = caseTypes.Where(c => c.Code.StartsWith("FAM-") || c.Code.StartsWith("LABOR-")).ToList();
        foreach (var ct in famAndLaborTypes)
        {
            var policy = await context.CaseYearPolicies
                .Include(p => p.Bands)
                .FirstOrDefaultAsync(p => p.JudicialYearId == year.Id && p.CaseTypeId == ct.Id);

            if (policy == null)
            {
                policy = new CaseYearPolicy
                {
                    JudicialYearId = year.Id,
                    CaseTypeId = ct.Id,
                    VersionNo = 1,
                    DecisionReference = "قرار سياسة تصنيف شرائح سنوات القيد للعام القضائي 2025/2026",
                    Status = "Approved"
                };
                context.CaseYearPolicies.Add(policy);
            }

            await SyncBandsAsync(context, policy, famAndLaborDesired);
        }

        // 3. سياسات الجنائي (سنة واحدة موحدة: "السنة الإحصائية")
        var crimDesired = new List<(string Kind, string LabelAr, short SortOrder, short? From, short? To)>
        {
            ("AllYears", "السنة الإحصائية", 1, null, null)
        };

        var crimTypes = caseTypes.Where(c => c.Code.StartsWith("CRIM-")).ToList();
        foreach (var ct in crimTypes)
        {
            var policy = await context.CaseYearPolicies
                .Include(p => p.Bands)
                .FirstOrDefaultAsync(p => p.JudicialYearId == year.Id && p.CaseTypeId == ct.Id);

            if (policy == null)
            {
                policy = new CaseYearPolicy
                {
                    JudicialYearId = year.Id,
                    CaseTypeId = ct.Id,
                    VersionNo = 1,
                    DecisionReference = "قرار سياسة تصنيف شرائح سنوات القيد للعام القضائي 2025/2026",
                    Status = "Approved"
                };
                context.CaseYearPolicies.Add(policy);
            }

            await SyncBandsAsync(context, policy, crimDesired);
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("تم ضبط وتحديث شرائح سنوات القيد للعام القضائي (مدني كلي: 5، أسرة وعمال: 3، جنائي: سنة واحدة).");
    }

    private static async Task SyncBandsAsync(
        JudicialDbContext context,
        CaseYearPolicy policy,
        List<(string Kind, string LabelAr, short SortOrder, short? From, short? To)> desiredBands)
    {
        var existingBands = policy.Bands.OrderBy(b => b.SortOrder).ToList();

        // تحديث الشرائح القائمة في مكانها لتجنب خرق قيود المفاتيح الأجنبية مع الجداول الإحصائية
        int i = 0;
        for (; i < Math.Min(existingBands.Count, desiredBands.Count); i++)
        {
            var existing = existingBands[i];
            var desired = desiredBands[i];
            existing.BandKind = desired.Kind;
            existing.LabelAr = desired.LabelAr;
            existing.SortOrder = desired.SortOrder;
            existing.FromYear = desired.From;
            existing.ToYear = desired.To;
        }

        // إضافة أي شرائح جديدة مطلوبة
        for (; i < desiredBands.Count; i++)
        {
            var desired = desiredBands[i];
            policy.Bands.Add(new CaseYearBand
            {
                BandKind = desired.Kind,
                LabelAr = desired.LabelAr,
                SortOrder = desired.SortOrder,
                FromYear = desired.From,
                ToYear = desired.To
            });
        }

        // إزالة الشرائح الزائدة فقط إذا لم تكن مرتبطة بصفوف إحصائية
        for (; i < existingBands.Count; i++)
        {
            var surplus = existingBands[i];
            bool isReferenced = await context.IncomingRows.AnyAsync(r => r.CaseYearBandId == surplus.Id)
                || await context.OpeningBalances.AnyAsync(r => r.CaseYearBandId == surplus.Id)
                || await context.ProductionRows.AnyAsync(r => r.CaseYearBandId == surplus.Id)
                || await context.ClosingBalances.AnyAsync(r => r.CaseYearBandId == surplus.Id);

            if (!isReferenced)
            {
                policy.Bands.Remove(surplus);
                context.CaseYearBands.Remove(surplus);
            }
        }
    }
}
