using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// بذار قواعد وبيانات تصنيف القضايا المدنية الكلية استناداً لنموذج إحصاء 2025/2026:
/// - أنواع المقدمات (الرصيد الوارد)
/// - أنواع وأسباب المؤجلات
/// - شجرة تصنيف الأحكام الهرمية
/// - أنواع الأحكام الفردية
/// - بنود الإنتاج
/// - شجرة تصنيف الدعاوى وأنواع القضايا
/// - قواعد ربط الأحكام والتأجيلات بأنواع القضايا
/// آمن للتنفيذ المتكرر (Idempotent).
/// </summary>
public static class CivilFullRulesSeedData
{
    public static async Task SeedAsync(JudicialDbContext context, ILogger? logger = null)
    {
        logger?.LogInformation("بدء تهيئة قواعد وبنود الإحصاء القضائي (مدني كلي)...");

        // 1. التأكد من وجود نسخة القواعد المعتمدة
        var ruleSet = await EnsureRuleSetVersionAsync(context, logger);

        // 2. أنواع المقدمات (الوارد)
        var incomingMap = await SeedIncomingTypesAsync(context, logger);

        // 3. أسباب المؤجلات
        var deferralMap = await SeedDeferralReasonsAsync(context, logger);

        // 4. إجراءات القاضي المستقلة
        await SeedJudgeActionTypesAsync(context, deferralMap, logger);

        // 5. بنود الإنتاج
        await SeedProductionItemTypesAsync(context, deferralMap, logger);

        // 6. شجرة تصنيف الأحكام
        var treeNodeMap = await SeedJudgmentTreeAsync(context, ruleSet.Id, logger);

        // 7. أنواع الأحكام الفردية
        var judgmentTypeMap = await SeedJudgmentTypesAsync(context, logger);

        // 8. شجرة تصنيف الدعاوى وأنواع قضايا مدني كلي
        var caseTypeMap = await SeedCaseTaxonomyAndTypesAsync(context, logger);

        // 9. قواعد ربط أنواع الأحكام والتأجيلات بالقضايا
        await SeedCaseRulesAsync(context, ruleSet.Id, caseTypeMap, judgmentTypeMap, treeNodeMap, deferralMap, incomingMap, logger);

        // 10. سياسة شرائح سنوات القيد للعام القضائي
        await SeedCaseYearPoliciesAsync(context, caseTypeMap, logger);

        logger?.LogInformation("اكتملت تهيئة قواعد وبنود الإحصاء القضائي بنجاح.");
    }

