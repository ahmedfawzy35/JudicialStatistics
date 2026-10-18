using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using JudicialStatistics.Application.Import;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Services;

public class FormationExportService : IFormationExportService
{
    private readonly JudicialDbContext _context;

    public FormationExportService(JudicialDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> ExportFormationWorkbookAsync(long judicialYearId, long? periodId = null)
    {
        // 1. العام القضائي
        var year = await _context.JudicialYears
            .FirstOrDefaultAsync(y => y.Id == judicialYearId)
            ?? await _context.JudicialYears.FirstOrDefaultAsync(y => y.Status == "Active")
            ?? await _context.JudicialYears.OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();

        if (year == null)
        {
            throw new InvalidOperationException("لم يتم العثور على أي عام قضائي مسجل في النظام.");
        }

        // 2. الجداول المرجعية الديناميكية من قاعدة البيانات
        var allCourts = await _context.CourtUnits
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var courtMap = allCourts.ToDictionary(c => c.Code, c => c);
        var rootCourt = courtMap.GetValueOrDefault("BNISF-COURT") ?? allCourts.First();

        // استبعاد المحكمة الابتدائية الأم من قائمة الاختيارات وجدول المراجع
        var courtUnits = allCourts
            .Where(c => c.ParentCourtUnitId != null && c.UnitKind != "Court" && c.Code != "BNISF-COURT" && c.Code != "FYM-COURT")
            .OrderBy(c => c.Id)
            .ToList();

        var disciplines = await _context.StatisticalDisciplines
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToListAsync();

        var caseTypes = await _context.CaseTypes
            .OrderBy(c => c.StatisticalLevelCode)
            .ThenBy(c => c.NameAr)
            .ToListAsync();

        // أيام العمل وحالة القاضي
        var workDays = new List<string> { "السبت", "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس" };
        var judgeStatuses = new List<string> { "يعمل", "لا يعمل" };

        // 3. الدوائر والجلسات والقضاة
        var circuits = await _context.Circuits
            .Include(c => c.CourtUnit)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.JudicialRole)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Employee)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Versions)
                    .ThenInclude(v => v.SessionCaseTypes)
                        .ThenInclude(sct => sct.CaseType)
            .Where(c => c.JudicialYearId == year.Id && !c.IsClosed)
            .OrderBy(c => c.Id)
            .ToListAsync();

        // أنواع الدوائر الديناميكية من الدوائر الحالية
        var circuitKinds = new List<string> { "مدني كلي", "أسرة", "جنح مستأنف", "عمالية", "جزئي" };
        foreach (var c in circuits)
        {
            var k = MapCircuitKindToAr(c.Versions.FirstOrDefault()?.CircuitKind);
            if (!circuitKinds.Contains(k)) circuitKinds.Add(k);
        }

        // جميع الجلسات مرتبة حسب الترتيب القياسي لأقسام التشكيل
        var allSessions = new List<(Circuit Circuit, Session Session, string DisciplineName, string CaseTypeName, string Day, int StartRange)>();

        // ترتيب الأقسام: مدني كلي -> أسرة -> جنح مستأنف -> عمالية -> جزئي (جنح ثم مدني)
        var civilFullCircuits = circuits.Where(c => c.Versions.FirstOrDefault()?.CircuitKind == "CivilFull").ToList();
        var familyCircuits = circuits.Where(c => c.Versions.FirstOrDefault()?.CircuitKind == "Family").ToList();
        var criminalAppealCircuits = circuits.Where(c => c.Versions.FirstOrDefault()?.CircuitKind == "CriminalAppeal").ToList();
        var laborCircuits = circuits.Where(c => c.Versions.FirstOrDefault()?.CircuitKind == "Labor").ToList();
        var partialCircuits = circuits.Where(c =>
        {
            var k = c.Versions.FirstOrDefault()?.CircuitKind;
            return k == "Partial" || k == "Summary";
        }).ToList();

        // 1. مدني كلي
        int civilStart = 6;
        foreach (var c in civilFullCircuits)
        {
            foreach (var s in c.Sessions.OrderBy(s => s.Id))
            {
                var (typeName, day) = ParseSessionSchedule(s);
                allSessions.Add((c, s, "مدني كلي", typeName, day, civilStart));
                civilStart += 15;
            }
        }

        // 2. أسرة
        int familyStart = 6;
        foreach (var c in familyCircuits)
        {
            foreach (var s in c.Sessions.OrderBy(s => s.Id))
            {
                var (typeName, day) = ParseSessionSchedule(s);
                allSessions.Add((c, s, "أسرة", string.IsNullOrEmpty(typeName) ? "أسرة نفس وحبس" : typeName, day, familyStart));
                familyStart += 16;
            }
        }

        // 3. جنح مستأنف
        int crimAppStart = 12;
        foreach (var c in criminalAppealCircuits)
        {
            foreach (var s in c.Sessions.OrderBy(s => s.Id))
            {
                var (typeName, day) = ParseSessionSchedule(s);
                allSessions.Add((c, s, "جنح مستأنف", string.IsNullOrEmpty(typeName) ? "جنح ومخالفات مستأنفة" : typeName, day, crimAppStart));
                crimAppStart += 1;
            }
        }

        // 4. عمالية
        int laborStart = 10;
        foreach (var c in laborCircuits)
        {
            foreach (var s in c.Sessions.OrderBy(s => s.Id))
            {
                var (typeName, day) = ParseSessionSchedule(s);
                allSessions.Add((c, s, "عمالية", string.IsNullOrEmpty(typeName) ? "قضايا عمال" : typeName, day, laborStart));
                laborStart += 27;
            }
        }

        // 5. جزئي (جنح جزئي أولاً، ثم مدني جزئي)
        var partialMisdSessions = new List<(Circuit Circuit, Session Session)>();
        var partialCivSessions = new List<(Circuit Circuit, Session Session)>();
        foreach (var c in partialCircuits)
        {
            foreach (var s in c.Sessions.OrderBy(s => s.Id))
            {
                if (s.Code.Contains("-CIV-") || s.NameAr.Contains("مدني") || s.NameAr.Contains("حكومة") || s.NameAr.Contains("مستعجل")
                    || s.NameAr.Contains("صحة توقيع") || s.NameAr.Contains("توقيع") || (s.Description != null && s.Description.Contains("توقيع")))
                    partialCivSessions.Add((c, s));
                else
                    partialMisdSessions.Add((c, s));
            }
        }

        int misdStart = 12;
        foreach (var (c, s) in partialMisdSessions)
        {
            var (typeName, day) = ParseSessionSchedule(s);
            allSessions.Add((c, s, "جنح جزئي", string.IsNullOrEmpty(typeName) ? "جنح ومخالفات" : typeName, day, misdStart));
            misdStart += 1;
        }

        int civPartStart = 6;
        foreach (var (c, s) in partialCivSessions)
        {
            var (typeName, day) = ParseSessionSchedule(s);
            if (s.NameAr.Contains("صحة توقيع") || s.NameAr.Contains("توقيع") || (s.Description != null && s.Description.Contains("توقيع")))
            {
                typeName = "صحة توقيع";
            }
            allSessions.Add((c, s, "مدني جزئي", string.IsNullOrEmpty(typeName) ? "مدني جزئي" : typeName, day, civPartStart));
            civPartStart += 21;
        }

        // تكليفات القضاة مرتبة بالدوائر
        var allAssignments = circuits
            .SelectMany(c => (c.Versions.FirstOrDefault()?.JudgeAssignments ?? new List<CircuitJudgeAssignment>())
                .Select(ja => (Circuit: c, Assignment: ja)))
            .OrderBy(x => x.Circuit.Id)
            .ThenBy(x => x.Assignment.DisplayOrder)
            .ToList();

        // 4. إنشاء ملف الإكسل بتنسيق التشكيل الموحد الاحترافي
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("التشكيل");
        ws.RightToLeft = true;
        ws.ShowGridLines = true;

        // الرؤوس الكبرى في السطر 2 (Super Headers)
        ws.Row(2).Height = 28;
        ws.Range("A2:E2").Merge().Value = "الدوائر";
        StyleSuperHeader(ws.Range("A2:E2"), "#1E3A8A"); // Navy Blue

        ws.Range("F2:P2").Merge().Value = "الجلسات";
        StyleSuperHeader(ws.Range("F2:P2"), "#B45309"); // Dark Amber

        ws.Range("Q2:Z2").Merge().Value = "القضاة";
        StyleSuperHeader(ws.Range("Q2:Z2"), "#4338CA"); // Indigo

        ws.Range("AB2:AM2").Merge().Value = "الجداول المرجعية وقوائم الاختيار للنظام (System Reference & Lookup Tables)";
        StyleSuperHeader(ws.Range("AB2:AM2"), "#0F766E"); // Teal

        // السطر 4: ترويسات الأعمدة
        ws.Row(4).Height = 26;
        var colHeaders = new (int Col, string Text, string BgColor, string FgColor)[]
        {
            // Table 1: الدوائر
            (1, "رقم الدائرة", "#DBEAFE", "#1E3A8A"),
            (2, "اسم الدائرة", "#DBEAFE", "#1E3A8A"),
            (3, "المحكمة / المأمورية", "#DBEAFE", "#1E3A8A"),
            (4, "نوع الدائرة", "#DBEAFE", "#1E3A8A"),
            (5, "بداية", "#DBEAFE", "#1E3A8A"),

            // Table 2: الجلسات
            (6, "اسم الدائرة (معادلة)", "#FEF3C7", "#92400E"),
            (7, "رقم الدائرة", "#FEF3C7", "#92400E"),
            (8, "التخصص القضائي", "#FEF3C7", "#92400E"),
            (9, "رقم الجلسة", "#FEF3C7", "#92400E"),
            (10, "اسم الجلسة", "#FEF3C7", "#92400E"),
            (11, "نوع الجلسة", "#FEF3C7", "#92400E"),
            (12, "يوم العمل", "#FEF3C7", "#92400E"),
            (13, "بداية الرنج", "#FEF3C7", "#92400E"),
            (14, "المحكمة (معادلة)", "#FEF3C7", "#92400E"),
            (15, "اسم سكرتير الجلسة", "#FEF3C7", "#92400E"),
            (16, "رقم تليفون سكرتير الجلسة", "#FEF3C7", "#92400E"),

            // Table 3: القضاة
            (17, "رقم القاضي", "#E0E7FF", "#3730A3"),
            (18, "اسم القاضي", "#E0E7FF", "#3730A3"),
            (19, "الاقدمية", "#E0E7FF", "#3730A3"),
            (20, "الدرجة", "#E0E7FF", "#3730A3"),
            (21, "رقم الدائرة", "#E0E7FF", "#3730A3"),
            (22, "اسم الدائرة (معادلة)", "#E0E7FF", "#3730A3"),
            (23, "التخصص القضائي", "#E0E7FF", "#3730A3"),
            (24, "العمل بالدائرة", "#E0E7FF", "#3730A3"),
            (25, "الحالة", "#E0E7FF", "#3730A3"),
            (26, "السبب", "#E0E7FF", "#3730A3"),

            // Lookup Tables
            (28, "كود المحكمة", "#CCFBF1", "#115E59"),
            (29, "المحكمة أو المأمورية", "#CCFBF1", "#115E59"),
            (31, "نوع الدائرة", "#CCFBF1", "#115E59"),
            (33, "التخصص القضائي", "#CCFBF1", "#115E59"),
            (35, "نوع الجلسة", "#CCFBF1", "#115E59"),
            (36, "ظهور الجلسة", "#FEF3C7", "#92400E"),
            (37, "يوم العمل", "#CCFBF1", "#115E59"),
            (39, "حالة القاضي", "#CCFBF1", "#115E59")
        };

        foreach (var h in colHeaders)
        {
            var cell = ws.Cell(4, h.Col);
            cell.Value = h.Text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Segoe UI";
            cell.Style.Font.FontSize = 10;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(h.BgColor);
            cell.Style.Font.FontColor = XLColor.FromHtml(h.FgColor);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        }

        // ==================== كتابة الجداول المرجعية الديناميكية ====================
        // 1. المحاكم (AB & AC)
        int courtsEnd = 4 + courtUnits.Count;
        for (int i = 0; i < courtUnits.Count; i++)
        {
            int r = 5 + i;
            ws.Cell(r, 28).Value = courtUnits[i].Code;
            ws.Cell(r, 29).Value = courtUnits[i].NameAr;
            ApplyDataCellStyle(ws.Cell(r, 28), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 29), XLAlignmentHorizontalValues.Right);
        }

        // 2. نوع الدائرة (AE)
        int kindsEnd = 4 + circuitKinds.Count;
        for (int i = 0; i < circuitKinds.Count; i++)
        {
            int r = 5 + i;
            ws.Cell(r, 31).Value = circuitKinds[i];
            ApplyDataCellStyle(ws.Cell(r, 31), XLAlignmentHorizontalValues.Center);
        }

        // 3. التخصص القضائي (AG)
        var disciplineNames = new List<string> { "مدني كلي", "أسرة", "عمالية", "جنح مستأنف", "جنح جزئي", "مدني جزئي" };
        foreach (var d in disciplines)
        {
            if (!disciplineNames.Contains(d.NameAr)) disciplineNames.Add(d.NameAr);
        }
        int discEnd = 4 + disciplineNames.Count;
        for (int i = 0; i < disciplineNames.Count; i++)
        {
            int r = 5 + i;
            ws.Cell(r, 33).Value = disciplineNames[i];
            ApplyDataCellStyle(ws.Cell(r, 33), XLAlignmentHorizontalValues.Center);
        }

        // 4. نوع الجلسة (AI)
        var caseTypeNames = caseTypes.Select(c => c.NameAr.Trim()).Distinct().ToList();
        // التأكد من توفر المسميات الشائعة
        var standardTypes = new[] { 
            "مدني كلي", "مدني مستأنف", "مستأنف مستعجل", "مستأنف أوقاف", "تجاري", "حكومة", "تعويضات", "إيجارات", "أوقاف",
            "كلي مستأنف", "كلي صحة توقيع", "أسرة نفس وحبس", "أسرة مال", "أسرة نفس", "قضايا عمال", "تأمينات اجتماعية", "جنح عمال", 
            "جنح ومخالفات مستأنفة", "جنح العنف ضد المرأة مستأنفة", "جنح المباني مستأنفة", "جنح البيئة مستأنفة", "جنح ماسة بأمن البلاد مستأنفة", 
            "لجنة الإعفاء من الرسوم القضائية", "جنح طفل مستأنف", "جنايات طفل مستأنف", "جنح ومخالفات", "جنح المرور", "قضايا العنف ضد المرأة", 
            "مباني وأملاك الدولة", "جنح البيئة", "مدني جزئي", "مستعجل جزئي", "حكومة جزئي", "صحة توقيع" 
        };
        foreach (var st in standardTypes)
        {
            if (!caseTypeNames.Contains(st)) caseTypeNames.Add(st);
        }
        int typesEnd = 4 + caseTypeNames.Count;
        for (int i = 0; i < caseTypeNames.Count; i++)
        {
            int r = 5 + i;
            ws.Cell(r, 35).Value = caseTypeNames[i];
            ApplyDataCellStyle(ws.Cell(r, 35), XLAlignmentHorizontalValues.Right);
        }

        // 5. يوم العمل (AK)
        int daysEnd = 4 + workDays.Count;
        for (int i = 0; i < workDays.Count; i++)
        {
            int r = 5 + i;
            ws.Cell(r, 37).Value = workDays[i];
            ApplyDataCellStyle(ws.Cell(r, 37), XLAlignmentHorizontalValues.Center);
        }

        // 6. حالة القاضي (AM)
        int statusEnd = 4 + judgeStatuses.Count;
        for (int i = 0; i < judgeStatuses.Count; i++)
        {
            int r = 5 + i;
            ws.Cell(r, 39).Value = judgeStatuses[i];
            ApplyDataCellStyle(ws.Cell(r, 39), XLAlignmentHorizontalValues.Center);
        }

        // ==================== كتابة جدول الدوائر (A:E) ====================
        int circuitsEnd = 4 + circuits.Count;
        for (int i = 0; i < circuits.Count; i++)
        {
            int r = 5 + i;
            var c = circuits[i];
            string kindAr = MapCircuitKindToAr(c.Versions.FirstOrDefault()?.CircuitKind);

            // تحديد المحكمة الجزئية أو المأمورية التابعة التابعة لها الدائرة (استبعاد المحكمة الأم)
            string courtName = "";
            if (c.CourtUnit != null && c.CourtUnit.ParentCourtUnitId != null && c.CourtUnit.Code != "BNISF-COURT" && c.CourtUnit.Code != "FYM-COURT")
            {
                courtName = c.CourtUnit.NameAr;
            }
            else
            {
                var resolved = Formation2026SeedData.ResolveCourt(c.Code, courtMap, rootCourt);
                courtName = (resolved.Code != "BNISF-COURT" && resolved.Code != "FYM-COURT") ? resolved.NameAr : "";
            }

            string startVal = ResolveCircuitStart(c.Id);

            // كتابة اسم الدائرة بالكامل دون اختصار أو اقتطاع
            string circuitNameForExcel = c.NameAr;
            var cKind = c.Versions.FirstOrDefault()?.CircuitKind;
            if (cKind == "Partial" || (int.TryParse(c.Code.Replace("C", ""), out int cNum) && cNum >= 25 && cNum <= 46))
            {
                circuitNameForExcel = Formation2026SeedData.ResolvePartialCircuitName(c.Code, c.NameAr);
            }

            ws.Cell(r, 1).Value = c.Id;
            ws.Cell(r, 2).Value = circuitNameForExcel;
            ws.Cell(r, 3).Value = courtName;
            ws.Cell(r, 4).Value = kindAr;
            ws.Cell(r, 5).Value = startVal;

            ApplyDataCellStyle(ws.Cell(r, 1), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 2), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 3), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 4), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 5), XLAlignmentHorizontalValues.Center);
        }

        // قوائم الاختيار لجدول الدوائر
        var dvCircCourt = ws.Range(5, 3, circuitsEnd, 3).CreateDataValidation();
        dvCircCourt.List($"=$AC$5:$AC${courtsEnd}", true);

        var dvCircKind = ws.Range(5, 4, circuitsEnd, 4).CreateDataValidation();
        dvCircKind.List($"=$AE$5:$AE${kindsEnd}", true);

        // ==================== كتابة جدول الجلسات (F:P) ====================
        int sessionsEnd = 4 + allSessions.Count;
        for (int i = 0; i < allSessions.Count; i++)
        {
            int r = 5 + i;
            var item = allSessions[i];

            // Col F: معادلة اسم الدائرة
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(G{r}, $A$5:$B${circuitsEnd}, 2, FALSE), \"\")";
            ws.Cell(r, 7).Value = item.Circuit.Id;
            ws.Cell(r, 8).Value = item.DisciplineName;
            ws.Cell(r, 9).Value = item.Session.Id;
            ws.Cell(r, 10).Value = item.Session.NameAr;
            ws.Cell(r, 11).Value = item.CaseTypeName;
            ws.Cell(r, 12).Value = item.Day;
            ws.Cell(r, 13).Value = item.StartRange;
            // Col N: معادلة المحكمة
            ws.Cell(r, 14).FormulaA1 = $"IFERROR(VLOOKUP(G{r}, $A$5:$C${circuitsEnd}, 3, FALSE), \"\")";
            ws.Cell(r, 15).Value = item.Session.Employee?.FullNameAr ?? ""; // اسم سكرتير الجلسة
            ws.Cell(r, 16).Value = item.Session.Employee?.PhoneNumber ?? ""; // رقم تليفون سكرتير الجلسة
            // Col AJ (36): ظهور الجلسة
            ws.Cell(r, 36).Value = item.Session.ShowInProgressReport ? "نعم" : "لا";

            ApplyDataCellStyle(ws.Cell(r, 6), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 7), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 8), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 9), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 10), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 11), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 12), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 13), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 14), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 15), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 16), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 36), XLAlignmentHorizontalValues.Center);
        }

        // قوائم الاختيار لجدول الجلسات
        var dvSessDisc = ws.Range(5, 8, sessionsEnd, 8).CreateDataValidation();
        dvSessDisc.List($"=$AG$5:$AG${discEnd}", true);

        var dvSessType = ws.Range(5, 11, sessionsEnd, 11).CreateDataValidation();
        dvSessType.List($"=$AI$5:$AI${typesEnd}", true);

        var dvSessDay = ws.Range(5, 12, sessionsEnd, 12).CreateDataValidation();
        dvSessDay.List($"=$AK$5:$AK${daysEnd}", true);

        // ==================== كتابة جدول القضاة (Q:Z) ====================
        int judgesEnd = 4 + allAssignments.Count;
        for (int i = 0; i < allAssignments.Count; i++)
        {
            int r = 5 + i;
            var item = allAssignments[i];
            var ja = item.Assignment;
            var judge = ja.Judge;
            string kindAr = MapCircuitKindToAr(item.Circuit.Versions.FirstOrDefault()?.CircuitKind);

            ws.Cell(r, 17).Value = judge?.Id ?? 0;
            ws.Cell(r, 18).Value = judge?.FullNameAr ?? "";
            ws.Cell(r, 19).Value = ja.DisplayOrder.ToString();
            ws.Cell(r, 20).Value = "أ";
            ws.Cell(r, 21).Value = item.Circuit.Id;
            // Col V: معادلة اسم الدائرة
            ws.Cell(r, 22).FormulaA1 = $"IFERROR(VLOOKUP(U{r}, $A$5:$B${circuitsEnd}, 2, FALSE), \"\")";
            ws.Cell(r, 23).Value = kindAr;
            ws.Cell(r, 24).Value = ja.JudicialRole?.NameAr ?? "عضو";
            ws.Cell(r, 25).Value = (judge?.IsActive ?? true) ? "يعمل" : "لا يعمل";
            ws.Cell(r, 26).Value = "";

            ApplyDataCellStyle(ws.Cell(r, 17), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 18), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 19), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 20), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 21), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 22), XLAlignmentHorizontalValues.Right);
            ApplyDataCellStyle(ws.Cell(r, 23), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 24), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 25), XLAlignmentHorizontalValues.Center);
            ApplyDataCellStyle(ws.Cell(r, 26), XLAlignmentHorizontalValues.Right);
        }

        // قوائم الاختيار لجدول القضاة
        var dvJudgeDisc = ws.Range(5, 23, judgesEnd, 23).CreateDataValidation();
        dvJudgeDisc.List($"=$AG$5:$AG${discEnd}", true);

        var dvJudgeStatus = ws.Range(5, 25, judgesEnd, 25).CreateDataValidation();
        dvJudgeStatus.List($"=$AM$5:$AM${statusEnd}", true);

        // ==================== أبعاد الأعمدة الاحترافية ====================
        ws.Column(1).Width = 14;   // رقم الدائرة
        ws.Column(2).Width = 34;   // اسم الدائرة
        ws.Column(3).Width = 28;   // المحكمة / المأمورية
        ws.Column(4).Width = 18;   // نوع الدائرة
        ws.Column(5).Width = 12;   // بداية
        ws.Column(6).Width = 34;   // اسم الدائرة (معادلة)
        ws.Column(7).Width = 14;   // رقم الدائرة
        ws.Column(8).Width = 18;   // التخصص القضائي
        ws.Column(9).Width = 14;   // رقم الجلسة
        ws.Column(10).Width = 36;  // اسم الجلسة
        ws.Column(11).Width = 24;  // نوع الجلسة
        ws.Column(12).Width = 14;  // يوم العمل
        ws.Column(13).Width = 14;  // بداية الرنج
        ws.Column(14).Width = 28;  // المحكمة (معادلة)
        ws.Column(15).Width = 24;  // اسم سكرتير الجلسة
        ws.Column(16).Width = 18;  // رقم تليفون سكرتير الجلسة
        ws.Column(17).Width = 14;  // رقم القاضي
        ws.Column(18).Width = 34;  // اسم القاضي
        ws.Column(19).Width = 12;  // الاقدمية
        ws.Column(20).Width = 10;  // الدرجة
        ws.Column(21).Width = 14;  // رقم الدائرة
        ws.Column(22).Width = 34;  // اسم الدائرة (معادلة)
        ws.Column(23).Width = 18;  // التخصص القضائي
        ws.Column(24).Width = 22;  // العمل بالدائرة
        ws.Column(25).Width = 14;  // الحالة
        ws.Column(26).Width = 24;  // السبب
        ws.Column(27).Width = 4;   // فاصل
        ws.Column(28).Width = 18;  // كود المحكمة
        ws.Column(29).Width = 30;  // المحكمة أو المأمورية
        ws.Column(30).Width = 4;   // فاصل
        ws.Column(31).Width = 18;  // نوع الدائرة
        ws.Column(32).Width = 4;   // فاصل
        ws.Column(33).Width = 18;  // التخصص القضائي
        ws.Column(34).Width = 4;   // فاصل
        ws.Column(35).Width = 28;  // نوع الجلسة
        ws.Column(36).Width = 14;  // ظهور الجلسة (AJ)
        ws.Column(37).Width = 14;  // يوم العمل
        ws.Column(38).Width = 4;   // فاصل
        ws.Column(39).Width = 14;  // حالة القاضي

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void StyleSuperHeader(IXLRange range, string bgColorHex)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontName = "Segoe UI";
        range.Style.Font.FontSize = 12;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(bgColorHex);
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml(bgColorHex);
    }

    private static void ApplyDataCellStyle(IXLCell cell, XLAlignmentHorizontalValues alignment)
    {
        cell.Style.Font.FontName = "Segoe UI";
        cell.Style.Font.FontSize = 9.5;
        cell.Style.Alignment.Horizontal = alignment;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
    }

    private static string MapCircuitKindToAr(string? kind)
    {
        return (kind ?? "").Trim().ToLowerInvariant() switch
        {
            "civilfull" => "مدني كلي",
            "family" => "أسرة",
            "criminalappeal" => "جنح مستأنف",
            "labor" => "عمالية",
            "partial" => "جزئي",
            "summary" => "جزئي",
            _ => "مدني كلي"
        };
    }

    private static string ResolveCircuitStart(long circuitId)
    {
        return circuitId switch
        {
            1 => "6",
            2 => "66",
            3 => "126",
            4 => "186",
            5 => "231",
            6 => "336",
            7 => "426",
            8 => "501",
            9 => "6",
            10 => "54",
            11 => "102",
            12 => "150",
            13 => "198",
            14 => "246",
            15 => "294",
            16 => "342",
            17 => "12",
            18 => "20",
            19 => "26",
            20 => "32",
            21 => "39",
            22 => "47",
            23 => "53",
            24 => "59",
            25 => "10",
            26 => "118",
            27 => "د1",
            28 => "د2",
            29 => "د3",
            30 => "د4",
            31 => "د1",
            32 => "د2",
            33 => "د3",
            34 => "د1",
            35 => "د2",
            36 => "د3",
            37 => "د1",
            38 => "د2",
            39 => "د1",
            40 => "د2",
            41 => "د1",
            42 => "د2",
            43 => "د2",
            44 => "د1",
            45 => "د2",
            46 => "د3",
            47 => "د1",
            48 => "د2",
            _ => "0"
        };
    }

    private static (string TypeName, string Day) ParseSessionSchedule(Session s)
    {
        var sv = s.Versions.FirstOrDefault();
        string text = $"{s.Description} {sv?.ScheduleDescription} {s.NameAr}";

        string day = "السبت";
        if (text.Contains("السبت")) day = "السبت";
        else if (text.Contains("الأحد") || text.Contains("الاحد")) day = "الأحد";
        else if (text.Contains("الاثنين") || text.Contains("الأثنين") || text.Contains("الإثنين")) day = "الاثنين";
        else if (text.Contains("الثلاثاء")) day = "الثلاثاء";
        else if (text.Contains("الأربعاء") || text.Contains("الاربعاء")) day = "الأربعاء";
        else if (text.Contains("الخميس")) day = "الخميس";
        else if (text.Contains("الجمعة")) day = "الجمعة";

        // إذا كان الاسم أو التوصيف يحتوي على صحة توقيع، نحدد نوعها مباشرة كصحة توقيع
        if (text.Contains("صحة توقيع") || text.Contains("توقيع"))
        {
            return ("صحة توقيع", day);
        }

        // إذا كانت الجلسة مستعجل
        if (text.Contains("مستعجل"))
        {
            bool isPlen = s.Circuit?.Versions.FirstOrDefault()?.CircuitKind == "CivilFull" || text.Contains("مستأنف");
            return (isPlen ? "مستأنف مستعجل" : "مستعجل جزئي", day);
        }

        // إذا كانت مستأنف أوقاف
        if (text.Contains("أوقاف") && text.Contains("مستأنف"))
        {
            return ("مستأنف أوقاف", day);
        }

        // إذا كانت مدني مستأنف
        if (text.Contains("مدني مستأنف") || (text.Contains("مستأنف") && s.Circuit?.Versions.FirstOrDefault()?.CircuitKind == "CivilFull"))
        {
            return ("مدني مستأنف", day);
        }

        // إذا كانت حكومة جزئي
        if (text.Contains("حكومة") && (s.Circuit?.Versions.FirstOrDefault()?.CircuitKind == "Partial" || s.Circuit?.Versions.FirstOrDefault()?.CircuitKind == "Summary"))
        {
            return ("حكومة جزئي", day);
        }

        // 1. أولوية لاسم نوع الدعوى الحقيقي المرتبط بالجلسة في قاعدة البيانات
        string typeName = sv?.SessionCaseTypes.FirstOrDefault()?.CaseType?.NameAr?.Trim() ?? "";

        // 2. إذا لم يكن مسجلاً أو كان مسمى عاماً مثل "جلسة 1"، نستنتجه من التوصيف
        if (string.IsNullOrWhiteSpace(typeName) || typeName.StartsWith("جلسة ") || typeName.StartsWith("جلسة"))
        {
            if (sv?.ScheduleDescription != null && sv.ScheduleDescription.Contains("-"))
            {
                var parts = sv.ScheduleDescription.Split('-');
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) && !parts[1].Trim().StartsWith("جلسة"))
                    typeName = parts[1].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(typeName) || typeName.StartsWith("جلسة ") || typeName.StartsWith("جلسة"))
        {
            if (text.Contains("أوقاف") || text.Contains("اوقاف")) typeName = "أوقاف";
            else if (text.Contains("حكومة")) typeName = "حكومة";
            else if (text.Contains("تجاري")) typeName = "تجاري";
            else if (text.Contains("إيجارات") || text.Contains("ايجارات")) typeName = "إيجارات";
            else if (text.Contains("تعويضات")) typeName = "تعويضات";
            else if (text.Contains("صحة توقيع")) typeName = "صحة توقيع";
            else if (text.Contains("مستعجل")) typeName = "مستعجل جزئي";
            else if (text.Contains("تأمينات")) typeName = "تأمينات اجتماعية";
            else if (text.Contains("بيئة") || text.Contains("البيئة")) typeName = "جنح البيئة";
            else if (text.Contains("مرور") || text.Contains("المرور")) typeName = "جنح المرور";
            else if (text.Contains("مباني") || text.Contains("البناء")) typeName = "مباني وأملاك الدولة";
            else if (text.Contains("عنف ضد الطفل")) typeName = "جنح مستأنف العنف ضد الطفل";
            else if (text.Contains("عنف") || text.Contains("العنف")) typeName = "قضايا العنف ضد المرأة";
            else if (text.Contains("طفل") || text.Contains("أحداث")) typeName = "جنح طفل مستأنف";
            else if (text.Contains("امن دولة") || text.Contains("أمن البلاد")) typeName = "جنح ماسة بأمن البلاد مستأنفة";
            else if (text.Contains("إرهاب") || text.Contains("ارهاب")) typeName = "جنح مستأنف الإرهاب";
            else if (text.Contains("مخدرات") || text.Contains("التعاطي")) typeName = "مخدرات مستأنف";
            else if (text.Contains("هجرة")) typeName = "هجرة غير شرعية";
            else if (text.Contains("مستأنف") && text.Contains("جنح")) typeName = "جنح ومخالفات مستأنفة";
            else if (text.Contains("جنح ومخالفات")) typeName = "جنح ومخالفات";
            else if (text.Contains("مدني كلي")) typeName = "مدني كلي";
            else if (text.Contains("مدني جزئي") || text.Contains("مدني")) typeName = "مدني جزئي";
            else if (text.Contains("أسرة") || text.Contains("اسرة")) typeName = "أسرة نفس وحبس";
            else if (text.Contains("عمال")) typeName = "قضايا عمال";
        }

        return (typeName, day);
    }
}
