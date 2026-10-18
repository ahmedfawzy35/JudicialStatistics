using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JudicialStatistics.Domain.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Persistence.Seed;

/// <summary>
/// بذر القوالب الافتراضية لاستيراد الإحصائيات من ملف الإكسل
/// مستخلصة بدقة من ملف نسخة أكتوبر 2025/2026 المعتمد
/// </summary>
public static class ImportTemplateSeedData
{
    public static async Task SeedAsync(JudicialDbContext context, ILogger? logger = null)
    {
        if (await context.ExcelImportTemplates.AnyAsync())
        {
            logger?.LogInformation("قوالب استيراد الإكسل مسجلة مسبقاً، تم التخطي.");
            return;
        }

        logger?.LogInformation("بدء تهيئة قوالب استيراد الإكسل الافتراضية...");

        // 1. قالب مدني كلي
        var civilFullTemplate = CreateCivilFullTemplate();
        context.ExcelImportTemplates.Add(civilFullTemplate);

        // 2. قالب الأسرة
        var familyTemplate = CreateFamilyTemplate();
        context.ExcelImportTemplates.Add(familyTemplate);

        // 3. قالب العمالية
        var laborTemplate = CreateLaborTemplate();
        context.ExcelImportTemplates.Add(laborTemplate);

        // 4. قالب جنح مستأنف
        var crimAppealTemplate = CreateCriminalAppealTemplate();
        context.ExcelImportTemplates.Add(crimAppealTemplate);

        // 5. قالب جنح جزئي
        var crimPartialTemplate = CreateCriminalPartialTemplate();
        context.ExcelImportTemplates.Add(crimPartialTemplate);

        // 6. قالب مدني جزئي وصحة توقيع
        var civilPartialTemplate = CreateCivilPartialTemplate();
        context.ExcelImportTemplates.Add(civilPartialTemplate);

        await context.SaveChangesAsync();
        logger?.LogInformation("تم حفظ قوالب استيراد الإكسل الافتراضية بنجاح.");
    }