    private static async Task<RuleSetVersion> EnsureRuleSetVersionAsync(JudicialDbContext context, ILogger? logger)
    {
        var ruleSet = await context.RuleSetVersions.FirstOrDefaultAsync(r => r.VersionNo == 1);
        if (ruleSet is null)
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
            logger?.LogInformation("تم إنشاء نسخة القواعد الأساسية رقم 1.");
        }
        return ruleSet;
    }

    private static async Task<Dictionary<string, IncomingType>> SeedIncomingTypesAsync(JudicialDbContext context, ILogger? logger)
    {
        var definitions = new[]
        {
            new { Code = "PREV",            NameAr = "سابق (رصيد مدور)",         Source = "Balance",    Sort = (short)1 },
            new { Code = "RENEW",           NameAr = "مجدد من الشطب",            Source = "Manual",     Sort = (short)2 },
            new { Code = "RECUSAL_WAIT",    NameAr = "لحين الفصل في طلب الرد",   Source = "Manual",     Sort = (short)3 },
            new { Code = "ACCEL_PENAL",     NameAr = "معجل من الوقف جزائي",      Source = "Manual",     Sort = (short)4 },
            new { Code = "ACCEL_SUSP",      NameAr = "معجل من الوقف تعليقي",     Source = "Manual",     Sort = (short)5 },
            new { Code = "ACCEL_AGREE",     NameAr = "معجل من الوقف اتفاقي",     Source = "Manual",     Sort = (short)6 },
            new { Code = "ACCEL_INTERRUPT", NameAr = "معجل من الانقطاع",         Source = "Manual",     Sort = (short)7 },
            new { Code = "RETURN_APPEAL",   NameAr = "معاد من الاستئناف",        Source = "Manual",     Sort = (short)8 },
            new { Code = "SHORT",           NameAr = "التقصير الوارد",           Source = "Shortening", Sort = (short)9 },
            new { Code = "TRANSFER_FROM",   NameAr = "إحالة من الدائرة",          Source = "Manual",     Sort = (short)10 },
            new { Code = "TRANSFER_TO",     NameAr = "إحالة إلى الدائرة",         Source = "Manual",     Sort = (short)11 },
            new { Code = "NEW",             NameAr = "الجديد",                   Source = "Manual",     Sort = (short)12 },
        };

        var existing = await context.IncomingTypes.ToDictionaryAsync(x => x.Code);
        bool hasChanges = false;

        // تنظيف الكود القديم ACCEL إذا وُجد ولم يكن مستخدماً في جداول فرعية
        if (existing.TryGetValue("ACCEL", out var oldAccel) && !await context.CaseTypeInputRules.AnyAsync(r => r.IncomingTypeId == oldAccel.Id))
        {
            context.IncomingTypes.Remove(oldAccel);
            existing.Remove("ACCEL");
            hasChanges = true;
        }

        foreach (var def in definitions)
        {
            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || ent.InputSource != def.Source || ent.SortOrder != def.Sort)
                {
                    ent.NameAr = def.NameAr;
                    ent.InputSource = def.Source;
                    ent.SortOrder = def.Sort;
                    hasChanges = true;
                }
            }
            else
            {
                var newEnt = new IncomingType
                {
                    Code = def.Code,
                    NameAr = def.NameAr,
                    InputSource = def.Source,
                    SortOrder = def.Sort
                };
                context.IncomingTypes.Add(newEnt);
                existing[def.Code] = newEnt;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث بنود المقدم (الوارد).");
        }

        return await context.IncomingTypes.ToDictionaryAsync(x => x.Code);
    }

    private static async Task<Dictionary<string, DeferralReason>> SeedDeferralReasonsAsync(JudicialDbContext context, ILogger? logger)
    {
        var definitions = new[]
        {
            new { Code = "RESERVED",           NameAr = "محجوز للحكم",                 Source = "Hearing", Sort = (short)1 },
            new { Code = "EXTEND_JUDGMENT",    NameAr = "مد أجل النطق بالحكم",         Source = "Judge",   Sort = (short)2 },
            new { Code = "REOPEN_ARGUMENT",    NameAr = "إعادة للمرافعة",               Source = "Judge",   Sort = (short)3 },
            new { Code = "WAIT_REPORT",        NameAr = "المؤجل لورود التقرير",         Source = "Hearing", Sort = (short)4 },
            new { Code = "OTHER_REASONS",      NameAr = "المؤجل لأسباب أخرى",            Source = "Hearing", Sort = (short)5 },
            new { Code = "EXPERT_RETURN_FIRST",NameAr = "معاد من الخبراء (أول جلسة - بيان مستقل)", Source = "Standalone", Sort = (short)6 },
            new { Code = "SUB_JUDGMENT_EVID",  NameAr = "أحكام فرعية وإثبات",          Source = "Hearing", Sort = (short)7 },
            new { Code = "REMAINING_DEF",      NameAr = "باقي المؤجلات",                Source = "Hearing", Sort = (short)8 },
        };

        var existing = await context.DeferralReasons.ToDictionaryAsync(x => x.Code);
        bool hasChanges = false;

        // إزالة أكواد مؤقتة قديمة غير مستخدمة
        var oldCodes = new[] { "RESERVE", "EXTEND", "REPLEAD", "REPORT", "NOTIFY", "DOCS" };
        foreach (var c in oldCodes)
        {
            if (existing.TryGetValue(c, out var oldEnt) &&
                !await context.CaseTypeDeferralRules.AnyAsync(r => r.DeferralReasonId == oldEnt.Id) &&
                !await context.ProductionItemTypes.AnyAsync(p => p.DeferralReasonId == oldEnt.Id))
            {
                context.DeferralReasons.Remove(oldEnt);
                existing.Remove(c);
                hasChanges = true;
            }
        }

        foreach (var def in definitions)
        {
            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || ent.SourceKind != def.Source || ent.SortOrder != def.Sort)
                {
                    ent.NameAr = def.NameAr;
                    ent.SourceKind = def.Source;
                    ent.SortOrder = def.Sort;
                    hasChanges = true;
                }
            }
            else
            {
                var newEnt = new DeferralReason
                {
                    Code = def.Code,
                    NameAr = def.NameAr,
                    SourceKind = def.Source,
                    SortOrder = def.Sort
                };
                context.DeferralReasons.Add(newEnt);
                existing[def.Code] = newEnt;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث أسباب المؤجلات.");
        }

        return await context.DeferralReasons.ToDictionaryAsync(x => x.Code);
    }

    private static async Task SeedJudgeActionTypesAsync(JudicialDbContext context, Dictionary<string, DeferralReason> deferrals, ILogger? logger)
    {
        var definitions = new[]
        {
            new { Code = "JA_EXTEND", NameAr = "مد أجل النطق بالحكم", DefCode = "EXTEND_JUDGMENT", Sort = (short)1 },
            new { Code = "JA_REOPEN", NameAr = "إعادة للمرافعة",       DefCode = "REOPEN_ARGUMENT", Sort = (short)2 }
        };

        var existing = await context.JudgeActionTypes.ToDictionaryAsync(x => x.Code);
        bool hasChanges = false;

        foreach (var def in definitions)
        {
            if (!deferrals.TryGetValue(def.DefCode, out var defReason)) continue;

            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || ent.DeferralReasonId != defReason.Id || ent.SortOrder != def.Sort)
                {
                    ent.NameAr = def.NameAr;
                    ent.DeferralReasonId = defReason.Id;
                    ent.SortOrder = def.Sort;
                    hasChanges = true;
                }
            }
            else
            {
                var newEnt = new JudgeActionType
                {
                    Code = def.Code,
                    NameAr = def.NameAr,
                    DeferralReasonId = defReason.Id,
                    SortOrder = def.Sort
                };
                context.JudgeActionTypes.Add(newEnt);
                existing[def.Code] = newEnt;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث إجراءات القاضي.");
        }
    }

    private static async Task SeedProductionItemTypesAsync(JudicialDbContext context, Dictionary<string, DeferralReason> deferrals, ILogger? logger)
    {
        var definitions = new ProductionItemDef[]
        {
            new("PROD_FINAL_JUDGMENT", "أحكام منهية للخصومة (قطعية)", "Judgment", "Judgments", null, 1),
            new("PROD_INTERLOCUTORY", "أحكام فرعية", "Judgment", "Judgments", "SUB_JUDGMENT_EVID", 2),
            new("PROD_EVIDENCE", "أحكام إثبات (خبير / تحقيق)", "Judgment", "Judgments", "SUB_JUDGMENT_EVID", 3),
            new("PROD_STAY", "أحكام الوقف", "Judgment", "Judgments", "OTHER_REASONS", 4),
            new("PROD_INTERRUPT", "انقطاع سير الخصومة", "Judgment", "Judgments", "OTHER_REASONS", 5),
            new("PROD_STRIKE", "شطب الدعوى", "Action", "Actions", null, 6),
            new("PROD_RECUSAL", "طلب الرد", "Action", "Actions", null, 7),
            new("PROD_EXTEND", "مد أجل النطق بالحكم", "Action", "Actions", "EXTEND_JUDGMENT", 8),
            new("PROD_REOPEN", "إعادة للمرافعة", "Action", "Actions", "REOPEN_ARGUMENT", 9),
            new("PROD_DEF_RESERVED", "محجوز للحكم", "Deferral", "Deferrals", "RESERVED", 10),
            new("PROD_DEF_REPORT", "مؤجل لورود التقرير", "Deferral", "Deferrals", "WAIT_REPORT", 11),
            new("PROD_DEF_EXPERT_RET", "معاد من الخبراء (أول جلسة - بيان مستقل)", "Action", "Actions", null, 12),
            new("PROD_DEF_OTHER", "مؤجل لأسباب أخرى", "Deferral", "Deferrals", "OTHER_REASONS", 13),
        };

        var existing = await context.ProductionItemTypes.ToDictionaryAsync(x => x.Code);
        bool hasChanges = false;

        // إزالة أكواد مؤقتة سابقة
        var oldCodes = new[] { "JUDGMENT_FINAL", "JUDGMENT_PREP", "DEF_RESERVED", "DEF_EXTEND", "DEF_REOPEN", "DEF_OTHER" };
        foreach (var c in oldCodes)
        {
            if (existing.TryGetValue(c, out var oldEnt) && !await context.CaseTypeInputRules.AnyAsync(r => r.ProductionItemTypeId == oldEnt.Id))
            {
                context.ProductionItemTypes.Remove(oldEnt);
                existing.Remove(c);
                hasChanges = true;
            }
        }

        foreach (var def in definitions)
        {
            long? defId = def.DefCode != null && deferrals.TryGetValue(def.DefCode, out var d) ? d.Id : null;

            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || ent.ItemKind != def.Kind || ent.EntryScreen != def.Screen || ent.DeferralReasonId != defId || ent.SortOrder != def.Sort)
                {
                    ent.NameAr = def.NameAr;
                    ent.ItemKind = def.Kind;
                    ent.EntryScreen = def.Screen;
                    ent.DeferralReasonId = defId;
                    ent.SortOrder = def.Sort;
                    hasChanges = true;
                }
            }
            else
            {
                var newEnt = new ProductionItemType
                {
                    Code = def.Code,
                    NameAr = def.NameAr,
                    ItemKind = def.Kind,
                    EntryScreen = def.Screen,
                    DeferralReasonId = defId,
                    SortOrder = def.Sort
                };
                context.ProductionItemTypes.Add(newEnt);
                existing[def.Code] = newEnt;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث بنود الإنتاج.");
        }
    }

    private static async Task<Dictionary<string, JudgmentTreeNode>> SeedJudgmentTreeAsync(JudicialDbContext context, long ruleSetVersionId, ILogger? logger)
    {
        // 1. العقد الرئيسية (Level 1)
        var rootDefs = new[]
        {
            new { Code = "JT_FINAL",         NameAr = "أحكام قطعية (منهية للخصومة)",   Sort = (short)1 },
            new { Code = "JT_INTERLOCUTORY", NameAr = "أحكام فرعية",                  Sort = (short)2 },
            new { Code = "JT_EVIDENCE",      NameAr = "أحكام الإثبات",                Sort = (short)3 },
            new { Code = "JT_STAY",          NameAr = "أحكام الوقف",                  Sort = (short)4 },
            new { Code = "JT_INTERRUPT",     NameAr = "انقطاع سير الخصومة",           Sort = (short)5 },
            new { Code = "JT_ACTIONS",       NameAr = "بيان مستقل وإجراءات قضائية",  Sort = (short)6 },
        };

        var existing = await context.JudgmentTreeNodes
            .Where(x => x.RuleSetVersionId == ruleSetVersionId)
            .ToDictionaryAsync(x => x.Code);

        bool rootsChanged = false;
        foreach (var def in rootDefs)
        {
            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || ent.SortOrder != def.Sort)
                {
                    ent.NameAr = def.NameAr;
                    ent.SortOrder = def.Sort;
                    rootsChanged = true;
                }
            }
            else
            {
                var newEnt = new JudgmentTreeNode
                {
                    RuleSetVersionId = ruleSetVersionId,
                    ParentId = null,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    SortOrder = def.Sort
                };
                context.JudgmentTreeNodes.Add(newEnt);
                existing[def.Code] = newEnt;
                rootsChanged = true;
            }
        }

        if (rootsChanged)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم حفظ العقد الرئيسية لشجرة الأحكام.");
        }

        // 2. الفروع (Level 2)
        var childDefs = new[]
        {
            // فروع أحكام قطعية
            new { Code = "JT_FINAL_FORMAL",       ParentCode = "JT_FINAL",    NameAr = "شكلي",                       Sort = (short)1 },
            new { Code = "JT_FINAL_SUBSTANTIVE",  ParentCode = "JT_FINAL",    NameAr = "موضوعي",                      Sort = (short)2 },
            new { Code = "JT_FINAL_SETTLE",       ParentCode = "JT_FINAL",    NameAr = "صلح",                         Sort = (short)3 },

            // فروع أحكام الإثبات
            new { Code = "JT_EVID_EXPERT",        ParentCode = "JT_EVIDENCE", NameAr = "ندب خبير",                   Sort = (short)1 },
            new { Code = "JT_EVID_EXPERT_RETURN", ParentCode = "JT_EVIDENCE", NameAr = "إعادة للخبير",               Sort = (short)2 },
            new { Code = "JT_EVID_INVESTIGATION", ParentCode = "JT_EVIDENCE", NameAr = "تحقيق",                      Sort = (short)3 },
            new { Code = "JT_EVID_INTERROGATE",   ParentCode = "JT_EVIDENCE", NameAr = "استجواب",                    Sort = (short)4 },
            new { Code = "JT_EVID_OATH",          ParentCode = "JT_EVIDENCE", NameAr = "حلف يمين",                   Sort = (short)5 },

            // فروع أحكام الوقف
            new { Code = "JT_STAY_PENAL",         ParentCode = "JT_STAY",     NameAr = "وقف جزائي",                  Sort = (short)1 },
            new { Code = "JT_STAY_SUSP",          ParentCode = "JT_STAY",     NameAr = "وقف تعليقي",                 Sort = (short)2 },
            new { Code = "JT_STAY_AGREE",         ParentCode = "JT_STAY",     NameAr = "وقف اتفاقي",                 Sort = (short)3 },

            // فروع الإجراءات القضائية
            new { Code = "JT_ACT_EXTEND",         ParentCode = "JT_ACTIONS",  NameAr = "مد أجل النطق بالحكم",       Sort = (short)1 },
            new { Code = "JT_ACT_REOPEN",         ParentCode = "JT_ACTIONS",  NameAr = "إعادة للمرافعة",             Sort = (short)2 },
            new { Code = "JT_ACT_RET_APPEAL",     ParentCode = "JT_ACTIONS",  NameAr = "قضايا معادة من الاستئناف",  Sort = (short)3 },
        };

        bool childrenChanged = false;
        foreach (var def in childDefs)
        {
            var parent = existing[def.ParentCode];
            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || ent.ParentId != parent.Id || ent.SortOrder != def.Sort)
                {
                    ent.NameAr = def.NameAr;
                    ent.ParentId = parent.Id;
                    ent.SortOrder = def.Sort;
                    childrenChanged = true;
                }
            }
            else
            {
                var newEnt = new JudgmentTreeNode
                {
                    RuleSetVersionId = ruleSetVersionId,
                    ParentId = parent.Id,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    SortOrder = def.Sort
                };
                context.JudgmentTreeNodes.Add(newEnt);
                existing[def.Code] = newEnt;
                childrenChanged = true;
            }
        }

        if (childrenChanged)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم حفظ فروع شجرة الأحكام.");
        }

        return await context.JudgmentTreeNodes
            .Where(x => x.RuleSetVersionId == ruleSetVersionId)
            .ToDictionaryAsync(x => x.Code);
    }

    private static async Task<Dictionary<string, JudgmentType>> SeedJudgmentTypesAsync(JudicialDbContext context, ILogger? logger)
    {
        var definitions = new[]
        {
            // أحكام شكلية
            new { Code = "J_NO_ADMIT",           NameAr = "عدم قبول" },
            new { Code = "J_NO_JURIS",           NameAr = "عدم اختصاص" },
            new { Code = "J_DROP_RIGHT",         NameAr = "سقوط الحق في رفع الدعوى" },
            new { Code = "J_EXTINCT",            NameAr = "سقوط وانقضاء الخصومة" },
            new { Code = "J_ABANDON",            NameAr = "ترك الخصومة / التنازل" },
            new { Code = "J_NULLITY",            NameAr = "انعدام الخصومة" },
            new { Code = "J_AS_NOT_EXIST",       NameAr = "اعتبار الدعوى كأن لم تكن" },
            new { Code = "J_OTHER_FORMAL",       NameAr = "أحكام شكلية أخرى" },

            // أحكام موضوعية
            new { Code = "J_ADMIT_TOTAL",        NameAr = "قبول كلي / إلزام بالطلبات" },
            new { Code = "J_ADMIT_PARTIAL",      NameAr = "قبول جزئي" },
            new { Code = "J_REJECT",             NameAr = "رفض الدعوى بحالتها أو موضوعاً" },
            new { Code = "J_PRIOR_JUDGMENT",     NameAr = "عدم جواز لسابقة الفصل فيها" },
            new { Code = "J_PRESCRIPTION",       NameAr = "انقضاء الالتزام بالتقادم" },
            new { Code = "J_OTHER_SUBSTANTIVE",  NameAr = "أحكام موضوعية أخرى" },

            // صلح
            new { Code = "J_SETTLEMENT",         NameAr = "إلحاق عقد الصلح بمحضر الجلسة (إثبات ترك/صلح)" },

            // أحكام فرعية
            new { Code = "J_INTERLOCUTORY",      NameAr = "حكم فرعي / تمهيدي" },

            // أحكام الإثبات
            new { Code = "J_EXPERT_APPOINT",     NameAr = "ندب خبير" },
            new { Code = "J_EXPERT_RETURN",      NameAr = "إعادة للخبير" },
            new { Code = "J_INVESTIGATION",      NameAr = "تحقيق" },
            new { Code = "J_INTERROGATION",      NameAr = "استجواب" },
            new { Code = "J_OATH",               NameAr = "حلف يمين حاسمة / متممة" },

            // أحكام الوقف
            new { Code = "J_STAY_PENAL",         NameAr = "وقف جزائي" },
            new { Code = "J_STAY_SUSP",          NameAr = "وقف تعليقي" },
            new { Code = "J_STAY_AGREE",         NameAr = "وقف اتفاقي" },

            // انقطاع الخصومة
            new { Code = "J_INTERRUPT",          NameAr = "انقطاع سير الخصومة" },

            // إجراءات قضائية وبيان مستقل
            new { Code = "J_EXTEND_JUDGMENT",    NameAr = "مد أجل النطق بالحكم" },
            new { Code = "J_REOPEN_ARGUMENT",    NameAr = "إعادة للمرافعة" },
            new { Code = "J_APPEAL_RETURN",      NameAr = "قضايا معادة من الاستئناف" },
        };

        var existing = await context.JudgmentTypes.ToDictionaryAsync(x => x.Code);
        bool hasChanges = false;

        foreach (var def in definitions)
        {
            if (existing.TryGetValue(def.Code, out var ent))
            {
                if (ent.NameAr != def.NameAr || !ent.IsActive)
                {
                    ent.NameAr = def.NameAr;
                    ent.IsActive = true;
                    hasChanges = true;
                }
            }
            else
            {
                var newEnt = new JudgmentType
                {
                    Code = def.Code,
                    NameAr = def.NameAr,
                    IsActive = true
                };
                context.JudgmentTypes.Add(newEnt);
                existing[def.Code] = newEnt;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث أنواع الأحكام القضائية.");
        }

        return await context.JudgmentTypes.ToDictionaryAsync(x => x.Code);
    }

    private static async Task<Dictionary<string, CaseType>> SeedCaseTaxonomyAndTypesAsync(JudicialDbContext context, ILogger? logger)
    {
        // 1. عقدة القضاء المدني
        var civilRoot = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CIVIL");
        if (civilRoot is null)
        {
            civilRoot = new CaseTaxonomyNode
            {
                Code = "CIVIL",
                NameAr = "القضاء المدني",
                ParentId = null,
                SortOrder = 1
            };
            context.CaseTaxonomyNodes.Add(civilRoot);
            await context.SaveChangesAsync();
        }

        // 2. عقدة مدني كلي
        var civilFullNode = await context.CaseTaxonomyNodes.FirstOrDefaultAsync(n => n.Code == "CIVIL_FULL");
        if (civilFullNode is null)
        {
            civilFullNode = new CaseTaxonomyNode
            {
                Code = "CIVIL_FULL",
                NameAr = "مدني كلي",
                ParentId = civilRoot.Id,
                SortOrder = 1
            };
            context.CaseTaxonomyNodes.Add(civilFullNode);
            await context.SaveChangesAsync();
        }

        // 3. أنواع قضايا مدني كلي (11 نوع دعوى من شيت مدني كلي)
        var caseTypeDefs = new[]
        {
            new { Code = "CIV-FULL-MAIN",       NameAr = "مدني كلي",         Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-AWQAF",      NameAr = "أوقاف",            Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-COMM",       NameAr = "تجاري",            Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-GOV",        NameAr = "حكومة",            Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-RENT",       NameAr = "إيجارات",          Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-INSUR",      NameAr = "تأمينات",          Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-LABOR",      NameAr = "عمال",             Level = "Full", Proc = "Labor", Age = "All" },
            new { Code = "CIV-FULL-APP",        NameAr = "مستأنف مدني كلي",   Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-APP-AWQAF",  NameAr = "مستأنف أوقاف",     Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-APP-URGENT", NameAr = "مستأنف مستعجل",    Level = "Full", Proc = "Civil", Age = "All" },
            new { Code = "CIV-FULL-COMP",       NameAr = "تعويضات",          Level = "Full", Proc = "Civil", Age = "All" },
        };

        var existingCaseTypes = await context.CaseTypes.ToDictionaryAsync(x => x.Code);
        bool caseTypesChanged = false;

        foreach (var def in caseTypeDefs)
        {
            if (existingCaseTypes.TryGetValue(def.Code, out var ct))
            {
                if (ct.NameAr != def.NameAr || ct.TaxonomyNodeId != civilFullNode.Id ||
                    ct.StatisticalLevelCode != def.Level || ct.ProcedureCode != def.Proc || ct.AgeCategoryCode != def.Age)
                {
                    ct.NameAr = def.NameAr;
                    ct.TaxonomyNodeId = civilFullNode.Id;
                    ct.StatisticalLevelCode = def.Level;
                    ct.ProcedureCode = def.Proc;
                    ct.AgeCategoryCode = def.Age;
                    caseTypesChanged = true;
                }
            }
            else
            {
                var newCt = new CaseType
                {
                    TaxonomyNodeId = civilFullNode.Id,
                    Code = def.Code,
                    NameAr = def.NameAr,
                    StatisticalLevelCode = def.Level,
                    ProcedureCode = def.Proc,
                    AgeCategoryCode = def.Age
                };
                context.CaseTypes.Add(newCt);
                existingCaseTypes[def.Code] = newCt;
                caseTypesChanged = true;
            }
        }

        if (caseTypesChanged)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم تحديث أنواع قضايا مدني كلي.");
        }

        return await context.CaseTypes.ToDictionaryAsync(x => x.Code);
    }

    private static async Task SeedCaseRulesAsync(
        JudicialDbContext context,
        long ruleSetVersionId,
        Dictionary<string, CaseType> caseTypes,
        Dictionary<string, JudgmentType> judgmentTypes,
        Dictionary<string, JudgmentTreeNode> treeNodes,
        Dictionary<string, DeferralReason> deferrals,
        Dictionary<string, IncomingType> incomingTypes,
        ILogger? logger)
    {
        // خرائط تصنيف الأحكام الفردية إلى فروع الشجرة
        var judgmentMappings = new JudgmentMappingDef[]
        {
            // أحكام شكلية قطعية
            new("J_NO_ADMIT", "JT_FINAL_FORMAL", true, true, null),
            new("J_NO_JURIS", "JT_FINAL_FORMAL", true, true, null),
            new("J_DROP_RIGHT", "JT_FINAL_FORMAL", true, true, null),
            new("J_EXTINCT", "JT_FINAL_FORMAL", true, true, null),
            new("J_ABANDON", "JT_FINAL_FORMAL", true, true, null),
            new("J_NULLITY", "JT_FINAL_FORMAL", true, true, null),
            new("J_AS_NOT_EXIST", "JT_FINAL_FORMAL", true, true, null),
            new("J_OTHER_FORMAL", "JT_FINAL_FORMAL", true, true, null),

            // أحكام موضوعية قطعية
            new("J_ADMIT_TOTAL", "JT_FINAL_SUBSTANTIVE", true, true, null),
            new("J_ADMIT_PARTIAL", "JT_FINAL_SUBSTANTIVE", true, true, null),
            new("J_REJECT", "JT_FINAL_SUBSTANTIVE", true, true, null),
            new("J_PRIOR_JUDGMENT", "JT_FINAL_SUBSTANTIVE", true, true, null),
            new("J_PRESCRIPTION", "JT_FINAL_SUBSTANTIVE", true, true, null),
            new("J_OTHER_SUBSTANTIVE", "JT_FINAL_SUBSTANTIVE", true, true, null),

            // صلح
            new("J_SETTLEMENT", "JT_FINAL_SETTLE", true, true, null),

            // فرعية
            new("J_INTERLOCUTORY", "JT_INTERLOCUTORY", false, true, "SUB_JUDGMENT_EVID"),

            // إثبات
            new("J_EXPERT_APPOINT", "JT_EVID_EXPERT", false, true, "WAIT_REPORT"),
            new("J_EXPERT_RETURN", "JT_EVID_EXPERT_RETURN", false, true, "WAIT_REPORT"),
            new("J_INVESTIGATION", "JT_EVID_INVESTIGATION", false, true, "SUB_JUDGMENT_EVID"),
            new("J_INTERROGATION", "JT_EVID_INTERROGATE", false, true, "SUB_JUDGMENT_EVID"),
            new("J_OATH", "JT_EVID_OATH", false, true, "SUB_JUDGMENT_EVID"),

            // وقف
            new("J_STAY_PENAL", "JT_STAY_PENAL", false, false, "OTHER_REASONS"),
            new("J_STAY_SUSP", "JT_STAY_SUSP", false, false, "OTHER_REASONS"),
            new("J_STAY_AGREE", "JT_STAY_AGREE", false, false, "OTHER_REASONS"),

            // انقطاع
            new("J_INTERRUPT", "JT_INTERRUPT", false, false, "OTHER_REASONS"),

            // إجراءات
            new("J_EXTEND_JUDGMENT", "JT_ACT_EXTEND", false, false, "EXTEND_JUDGMENT"),
            new("J_REOPEN_ARGUMENT", "JT_ACT_REOPEN", false, false, "REOPEN_ARGUMENT"),
            new("J_APPEAL_RETURN", "JT_ACT_RET_APPEAL", false, false, null),
        };

        // 1. قواعد الأحكام CaseTypeJudgmentRules
        var existingRules = await context.CaseTypeJudgmentRules
            .Where(r => r.RuleSetVersionId == ruleSetVersionId)
            .ToListAsync();

        var ruleLookup = existingRules.ToDictionary(r => (r.CaseTypeId, r.JudgmentTypeId));
        bool rulesChanged = false;

        foreach (var ct in caseTypes.Values)
        {
            foreach (var m in judgmentMappings)
            {
                if (!judgmentTypes.TryGetValue(m.JCode, out var jt)) continue;
                if (!treeNodes.TryGetValue(m.TreeCode, out var node)) continue;

                long? defId = m.DefCode != null && deferrals.TryGetValue(m.DefCode, out var def) ? def.Id : null;

                if (ruleLookup.TryGetValue((ct.Id, jt.Id), out var existingRule))
                {
                    if (existingRule.TreeNodeId != node.Id ||
                        existingRule.CountsInJudgmentTotal != m.IsTotal ||
                        existingRule.RequiresAllocation != m.Alloc ||
                        existingRule.DeferralReasonId != defId)
                    {
                        existingRule.TreeNodeId = node.Id;
                        existingRule.CountsInJudgmentTotal = m.IsTotal;
                        existingRule.RequiresAllocation = m.Alloc;
                        existingRule.DeferralReasonId = defId;
                        rulesChanged = true;
                    }
                }
                else
                {
                    var newRule = new CaseTypeJudgmentRule
                    {
                        RuleSetVersionId = ruleSetVersionId,
                        CaseTypeId = ct.Id,
                        JudgmentTypeId = jt.Id,
                        TreeNodeId = node.Id,
                        CountsInJudgmentTotal = m.IsTotal,
                        RequiresAllocation = m.Alloc,
                        DeferralReasonId = defId
                    };
                    context.CaseTypeJudgmentRules.Add(newRule);
                    ruleLookup[(ct.Id, jt.Id)] = newRule;
                    rulesChanged = true;
                }
            }
        }

        if (rulesChanged)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم حفظ قواعد ربط الأحكام بقضايا مدني كلي.");
        }

        // 2. قواعد التأجيل CaseTypeDeferralRules
        var deferralReturnMappings = new[]
        {
            new { DefCode = "RESERVED",            ReturnIncomingCode = "PREV" },
            new { DefCode = "EXTEND_JUDGMENT",     ReturnIncomingCode = "PREV" },
            new { DefCode = "REOPEN_ARGUMENT",     ReturnIncomingCode = "PREV" },
            new { DefCode = "WAIT_REPORT",         ReturnIncomingCode = "SHORT" },
            new { DefCode = "SUB_JUDGMENT_EVID",   ReturnIncomingCode = "PREV" },
            new { DefCode = "OTHER_REASONS",       ReturnIncomingCode = "PREV" },
            new { DefCode = "REMAINING_DEF",       ReturnIncomingCode = "PREV" },
        };

        var existingDefRules = await context.CaseTypeDeferralRules
            .Where(r => r.RuleSetVersionId == ruleSetVersionId)
            .ToListAsync();

        var defRuleLookup = existingDefRules.ToDictionary(r => (r.CaseTypeId, r.DeferralReasonId));
        bool defRulesChanged = false;

        foreach (var ct in caseTypes.Values)
        {
            foreach (var dm in deferralReturnMappings)
            {
                if (!deferrals.TryGetValue(dm.DefCode, out var def)) continue;
                if (!incomingTypes.TryGetValue(dm.ReturnIncomingCode, out var inc)) continue;

                if (defRuleLookup.TryGetValue((ct.Id, def.Id), out var existingDefRule))
                {
                    if (existingDefRule.ReturnIncomingTypeId != inc.Id)
                    {
                        existingDefRule.ReturnIncomingTypeId = inc.Id;
                        defRulesChanged = true;
                    }
                }
                else
                {
                    var newDefRule = new CaseTypeDeferralRule
                    {
                        RuleSetVersionId = ruleSetVersionId,
                        CaseTypeId = ct.Id,
                        DeferralReasonId = def.Id,
                        ReturnIncomingTypeId = inc.Id
                    };
                    context.CaseTypeDeferralRules.Add(newDefRule);
                    defRuleLookup[(ct.Id, def.Id)] = newDefRule;
                    defRulesChanged = true;
                }
            }
        }

        if (defRulesChanged)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("تم حفظ قواعد أثر التأجيل على المقدم.");
        }
    }

    private static async Task SeedCaseYearPoliciesAsync(JudicialDbContext context, Dictionary<string, CaseType> caseTypes, ILogger? logger)
    {
        var year = await context.JudicialYears.FirstOrDefaultAsync(y => y.Status == "Active")
                   ?? await context.JudicialYears.FirstOrDefaultAsync();

        if (year is null) return;

        var mainCaseType = caseTypes.TryGetValue("CIV-FULL-MAIN", out var ct) ? ct : caseTypes.Values.FirstOrDefault();
        if (mainCaseType is null) return;

        var existingPolicy = await context.CaseYearPolicies
            .Include(p => p.Bands)
            .FirstOrDefaultAsync(p => p.JudicialYearId == year.Id && p.CaseTypeId == mainCaseType.Id);

        if (existingPolicy is null)
        {
            existingPolicy = new CaseYearPolicy
            {
                JudicialYearId = year.Id,
                CaseTypeId = mainCaseType.Id,
                VersionNo = 1,
                DecisionReference = "قرار سياسة تصنيف شرائح سنوات القيد للعام القضائي 2025/2026",
                Status = "Approved"
            };

            existingPolicy.Bands.Add(new CaseYearBand
            {
                BandKind = "AllYears",
                LabelAr = "كافة السنوات",
                SortOrder = 1
            });

            context.CaseYearPolicies.Add(existingPolicy);
            await context.SaveChangesAsync();
            logger?.LogInformation("تم إنشاء سياسة سنوات القيد الافتراضية للعام القضائي.");
        }
        else if (existingPolicy.Bands.Count == 0)
        {
            existingPolicy.Bands.Add(new CaseYearBand
            {
                BandKind = "AllYears",
                LabelAr = "كافة السنوات",
                SortOrder = 1
            });
            await context.SaveChangesAsync();
        }
    }

    private record ProductionItemDef(string Code, string NameAr, string Kind, string Screen, string? DefCode, short Sort);
    private record JudgmentMappingDef(string JCode, string TreeCode, bool IsTotal, bool Alloc, string? DefCode);
}