    private static ExcelImportTemplate CreateCivilFullTemplate()
    {
        var t = new ExcelImportTemplate
        {
            NameAr = "قالب مدني كلي (2025/2026)",
            DisciplineKind = "CivilFull",
            StatisticsSheetName = "مدني كلي",
            TashkeelSheetName = "التشكيل",
            IsDefault = true,

            TashkeelCircuitStartRow = 5,
            TashkeelCircuitNoCol = "A",
            TashkeelCircuitNameCol = "B",

            TashkeelSessionStartRow = 5,
            TashkeelSessionCircuitNoCol = "E",
            TashkeelSessionNoCol = "F",
            TashkeelSessionNameCol = "G",
            TashkeelSessionTypeCol = "H",
            TashkeelSessionDayCol = "I",
            TashkeelSessionStartBlockCol = "J", // بداية الرنج
            TashkeelSessionCourtCol = "K",

            TashkeelJudgeStartRow = 5,
            TashkeelJudgeNoCol = "L",
            TashkeelJudgeNameCol = "M",
            TashkeelJudgeCircuitNoCol = "P",
            TashkeelJudgeRoleCol = "R",
            TashkeelJudgeStatusCol = "S",

            BlockTotalRows = 15,
            YearBandsCount = 5,
            PrimaryDataRowOffset = 1,     // صف 7 = 6 + 1 (المقدم، الإنتاج، قاضي 1 و2)
            SecondaryJudgesRowOffset = 8, // صف 14 = 6 + 8 (قاضي 3 و4 و6)
            Judge1HeaderRowOffset = 0,    // صف 6 = 6 + 0
            Judge3HeaderRowOffset = 7,    // صف 13 = 6 + 7
            Judge1HeaderCol = "BD",
            Judge2HeaderCol = "CF",
            Judge5HeaderCol = "DH"
        };

        int order = 1;

        // المقدم
        AddCol(t, "Incoming", "L", "YEAR", "سنة الدعوى", order++);
        AddCol(t, "Incoming", "M", "PREV", "السابق", order++);
        AddCol(t, "Incoming", "N", "RENEW", "مجدد من الشطب", order++);
        AddCol(t, "Incoming", "O", "RECUSAL_WAIT", "لحين الفصل في طلب الرد", order++);
        AddCol(t, "Incoming", "P", "ACCEL_PENAL", "معجل من الوقف جزائي", order++);
        AddCol(t, "Incoming", "Q", "ACCEL_SUSP", "معجل من الوقف تعليقي", order++);
        AddCol(t, "Incoming", "R", "ACCEL_AGREE", "معجل من الوقف اتفاقي", order++);
        AddCol(t, "Incoming", "S", "ACCEL_DISCONT", "معجل من الانقطاع", order++);
        AddCol(t, "Incoming", "T", "RETURN_APPEAL", "معاد من الاستئناف", order++);
        AddCol(t, "Incoming", "U", "SHORT", "التقصير الوارد", order++);
        AddCol(t, "Incoming", "V", "SHORT_MONTH", "الشهر المقصر منه", order++);
        AddCol(t, "Incoming", "W", "TRANSFER_OUT", "إحالة من الدائرة", order++);
        AddCol(t, "Incoming", "X", "TRANSFER_IN", "إحالة إلى الدائرة", order++);
        AddCol(t, "Incoming", "Y", "NEW", "الجديد", order++);

        // الإنتاج
        AddCol(t, "Production", "AA", "STRIKE", "شطب الدعوى", order++);
        AddCol(t, "Production", "AB", "RECUSAL", "طلب الرد", order++);
        AddCol(t, "Production", "AC", "FORMAL", "شكلي", order++);
        AddCol(t, "Production", "AD", "SUBST", "موضوعي", order++);
        AddCol(t, "Production", "AE", "CONCIL", "صلح", order++);
        AddCol(t, "Production", "AF", "TOTAL_FINAL", "جملة الأحكام القطعية", order++);
        AddCol(t, "Production", "AG", "INTERLOC", "أحكام فرعية", order++);
        AddCol(t, "Production", "AH", "EXPERT_APPOINT", "ندب خبير", order++);
        AddCol(t, "Production", "AI", "EXPERT_RET", "إعادة للخبير", order++);
        AddCol(t, "Production", "AJ", "INVESTIG", "تحقيق", order++);
        AddCol(t, "Production", "AK", "INTERROG", "استجواب", order++);
        AddCol(t, "Production", "AL", "OATH", "حلف يمين", order++);
        AddCol(t, "Production", "AM", "TOTAL_EVID", "جملة الاثبات", order++);
        AddCol(t, "Production", "AN", "STAY_PENAL", "وقف جزائي", order++);
        AddCol(t, "Production", "AO", "STAY_SUSP", "وقف تعليقي", order++);
        AddCol(t, "Production", "AP", "STAY_AGREE", "وقف اتفاقي", order++);
        AddCol(t, "Production", "AQ", "TOTAL_STAY", "جملة الوقف", order++);
        AddCol(t, "Production", "AR", "INTERRUPT", "انقطاع سير الخصومة", order++);
        AddCol(t, "Production", "AS", "TOTAL_ADJUD", "جملة المحكوم فيه", order++);
        AddCol(t, "Production", "AT", "DEF_RESERVED", "محجوز للحكم", order++);
        AddCol(t, "Production", "AU", "DEF_EXTEND", "مد أجل", order++);
        AddCol(t, "Production", "AV", "DEF_REHEARING", "إعادة للمرافعة", order++);
        AddCol(t, "Production", "AW", "DEF_REPORT", "المؤجل لورود التقرير", order++);
        AddCol(t, "Production", "AX", "DEF_OTHER", "مؤجل لاسباب أخرى", order++);
        AddCol(t, "Production", "AY", "DEF_TOTAL", "جملة المؤجل", order++);

        // أحكام القاضي الأول والثالث (Slot 1)
        AddJudgeCols(t, 1, "BD", order);

        // أحكام القاضي الثاني والرابع (Slot 2)
        AddJudgeCols(t, 2, "CF", order);

        // أحكام القاضي الخامس والسادس (Slot 3)
        AddJudgeCols(t, 3, "DH", order);

        // المؤجلات على شهور
        AddCol(t, "Postponement", "EK", "INTERLOC_EVID", "فرعي وإثبات - شهر 1", order++, targetMonthOffset: 1);
        AddCol(t, "Postponement", "EL", "INTERLOC_EVID", "فرعي وإثبات - شهر 2", order++, targetMonthOffset: 2);
        AddCol(t, "Postponement", "EM", "INTERLOC_EVID", "فرعي وإثبات - شهر 3", order++, targetMonthOffset: 3);
        AddCol(t, "Postponement", "EO", "DEF_RESERVED", "محجوز للحكم - شهر 1", order++, targetMonthOffset: 1);
        AddCol(t, "Postponement", "EP", "DEF_RESERVED", "محجوز للحكم - شهر 2", order++, targetMonthOffset: 2);
        AddCol(t, "Postponement", "EQ", "DEF_RESERVED", "محجوز للحكم - شهر 3", order++, targetMonthOffset: 3);
        AddCol(t, "Postponement", "ES", "DEF_EXTEND", "مد الأجل - شهر 1", order++, targetMonthOffset: 1);
        AddCol(t, "Postponement", "ET", "DEF_EXTEND", "مد الأجل - شهر 2", order++, targetMonthOffset: 2);
        AddCol(t, "Postponement", "EU", "DEF_EXTEND", "مد الأجل - شهر 3", order++, targetMonthOffset: 3);
        AddCol(t, "Postponement", "EW", "DEF_REHEARING", "إعادة للمرافعة - شهر 1", order++, targetMonthOffset: 1);
        AddCol(t, "Postponement", "EX", "DEF_REHEARING", "إعادة للمرافعة - شهر 2", order++, targetMonthOffset: 2);
        AddCol(t, "Postponement", "EZ", "DEF_OTHER", "باقي المؤجلات - شهر 1", order++, targetMonthOffset: 1);
        AddCol(t, "Postponement", "FA", "DEF_OTHER", "باقي المؤجلات - شهر 2", order++, targetMonthOffset: 2);
        AddCol(t, "Postponement", "FB", "DEF_OTHER", "باقي المؤجلات - شهر 3", order++, targetMonthOffset: 3);
        AddCol(t, "Postponement", "FO", "DEF_REPORT", "مؤجل للتقرير - شهر 1", order++, targetMonthOffset: 1);
        AddCol(t, "Postponement", "FP", "DEF_REPORT", "مؤجل للتقرير - شهر 2", order++, targetMonthOffset: 2);
        AddCol(t, "Postponement", "FQ", "DEF_REPORT", "مؤجل للتقرير - شهر 3", order++, targetMonthOffset: 3);

        return t;
    }

    private static ExcelImportTemplate CreateFamilyTemplate()
    {
        var t = new ExcelImportTemplate
        {
            NameAr = "قالب الأسرة (2025/2026)",
            DisciplineKind = "Family",
            StatisticsSheetName = "اسرة",
            TashkeelSheetName = "التشكيل",
            IsDefault = true,

            TashkeelCircuitStartRow = 58,
            TashkeelCircuitNoCol = "A",
            TashkeelCircuitNameCol = "B",

            TashkeelSessionStartRow = 58,
            TashkeelSessionCircuitNoCol = "E",
            TashkeelSessionNoCol = "F",
            TashkeelSessionNameCol = "G",
            TashkeelSessionTypeCol = "H",
            TashkeelSessionDayCol = "I",
            TashkeelSessionStartBlockCol = "J",
            TashkeelSessionCourtCol = "K",

            TashkeelJudgeStartRow = 58,
            TashkeelJudgeNoCol = "L",
            TashkeelJudgeNameCol = "M",
            TashkeelJudgeCircuitNoCol = "P",
            TashkeelJudgeRoleCol = "R",
            TashkeelJudgeStatusCol = "S",

            BlockTotalRows = 15,
            YearBandsCount = 5,
            PrimaryDataRowOffset = 1,
            SecondaryJudgesRowOffset = 8,
            Judge1HeaderRowOffset = 0,
            Judge3HeaderRowOffset = 7,
            Judge1HeaderCol = "BD",
            Judge2HeaderCol = "CF",
            Judge5HeaderCol = "DH"
        };

        int order = 1;
        AddCol(t, "Incoming", "L", "YEAR", "سنة الدعوى", order++);
        AddCol(t, "Incoming", "M", "PREV", "السابق", order++);
        AddCol(t, "Incoming", "N", "RENEW", "مجدد من الشطب", order++);
        AddCol(t, "Incoming", "O", "RECUSAL_WAIT", "لحين الفصل في طلب الرد", order++);
        AddCol(t, "Incoming", "P", "ACCEL_PENAL", "معجل من الوقف جزائي", order++);
        AddCol(t, "Incoming", "Q", "ACCEL_SUSP", "معجل من الوقف تعليقي", order++);
        AddCol(t, "Incoming", "R", "ACCEL_AGREE", "معجل من الوقف اتفاقي", order++);
        AddCol(t, "Incoming", "S", "ACCEL_DISCONT", "معجل من الانقطاع", order++);
        AddCol(t, "Incoming", "T", "RETURN_APPEAL", "معاد من الاستئناف", order++);
        AddCol(t, "Incoming", "U", "SHORT", "التقصير الوارد", order++);
        AddCol(t, "Incoming", "V", "SHORT_MONTH", "الشهر المقصر منه", order++);
        AddCol(t, "Incoming", "W", "TRANSFER_OUT", "إحالة من الدائرة", order++);
        AddCol(t, "Incoming", "X", "TRANSFER_IN", "إحالة إلى الدائرة", order++);
        AddCol(t, "Incoming", "Y", "NEW", "الجديد", order++);

        AddCol(t, "Production", "AA", "STRIKE", "شطب الدعوى", order++);
        AddCol(t, "Production", "AB", "RECUSAL", "طلب الرد", order++);
        AddCol(t, "Production", "AC", "FORMAL", "شكلي", order++);
        AddCol(t, "Production", "AD", "SUBST", "موضوعي", order++);
        AddCol(t, "Production", "AE", "CONCIL", "صلح", order++);
        AddCol(t, "Production", "AF", "TOTAL_FINAL", "جملة الأحكام القطعية", order++);
        AddCol(t, "Production", "AG", "INTERLOC", "أحكام فرعية", order++);
        AddCol(t, "Production", "AH", "EXPERT_APPOINT", "ندب خبير", order++);
        AddCol(t, "Production", "AI", "EXPERT_RET", "إعادة للخبير", order++);
        AddCol(t, "Production", "AJ", "INVESTIG", "تحقيق", order++);
        AddCol(t, "Production", "AK", "INTERROG", "استجواب", order++);
        AddCol(t, "Production", "AL", "OATH", "حلف يمين", order++);
        AddCol(t, "Production", "AM", "TOTAL_EVID", "جملة الاثبات", order++);
        AddCol(t, "Production", "AN", "STAY_PENAL", "وقف جزائي", order++);
        AddCol(t, "Production", "AO", "STAY_SUSP", "وقف تعليقي", order++);
        AddCol(t, "Production", "AP", "STAY_AGREE", "وقف اتفاقي", order++);
        AddCol(t, "Production", "AQ", "TOTAL_STAY", "جملة الوقف", order++);
        AddCol(t, "Production", "AR", "INTERRUPT", "انقطاع سير الخصومة", order++);
        AddCol(t, "Production", "AS", "TOTAL_ADJUD", "جملة المحكوم فيه", order++);
        AddCol(t, "Production", "AT", "DEF_RESERVED", "محجوز للحكم", order++);
        AddCol(t, "Production", "AU", "DEF_EXTEND", "مد أجل", order++);
        AddCol(t, "Production", "AV", "DEF_REHEARING", "إعادة للمرافعة", order++);
        AddCol(t, "Production", "AW", "DEF_REPORT", "المؤجل لورود التقرير", order++);
        AddCol(t, "Production", "AX", "DEF_OTHER", "مؤجل لاسباب أخرى", order++);
        AddCol(t, "Production", "AY", "DEF_TOTAL", "جملة المؤجل", order++);

        AddJudgeCols(t, 1, "BD", order);
        AddJudgeCols(t, 2, "CF", order);
        AddJudgeCols(t, 3, "DH", order);

        return t;
    }

    private static ExcelImportTemplate CreateLaborTemplate()
    {
        var t = new ExcelImportTemplate
        {
            NameAr = "قالب العمالية (2025/2026)",
            DisciplineKind = "Labor",
            StatisticsSheetName = "العمالية (عمال + تأمينات ) ",
            TashkeelSheetName = "التشكيل",
            IsDefault = true,

            TashkeelCircuitStartRow = 171,
            TashkeelCircuitNoCol = "A",
            TashkeelCircuitNameCol = "B",

            TashkeelSessionStartRow = 171,
            TashkeelSessionCircuitNoCol = "E",
            TashkeelSessionNoCol = "F",
            TashkeelSessionNameCol = "G",
            TashkeelSessionTypeCol = "H",
            TashkeelSessionDayCol = "I",
            TashkeelSessionStartBlockCol = "J",
            TashkeelSessionCourtCol = "K",

            TashkeelJudgeStartRow = 171,
            TashkeelJudgeNoCol = "L",
            TashkeelJudgeNameCol = "M",
            TashkeelJudgeCircuitNoCol = "P",
            TashkeelJudgeRoleCol = "R",
            TashkeelJudgeStatusCol = "S",

            BlockTotalRows = 15,
            YearBandsCount = 5,
            PrimaryDataRowOffset = 1,
            SecondaryJudgesRowOffset = 8,
            Judge1HeaderRowOffset = 0,
            Judge3HeaderRowOffset = 7,
            Judge1HeaderCol = "BD",
            Judge2HeaderCol = "CF",
            Judge5HeaderCol = "DH"
        };

        int order = 1;
        AddCol(t, "Incoming", "L", "YEAR", "سنة الدعوى", order++);
        AddCol(t, "Incoming", "M", "PREV", "السابق", order++);
        AddCol(t, "Incoming", "N", "RENEW", "مجدد من الشطب", order++);
        AddCol(t, "Incoming", "O", "RECUSAL_WAIT", "لحين الفصل في طلب الرد", order++);
        AddCol(t, "Incoming", "P", "ACCEL_PENAL", "معجل من الوقف جزائي", order++);
        AddCol(t, "Incoming", "Q", "ACCEL_SUSP", "معجل من الوقف تعليقي", order++);
        AddCol(t, "Incoming", "R", "ACCEL_AGREE", "معجل من الوقف اتفاقي", order++);
        AddCol(t, "Incoming", "S", "ACCEL_DISCONT", "معجل من الانقطاع", order++);
        AddCol(t, "Incoming", "T", "RETURN_APPEAL", "معاد من الاستئناف", order++);
        AddCol(t, "Incoming", "U", "SHORT", "التقصير الوارد", order++);
        AddCol(t, "Incoming", "V", "SHORT_MONTH", "الشهر المقصر منه", order++);
        AddCol(t, "Incoming", "W", "TRANSFER_OUT", "إحالة من الدائرة", order++);
        AddCol(t, "Incoming", "X", "TRANSFER_IN", "إحالة إلى الدائرة", order++);
        AddCol(t, "Incoming", "Y", "NEW", "الجديد", order++);

        AddCol(t, "Production", "AA", "STRIKE", "شطب الدعوى", order++);
        AddCol(t, "Production", "AB", "RECUSAL", "طلب الرد", order++);
        AddCol(t, "Production", "AC", "FORMAL", "شكلي", order++);
        AddCol(t, "Production", "AD", "SUBST", "موضوعي", order++);
        AddCol(t, "Production", "AE", "CONCIL", "صلح", order++);
        AddCol(t, "Production", "AF", "TOTAL_FINAL", "جملة الأحكام القطعية", order++);
        AddCol(t, "Production", "AG", "INTERLOC", "أحكام فرعية", order++);
        AddCol(t, "Production", "AH", "EXPERT_APPOINT", "ندب خبير", order++);
        AddCol(t, "Production", "AI", "EXPERT_RET", "إعادة للخبير", order++);
        AddCol(t, "Production", "AJ", "INVESTIG", "تحقيق", order++);
        AddCol(t, "Production", "AK", "INTERROG", "استجواب", order++);
        AddCol(t, "Production", "AL", "OATH", "حلف يمين", order++);
        AddCol(t, "Production", "AM", "TOTAL_EVID", "جملة الاثبات", order++);
        AddCol(t, "Production", "AN", "STAY_PENAL", "وقف جزائي", order++);
        AddCol(t, "Production", "AO", "STAY_SUSP", "وقف تعليقي", order++);
        AddCol(t, "Production", "AP", "STAY_AGREE", "وقف اتفاقي", order++);
        AddCol(t, "Production", "AQ", "TOTAL_STAY", "جملة الوقف", order++);
        AddCol(t, "Production", "AR", "INTERRUPT", "انقطاع سير الخصومة", order++);
        AddCol(t, "Production", "AS", "TOTAL_ADJUD", "جملة المحكوم فيه", order++);
        AddCol(t, "Production", "AT", "DEF_RESERVED", "محجوز للحكم", order++);
        AddCol(t, "Production", "AU", "DEF_EXTEND", "مد أجل", order++);
        AddCol(t, "Production", "AV", "DEF_REHEARING", "إعادة للمرافعة", order++);
        AddCol(t, "Production", "AW", "DEF_REPORT", "المؤجل لورود التقرير", order++);
        AddCol(t, "Production", "AX", "DEF_OTHER", "مؤجل لاسباب أخرى", order++);
        AddCol(t, "Production", "AY", "DEF_TOTAL", "جملة المؤجل", order++);

        AddJudgeCols(t, 1, "BD", order);
        AddJudgeCols(t, 2, "CF", order);
        AddJudgeCols(t, 3, "DH", order);

        return t;
    }

    private static ExcelImportTemplate CreateCriminalAppealTemplate()
    {
        var t = new ExcelImportTemplate
        {
            NameAr = "قالب جنح مستأنف (2025/2026)",
            DisciplineKind = "CriminalAppeal",
            StatisticsSheetName = "جنح مستأنف",
            TashkeelSheetName = "التشكيل",
            IsDefault = true,

            TashkeelCircuitStartRow = 115,
            TashkeelCircuitNoCol = "A",
            TashkeelCircuitNameCol = "B",

            TashkeelSessionStartRow = 115,
            TashkeelSessionCircuitNoCol = "E",
            TashkeelSessionNoCol = "F",
            TashkeelSessionNameCol = "G",
            TashkeelSessionTypeCol = "H",
            TashkeelSessionDayCol = "I",
            TashkeelSessionStartBlockCol = "J",
            TashkeelSessionCourtCol = "K",

            TashkeelJudgeStartRow = 115,
            TashkeelJudgeNoCol = "L",
            TashkeelJudgeNameCol = "M",
            TashkeelJudgeCircuitNoCol = "P",
            TashkeelJudgeRoleCol = "R",
            TashkeelJudgeStatusCol = "S",

            BlockTotalRows = 1,
            YearBandsCount = 1,
            PrimaryDataRowOffset = 0,
            SecondaryJudgesRowOffset = 0,
            Judge1HeaderRowOffset = 0,
            Judge3HeaderRowOffset = 0
        };

        int order = 1;
        // المقدم
        AddCol(t, "Incoming", "F", "CRIM_PREV", "سابق ومتداول", order++);
        AddCol(t, "Incoming", "J", "CRIM_NEW", "جديد ومعارضات", order++);
        AddCol(t, "Incoming", "L", "CRIM_TOTAL_INCOMING", "جملة المقدم", order++);

        // المؤجل
        AddCol(t, "Postponement", "N", "CRIM_DEF_TOTAL", "جملة المؤجل", order++);
        AddCol(t, "Postponement", "O", "CRIM_DEF_NOTICE", "مؤجل للإعلان", order++);

        // الإنتاج الفئوي
        AddCol(t, "Production", "W", "CRIM_CAT_SUBST", "أحكام موضوعية", order++);
        AddCol(t, "Production", "X", "CRIM_CAT_PROC", "أحكام إجرائية", order++);
        AddCol(t, "Production", "Y", "CRIM_CAT_ORDER", "أوامر جنائية", order++);
        AddCol(t, "Production", "Z", "CRIM_CAT_PREP", "أحكام تمهيدية", order++);
        AddCol(t, "Production", "AA", "CRIM_CAT_STAY", "الوقف", order++);
        AddCol(t, "Production", "AB", "CRIM_CAT_EXEC", "إشكالات التنفيذ", order++);
        AddCol(t, "Production", "AC", "CRIM_CAT_OTHER", "أخرى", order++);

        return t;
    }

    private static ExcelImportTemplate CreateCriminalPartialTemplate()
    {
        var t = new ExcelImportTemplate
        {
            NameAr = "قالب جنح جزئي (2025/2026)",
            DisciplineKind = "PartialMisdemeanor",
            StatisticsSheetName = "جنح المحاكم الجزئية",
            TashkeelSheetName = "التشكيل",
            IsDefault = true,

            TashkeelCircuitStartRow = 219,
            TashkeelCircuitNoCol = "A",
            TashkeelCircuitNameCol = "B",

            TashkeelSessionStartRow = 219,
            TashkeelSessionCircuitNoCol = "E",
            TashkeelSessionNoCol = "F",
            TashkeelSessionNameCol = "G",
            TashkeelSessionTypeCol = "H",
            TashkeelSessionDayCol = "I",
            TashkeelSessionStartBlockCol = "J",
            TashkeelSessionCourtCol = "K",

            TashkeelJudgeStartRow = 219,
            TashkeelJudgeNoCol = "L",
            TashkeelJudgeNameCol = "M",
            TashkeelJudgeCircuitNoCol = "P",
            TashkeelJudgeRoleCol = "R",
            TashkeelJudgeStatusCol = "S",

            BlockTotalRows = 1,
            YearBandsCount = 1,
            PrimaryDataRowOffset = 0,
            SecondaryJudgesRowOffset = 0,
            Judge1HeaderRowOffset = 0,
            Judge3HeaderRowOffset = 0
        };

        int order = 1;
        AddCol(t, "Incoming", "H", "CRIM_PREV", "سابق", order++);
        AddCol(t, "Incoming", "I", "CRIM_ACCEL_RENEW", "معجل ومجدد", order++);
        AddCol(t, "Incoming", "L", "CRIM_NEW", "جديد ومعارضات", order++);
        AddCol(t, "Incoming", "N", "CRIM_TOTAL_INCOMING", "جملة المقدم", order++);

        AddCol(t, "Postponement", "O", "CRIM_DEF_TOTAL", "جملة المؤجل", order++);

        return t;
    }

    private static ExcelImportTemplate CreateCivilPartialTemplate()
    {
        var t = new ExcelImportTemplate
        {
            NameAr = "قالب مدني جزئي وصحة توقيع (2025/2026)",
            DisciplineKind = "PartialCivil",
            StatisticsSheetName = "ترتيب الدوائر م جزئي و ص ت",
            TashkeelSheetName = "التشكيل",
            IsDefault = true,

            TashkeelCircuitStartRow = 219,
            TashkeelCircuitNoCol = "A",
            TashkeelCircuitNameCol = "B",

            TashkeelSessionStartRow = 219,
            TashkeelSessionCircuitNoCol = "E",
            TashkeelSessionNoCol = "F",
            TashkeelSessionNameCol = "G",
            TashkeelSessionTypeCol = "H",
            TashkeelSessionDayCol = "I",
            TashkeelSessionStartBlockCol = "J",
            TashkeelSessionCourtCol = "K",

            TashkeelJudgeStartRow = 219,
            TashkeelJudgeNoCol = "L",
            TashkeelJudgeNameCol = "M",
            TashkeelJudgeCircuitNoCol = "P",
            TashkeelJudgeRoleCol = "R",
            TashkeelJudgeStatusCol = "S",

            BlockTotalRows = 1,
            YearBandsCount = 1,
            PrimaryDataRowOffset = 0,
            SecondaryJudgesRowOffset = 0,
            Judge1HeaderRowOffset = 0,
            Judge3HeaderRowOffset = 0
        };

        int order = 1;
        AddCol(t, "Incoming", "H", "PREV", "سابق", order++);
        AddCol(t, "Incoming", "I", "RENEW", "معجل ومجدد", order++);
        AddCol(t, "Incoming", "L", "NEW", "جديد", order++);
        AddCol(t, "Incoming", "N", "TOTAL_INCOMING", "جملة المقدم", order++);

        return t;
    }

    private static void AddJudgeCols(ExcelImportTemplate t, int slotIndex, string startCol, int baseOrder)
    {
        int startColNum = ColToNum(startCol);
        var judgeJudgments = new (string Code, string Name)[]
        {
            ("FORMAL_INADMIS", "عدم قبول"),
            ("FORMAL_NO_JURIS", "عدم اختصاص"),
            ("FORMAL_FORFEIT", "سقوط الحق في رفع الدعوى"),
            ("FORMAL_LAPSE", "سقوط الخصومة وانقضائها"),
            ("FORMAL_ABANDON", "ترك الخصومة"),
            ("FORMAL_NULLITY", "انعدام الخصومة"),
            ("FORMAL_NON_EXIST", "كأن لم تكن"),
            ("SUBST_ACCEPT", "قبول"),
            ("SUBST_REJECT", "رفض"),
            ("SUBST_REJECT_STATE", "رفض بحالتها"),
            ("SUBST_RES_JUDICATA", "عدم جواز لسابقة الفصل"),
            ("SUBST_LIMITATION", "انقضاء الحق بمضي المدة"),
            ("CONCIL", "صلح"),
            ("TOTAL_FINAL", "اجمالي الاحكام القطعية"),
            ("INTERLOC", "أحكام فرعية"),
            ("EXPERT_APPOINT", "ندب خبير"),
            ("EXPERT_RET", "إعادة للخبير"),
            ("INVESTIG", "تحقيق"),
            ("INTERROG", "استجواب"),
            ("OATH", "حلف يمين"),
            ("STAY_PENAL", "جزائي"),
            ("STAY_SUSP", "تعليقي"),
            ("STAY_AGREE", "اتفاقي"),
            ("INTERRUPT", "انقطاع سير الخصومة")
        };

        for (int i = 0; i < judgeJudgments.Length; i++)
        {
            string colLetter = NumToCol(startColNum + i);
            AddCol(t, "JudgeJudgment", colLetter, judgeJudgments[i].Code, judgeJudgments[i].Name, baseOrder++, slotIndex);
        }
    }

    private static void AddCol(ExcelImportTemplate t, string section, string colLetter, string targetCode, string nameAr, int displayOrder, int? judgeSlot = null, int? targetMonthOffset = null)
    {
        t.ColumnMappings.Add(new ExcelImportColumnMapping
        {
            Section = section,
            ColumnLetter = colLetter.ToUpperInvariant(),
            TargetItemCode = targetCode,
            TargetItemNameAr = nameAr,
            DisplayOrder = displayOrder,
            JudgeSlotIndex = judgeSlot,
            TargetMonthOffset = targetMonthOffset
        });
    }

    public static int ColToNum(string col)
    {
        int result = 0;
        foreach (char c in col.ToUpperInvariant())
        {
            result = result * 26 + (c - 'A' + 1);
        }
        return result;
    }

    public static string NumToCol(int num)
    {
        string result = "";
        while (num > 0)
        {
            int mod = (num - 1) % 26;
            result = (char)('A' + mod) + result;
            num = (num - mod) / 26;
        }
        return result;
    }
}
