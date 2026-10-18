using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using JudicialStatistics.Application.Import;
using JudicialStatistics.Domain.Import;
using JudicialStatistics.Domain.Statistics;
using JudicialStatistics.Infrastructure.Persistence;
using JudicialStatistics.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudicialStatistics.Infrastructure.Services;

public class StatisticsImportService : IStatisticsImportService
{
    private readonly JudicialDbContext _context;
    private readonly ILogger<StatisticsImportService> _logger;

    public StatisticsImportService(JudicialDbContext context, ILogger<StatisticsImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ExcelImportTemplateDto>> GetTemplatesAsync()
    {
        var entities = await _context.ExcelImportTemplates
            .Include(t => t.ColumnMappings)
            .OrderBy(t => t.NameAr)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<ExcelImportTemplateDto?> GetTemplateByIdAsync(long id)
    {
        var entity = await _context.ExcelImportTemplates
            .Include(t => t.ColumnMappings)
            .FirstOrDefaultAsync(t => t.Id == id);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ExcelImportTemplateDto> SaveTemplateAsync(ExcelImportTemplateDto dto)
    {
        ExcelImportTemplate entity;
        if (dto.Id > 0)
        {
            entity = await _context.ExcelImportTemplates
                .Include(t => t.ColumnMappings)
                .FirstOrDefaultAsync(t => t.Id == dto.Id)
                ?? throw new InvalidOperationException($"القالب رقم {dto.Id} غير موجود.");

            entity.NameAr = dto.NameAr;
            entity.DisciplineKind = dto.DisciplineKind;
            entity.StatisticsSheetName = dto.StatisticsSheetName;
            entity.TashkeelSheetName = dto.TashkeelSheetName;
            entity.IsDefault = dto.IsDefault;

            entity.TashkeelCircuitStartRow = dto.TashkeelCircuitStartRow;
            entity.TashkeelCircuitNoCol = dto.TashkeelCircuitNoCol;
            entity.TashkeelCircuitNameCol = dto.TashkeelCircuitNameCol;

            entity.TashkeelSessionStartRow = dto.TashkeelSessionStartRow;
            entity.TashkeelSessionCircuitNoCol = dto.TashkeelSessionCircuitNoCol;
            entity.TashkeelSessionNoCol = dto.TashkeelSessionNoCol;
            entity.TashkeelSessionNameCol = dto.TashkeelSessionNameCol;
            entity.TashkeelSessionTypeCol = dto.TashkeelSessionTypeCol;
            entity.TashkeelSessionDayCol = dto.TashkeelSessionDayCol;
            entity.TashkeelSessionStartBlockCol = dto.TashkeelSessionStartBlockCol;
            entity.TashkeelSessionCourtCol = dto.TashkeelSessionCourtCol;

            entity.TashkeelJudgeStartRow = dto.TashkeelJudgeStartRow;
            entity.TashkeelJudgeNoCol = dto.TashkeelJudgeNoCol;
            entity.TashkeelJudgeNameCol = dto.TashkeelJudgeNameCol;
            entity.TashkeelJudgeCircuitNoCol = dto.TashkeelJudgeCircuitNoCol;
            entity.TashkeelJudgeRoleCol = dto.TashkeelJudgeRoleCol;
            entity.TashkeelJudgeStatusCol = dto.TashkeelJudgeStatusCol;

            entity.BlockTotalRows = dto.BlockTotalRows;
            entity.YearBandsCount = dto.YearBandsCount;
            entity.PrimaryDataRowOffset = dto.PrimaryDataRowOffset;
            entity.SecondaryJudgesRowOffset = dto.SecondaryJudgesRowOffset;
            entity.Judge1HeaderRowOffset = dto.Judge1HeaderRowOffset;
            entity.Judge3HeaderRowOffset = dto.Judge3HeaderRowOffset;
            entity.Judge1HeaderCol = dto.Judge1HeaderCol;
            entity.Judge2HeaderCol = dto.Judge2HeaderCol;
            entity.Judge5HeaderCol = dto.Judge5HeaderCol;

            _context.ExcelImportColumnMappings.RemoveRange(entity.ColumnMappings);
            entity.ColumnMappings.Clear();
        }
        else
        {
            entity = new ExcelImportTemplate
            {
                NameAr = dto.NameAr,
                DisciplineKind = dto.DisciplineKind,
                StatisticsSheetName = dto.StatisticsSheetName,
                TashkeelSheetName = dto.TashkeelSheetName,
                IsDefault = dto.IsDefault,

                TashkeelCircuitStartRow = dto.TashkeelCircuitStartRow,
                TashkeelCircuitNoCol = dto.TashkeelCircuitNoCol,
                TashkeelCircuitNameCol = dto.TashkeelCircuitNameCol,

                TashkeelSessionStartRow = dto.TashkeelSessionStartRow,
                TashkeelSessionCircuitNoCol = dto.TashkeelSessionCircuitNoCol,
                TashkeelSessionNoCol = dto.TashkeelSessionNoCol,
                TashkeelSessionNameCol = dto.TashkeelSessionNameCol,
                TashkeelSessionTypeCol = dto.TashkeelSessionTypeCol,
                TashkeelSessionDayCol = dto.TashkeelSessionDayCol,
                TashkeelSessionStartBlockCol = dto.TashkeelSessionStartBlockCol,
                TashkeelSessionCourtCol = dto.TashkeelSessionCourtCol,

                TashkeelJudgeStartRow = dto.TashkeelJudgeStartRow,
                TashkeelJudgeNoCol = dto.TashkeelJudgeNoCol,
                TashkeelJudgeNameCol = dto.TashkeelJudgeNameCol,
                TashkeelJudgeCircuitNoCol = dto.TashkeelJudgeCircuitNoCol,
                TashkeelJudgeRoleCol = dto.TashkeelJudgeRoleCol,
                TashkeelJudgeStatusCol = dto.TashkeelJudgeStatusCol,

                BlockTotalRows = dto.BlockTotalRows,
                YearBandsCount = dto.YearBandsCount,
                PrimaryDataRowOffset = dto.PrimaryDataRowOffset,
                SecondaryJudgesRowOffset = dto.SecondaryJudgesRowOffset,
                Judge1HeaderRowOffset = dto.Judge1HeaderRowOffset,
                Judge3HeaderRowOffset = dto.Judge3HeaderRowOffset,
                Judge1HeaderCol = dto.Judge1HeaderCol,
                Judge2HeaderCol = dto.Judge2HeaderCol,
                Judge5HeaderCol = dto.Judge5HeaderCol
            };
            _context.ExcelImportTemplates.Add(entity);
        }

        foreach (var m in dto.ColumnMappings)
        {
            entity.ColumnMappings.Add(new ExcelImportColumnMapping
            {
                Section = m.Section,
                ColumnLetter = m.ColumnLetter.ToUpperInvariant(),
                TargetItemCode = m.TargetItemCode,
                TargetItemNameAr = m.TargetItemNameAr,
                JudgeSlotIndex = m.JudgeSlotIndex,
                TargetMonthOffset = m.TargetMonthOffset,
                DisplayOrder = m.DisplayOrder
            });
        }

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ImportPreviewResultDto> PreviewImportAsync(Stream excelStream, long templateId, long judicialYearId, long periodId)
    {
        var template = await _context.ExcelImportTemplates
            .Include(t => t.ColumnMappings)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new InvalidOperationException("لم يتم العثور على قالب الاستيراد المحدد.");

        var period = await _context.StatisticalPeriods
            .Include(p => p.JudicialYear)
            .FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new InvalidOperationException("لم يتم العثور على الفترة الإحصائية المحددة.");

        var allPeriods = await _context.StatisticalPeriods
            .Where(p => p.JudicialYearId == judicialYearId)
            .OrderBy(p => p.MonthStartDate)
            .ToListAsync();

        var futurePeriods = allPeriods
            .Where(p => p.MonthStartDate > period.MonthStartDate)
            .OrderBy(p => p.MonthStartDate)
            .ToList();

        var result = new ImportPreviewResultDto
        {
            TemplateId = template.Id,
            TemplateName = template.NameAr,
            DisciplineKind = template.DisciplineKind,
            JudicialYearId = judicialYearId,
            StatisticalPeriodId = periodId,
            PeriodName = $"{period.MonthStartDate:MMMM yyyy}"
        };

        using var workbook = new XLWorkbook(excelStream);

        // 1. العثور على ورقة التشكيل
        var wsTashkeel = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(template.TashkeelSheetName.Trim(), StringComparison.OrdinalIgnoreCase))
                         ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("التشكيل"));

        if (wsTashkeel == null)
        {
            result.Errors.Add($"لم يتم العثور على ورقة التشكيل '{template.TashkeelSheetName}' داخل ملف الإكسل.");
            return result;
        }

        // 2. العثور على ورقة الإحصائية
        var wsStats = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(template.StatisticsSheetName.Trim(), StringComparison.OrdinalIgnoreCase))
                      ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains(template.StatisticsSheetName.Trim()));

        if (wsStats == null)
        {
            result.Errors.Add($"لم يتم العثور على ورقة الإحصائية '{template.StatisticsSheetName}' داخل ملف الإكسل.");
            return result;
        }

        // 3. تحميل جلسات وقضاة النظام للعام القضائي
        var circuits = await _context.Circuits
            .Include(c => c.CourtUnit)
            .Include(c => c.Versions)
                .ThenInclude(v => v.JudgeAssignments)
                    .ThenInclude(ja => ja.Judge)
            .Include(c => c.Sessions)
                .ThenInclude(s => s.Versions)
            .Where(c => c.JudicialYearId == judicialYearId && !c.IsClosed)
            .ToListAsync();

        var allSessions = circuits.SelectMany(c => c.Sessions).ToList();

        // تحميل سياسة شرائح سنوات القيد للتخصص
        var caseYearPolicies = await _context.CaseYearPolicies
            .Include(p => p.Bands)
            .Where(p => p.JudicialYearId == judicialYearId)
            .ToListAsync();

        var defaultPolicy = caseYearPolicies.FirstOrDefault(p => p.Bands.Count == template.YearBandsCount)
                            ?? caseYearPolicies.FirstOrDefault();

        var bands = defaultPolicy?.Bands.OrderBy(b => b.SortOrder).ToList() ?? [];

        // 4. قراءة الجلسات والقضاة من ورقة التشكيل
        var parsedSessions = ParseTashkeelSessions(wsTashkeel, template);
        var parsedJudges = ParseTashkeelJudges(wsTashkeel, template);

        result.TotalSessionsFound = parsedSessions.Count;

        // خريطة أعمدة الاستيراد
        var incomingCols = template.ColumnMappings.Where(m => m.Section == "Incoming").ToList();
        var prodCols = template.ColumnMappings.Where(m => m.Section == "Production").ToList();
        var judgeCols = template.ColumnMappings.Where(m => m.Section == "JudgeJudgment").ToList();
        var postCols = template.ColumnMappings.Where(m => m.Section == "Postponement").ToList();

        foreach (var ps in parsedSessions)
        {
            var previewSession = new ImportSessionPreviewDto
            {
                ExcelSessionNo = ps.SessionNo,
                ExcelSessionName = ps.SessionName,
                ExcelCircuitName = ps.CircuitName,
                BlockStartRow = ps.StartRow
            };

            // مطابقة الجلسة بالنظام
            var matchedSession = MatchSession(ps, allSessions, circuits);
            if (matchedSession != null)
            {
                var sv = matchedSession.Versions.FirstOrDefault();
                previewSession.MatchedSessionId = matchedSession.Id;
                previewSession.MatchedSessionVersionId = sv?.Id;
                previewSession.MatchedSessionCode = matchedSession.Code;
                previewSession.MatchedSessionName = matchedSession.NameAr;
                previewSession.MatchStatus = "MatchedExact";
                result.MatchedSessionsCount++;
            }
            else
            {
                previewSession.MatchStatus = "NotFound";
                previewSession.ValidationMessages.Add($"تعذر مطابقة الجلسة '{ps.SessionName}' مع جلسات النظام.");
                result.UnmatchedSessionsCount++;
                result.Sessions.Add(previewSession);
                continue;
            }

            var sessionCircuit = circuits.FirstOrDefault(c => c.Sessions.Any(s => s.Id == matchedSession.Id));
            var cv = sessionCircuit?.Versions.FirstOrDefault();
            var assignedJudges = cv?.JudgeAssignments.OrderBy(ja => ja.DisplayOrder).ToList() ?? [];

            // مطابقة القضاة في البلوك
            // قراءة أرقام القضاة من الخلايا الترويسية في شيت الإحصائية
            int rJudgeTop = ps.StartRow + template.Judge1HeaderRowOffset;
            int rJudgeBottom = ps.StartRow + template.Judge3HeaderRowOffset;

            string j1No = GetCellText(wsStats, rJudgeTop, template.Judge1HeaderCol);
            string j2No = GetCellText(wsStats, rJudgeTop, template.Judge2HeaderCol);
            string j5No = GetCellText(wsStats, rJudgeTop, template.Judge5HeaderCol);

            string j3No = GetCellText(wsStats, rJudgeBottom, template.Judge1HeaderCol);
            string j4No = GetCellText(wsStats, rJudgeBottom, template.Judge2HeaderCol);
            string j6No = GetCellText(wsStats, rJudgeBottom, template.Judge5HeaderCol);

            var judgeSlots = new (int Slot, string ExcelNo, int JudgeOrderIndex)[]
            {
                (1, j1No, 1),
                (2, j2No, 2),
                (1, j3No, 3),
                (2, j4No, 4),
                (3, j5No, 5),
                (3, j6No, 6)
            };

            var resolvedJudgesBySlot = new Dictionary<(int Slot, int SubIndex), Domain.Organization.Judge>();

            for (int slotIdx = 0; slotIdx < judgeSlots.Length; slotIdx++)
            {
                var js = judgeSlots[slotIdx];
                if (string.IsNullOrWhiteSpace(js.ExcelNo) || js.ExcelNo == "0") continue;

                var jMatchDto = new ImportJudgeMatchDto
                {
                    ExcelJudgeNo = js.ExcelNo,
                    SlotIndex = js.Slot
                };

                // البحث عن اسم القاضي في شيت التشكيل
                var tashkeelJudge = parsedJudges.FirstOrDefault(pj => pj.JudgeNo == js.ExcelNo);
                if (!string.IsNullOrEmpty(tashkeelJudge.JudgeName))
                {
                    jMatchDto.ExcelJudgeName = tashkeelJudge.JudgeName;
                }

                // المطابقة مع قضاة الدائرة المسندين بالنظام
                var matchedJudge = MatchJudge(js.ExcelNo, tashkeelJudge.JudgeName, assignedJudges);
                if (matchedJudge != null)
                {
                    jMatchDto.MatchedJudgeId = matchedJudge.Id;
                    jMatchDto.MatchedJudgeName = matchedJudge.FullNameAr;
                    jMatchDto.IsMatched = true;

                    // تخزين القاضي للاستخدام في توزيع الأحكام
                    resolvedJudgesBySlot[(js.Slot, js.JudgeOrderIndex)] = matchedJudge;
                }

                previewSession.JudgesMatches.Add(jMatchDto);
            }

            // قراءة صفوف الشرائح السنوية (المقدم، الإنتاج، المؤجلات، القضاة)
            int totalInc = 0;
            int totalProd = 0;
            int totalPost = 0;
            int totalFinal = 0;

            int bandsCount = Math.Min(template.YearBandsCount, bands.Count);

            for (int bIdx = 0; bIdx < bandsCount; bIdx++)
            {
                var band = bands[bIdx];
                int rData = ps.StartRow + template.PrimaryDataRowOffset + bIdx;

                // 1. المقدم
                foreach (var col in incomingCols)
                {
                    int val = GetCellInt(wsStats, rData, col.ColumnLetter);
                    if (val > 0)
                    {
                        previewSession.IncomingRows.Add(new ImportIncomingRowDto
                        {
                            BandId = band.Id,
                            BandLabel = band.LabelAr,
                            IncomingCode = col.TargetItemCode,
                            Count = val
                        });

                        if (col.TargetItemCode != "YEAR" && col.TargetItemCode != "SHORT_MONTH")
                        {
                            totalInc += val;
                        }
                    }
                }

                // 2. الإنتاج
                foreach (var col in prodCols)
                {
                    int val = GetCellInt(wsStats, rData, col.ColumnLetter);
                    if (val > 0)
                    {
                        previewSession.ProductionRows.Add(new ImportProductionRowDto
                        {
                            BandId = band.Id,
                            BandLabel = band.LabelAr,
                            ProductionCode = col.TargetItemCode,
                            Count = val
                        });

                        if (col.TargetItemCode == "TOTAL_FINAL")
                        {
                            totalFinal += val;
                        }
                        if (col.TargetItemCode != "TOTAL_FINAL" && col.TargetItemCode != "TOTAL_ADJUD" && col.TargetItemCode != "DEF_TOTAL")
                        {
                            totalProd += val;
                        }
                    }
                }

                // 3. أحكام القضاة الأول والثاني والخامس (صفوف الرنج الأول)
                foreach (var col in judgeCols)
                {
                    int val = GetCellInt(wsStats, rData, col.ColumnLetter);
                    if (val > 0)
                    {
                        int judgeOrder = col.JudgeSlotIndex == 1 ? 1 : (col.JudgeSlotIndex == 2 ? 2 : 5);
                        if (resolvedJudgesBySlot.TryGetValue((col.JudgeSlotIndex ?? 1, judgeOrder), out var targetJudge))
                        {
                            previewSession.JudgeJudgments.Add(new ImportJudgeJudgmentDto
                            {
                                JudgeId = targetJudge.Id,
                                BandId = band.Id,
                                JudgmentKind = col.TargetItemCode,
                                Count = val
                            });
                        }
                    }
                }

                // 4. المؤجلات على شهور
                foreach (var col in postCols)
                {
                    int val = GetCellInt(wsStats, rData, col.ColumnLetter);
                    if (val > 0)
                    {
                        int monthOffset = col.TargetMonthOffset ?? 1;
                        var targetPeriod = futurePeriods.Count >= monthOffset
                            ? futurePeriods[monthOffset - 1]
                            : period;

                        previewSession.Postponements.Add(new ImportPostponementDto
                        {
                            BandId = band.Id,
                            PostponementKind = col.TargetItemCode,
                            TargetPeriodId = targetPeriod.Id,
                            Count = val
                        });
                        totalPost += val;
                    }
                }
            }

            // قراءة أحكام القضاة الثانويين (القاضي 3 و 4 و 6) إن وجدت صفوفهم
            if (template.SecondaryJudgesRowOffset > 0)
            {
                for (int bIdx = 0; bIdx < bandsCount; bIdx++)
                {
                    var band = bands[bIdx];
                    int rSecondary = ps.StartRow + template.SecondaryJudgesRowOffset + bIdx;

                    foreach (var col in judgeCols)
                    {
                        int val = GetCellInt(wsStats, rSecondary, col.ColumnLetter);
                        if (val > 0)
                        {
                            int judgeOrder = col.JudgeSlotIndex == 1 ? 3 : (col.JudgeSlotIndex == 2 ? 4 : 6);
                            if (resolvedJudgesBySlot.TryGetValue((col.JudgeSlotIndex ?? 1, judgeOrder), out var targetJudge))
                            {
                                previewSession.JudgeJudgments.Add(new ImportJudgeJudgmentDto
                                {
                                    JudgeId = targetJudge.Id,
                                    BandId = band.Id,
                                    JudgmentKind = col.TargetItemCode,
                                    Count = val
                                });
                            }
                        }
                    }
                }
            }

            previewSession.TotalIncoming = totalInc;
            previewSession.TotalProduction = totalProd;
            previewSession.TotalPostponed = totalPost;
            previewSession.TotalFinalJudgments = totalFinal;

            // التحقق من توازن الجلسة
            // في النموذج العام: المقدم = الإنتاج + المؤجل (أو قريب منه)
            previewSession.BalanceDifference = totalInc - (totalFinal + totalPost);
            previewSession.IsBalanced = previewSession.BalanceDifference == 0;

            if (previewSession.IsBalanced)
            {
                result.BalancedSessionsCount++;
            }
            else
            {
                result.UnbalancedSessionsCount++;
                if (totalInc > 0 || totalFinal > 0)
                {
                    previewSession.ValidationMessages.Add($"فارق توازن الجلسة: {previewSession.BalanceDifference} قضية.");
                }
            }

            result.Sessions.Add(previewSession);
        }

        return result;
    }

    public async Task<ImportCommitResultDto> CommitImportAsync(ImportPreviewResultDto preview, long userId)
    {
        var commitResult = new ImportCommitResultDto();

        if (preview.Sessions.Count == 0)
        {
            commitResult.Message = "لا توجد جلسات للاستيراد.";
            return commitResult;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ruleSetVersion = await _context.RuleSetVersions
                .OrderByDescending(r => r.VersionNo)
                .FirstOrDefaultAsync() ?? throw new InvalidOperationException("لا توجد نسخة قواعد معتمدة.");

            var defaultPolicy = await _context.CaseYearPolicies
                .Include(p => p.Bands)
                .FirstOrDefaultAsync(p => p.JudicialYearId == preview.JudicialYearId)
                ?? await _context.CaseYearPolicies.FirstAsync();

            var incomingTypes = await _context.IncomingTypes.ToListAsync();
            var prodTypes = await _context.ProductionItemTypes.ToListAsync();

            int importedCount = 0;
            int updatedHeaders = 0;

            foreach (var s in preview.Sessions)
            {
                if (!s.MatchedSessionVersionId.HasValue) continue;

                long svId = s.MatchedSessionVersionId.Value;

                var header = await _context.StatisticsHeaders
                    .Include(h => h.IncomingRows)
                    .Include(h => h.ProductionRows)
                    .Include(h => h.JudgeJudgments)
                    .Include(h => h.Postponements)
                    .FirstOrDefaultAsync(h => h.SessionVersionId == svId && h.StatisticalPeriodId == preview.StatisticalPeriodId);

                if (header != null)
                {
                    if (header.IncomingRows.Count > 0) _context.IncomingRows.RemoveRange(header.IncomingRows);
                    if (header.ProductionRows.Count > 0) _context.ProductionRows.RemoveRange(header.ProductionRows);
                    if (header.JudgeJudgments.Count > 0) _context.JudgeJudgments.RemoveRange(header.JudgeJudgments);
                    if (header.Postponements.Count > 0) _context.Postponements.RemoveRange(header.Postponements);

                    updatedHeaders++;
                }
                else
                {
                    header = new StatisticsHeader
                    {
                        SessionVersionId = svId,
                        StatisticalPeriodId = preview.StatisticalPeriodId,
                        RuleSetVersionId = ruleSetVersion.Id,
                        CaseYearPolicyId = defaultPolicy.Id,
                        Status = "Draft",
                        CreatedByUserId = userId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.StatisticsHeaders.Add(header);
                    importedCount++;
                }

                // إضافة أسطر المقدم
                foreach (var inc in s.IncomingRows)
                {
                    var it = incomingTypes.FirstOrDefault(x => x.Code == inc.IncomingCode)
                             ?? incomingTypes.FirstOrDefault(x => x.Code == "NEW")
                             ?? incomingTypes.First();

                    header.IncomingRows.Add(new IncomingRow
                    {
                        CaseTypeId = 1, // Default or resolved
                        CaseYearBandId = inc.BandId,
                        IncomingTypeId = it.Id,
                        Count = inc.Count
                    });
                }

                // إضافة أسطر الإنتاج
                foreach (var pr in s.ProductionRows)
                {
                    var pt = prodTypes.FirstOrDefault(x => x.Code == pr.ProductionCode)
                             ?? prodTypes.FirstOrDefault(x => x.Code == "PROD_STRIKE")
                             ?? prodTypes.First();

                    header.ProductionRows.Add(new ProductionRow
                    {
                        CaseTypeId = 1,
                        CaseYearBandId = pr.BandId,
                        ProductionItemTypeId = pt.Id,
                        Count = pr.Count
                    });
                }

                // إضافة أحكام القضاة
                foreach (var jj in s.JudgeJudgments)
                {
                    header.JudgeJudgments.Add(new JudgeJudgment
                    {
                        JudgeId = jj.JudgeId,
                        CaseYearBandId = jj.BandId,
                        JudgmentKind = jj.JudgmentKind,
                        Count = jj.Count
                    });
                }

                // إضافة المؤجلات
                foreach (var post in s.Postponements)
                {
                    header.Postponements.Add(new Postponement
                    {
                        CaseYearBandId = post.BandId,
                        PostponementKind = post.PostponementKind,
                        TargetPeriodId = post.TargetPeriodId,
                        Count = post.Count
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            commitResult.Success = true;
            commitResult.ImportedSessionsCount = importedCount;
            commitResult.UpdatedHeadersCount = updatedHeaders;
            commitResult.Message = $"تم استيراد واعتماد بيانات {importedCount + updatedHeaders} جلسة بنجاح (جديدة: {importedCount}، محدثة: {updatedHeaders}).";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "فشل حفظ بيانات استيراد الإحصائيات.");
            commitResult.Success = false;
            commitResult.Message = $"فشل حفظ البيانات: {ex.Message}";
            commitResult.Errors.Add(ex.Message);
        }

        return commitResult;
    }

    private static List<(string SessionNo, string SessionName, string CircuitName, int StartRow)> ParseTashkeelSessions(IXLWorksheet ws, ExcelImportTemplate t)
    {
        var list = new List<(string SessionNo, string SessionName, string CircuitName, int StartRow)>();

        int r = t.TashkeelSessionStartRow;
        int emptyCount = 0;

        while (emptyCount < 5 && r <= 300)
        {
            string sNo = GetCellText(ws, r, t.TashkeelSessionNoCol);
            string sName = GetCellText(ws, r, t.TashkeelSessionNameCol);
            string cName = GetCellText(ws, r, t.TashkeelCircuitNameCol);
            int startRow = GetCellInt(ws, r, t.TashkeelSessionStartBlockCol);

            if (!string.IsNullOrWhiteSpace(sNo) && int.TryParse(sNo, out _) && startRow > 0)
            {
                list.Add((sNo.Trim(), sName.Trim(), cName.Trim(), startRow));
                emptyCount = 0;
            }
            else
            {
                emptyCount++;
            }
            r++;
        }

        return list;
    }

    private static List<(string JudgeNo, string JudgeName, string CircuitNo, string Role)> ParseTashkeelJudges(IXLWorksheet ws, ExcelImportTemplate t)
    {
        var list = new List<(string JudgeNo, string JudgeName, string CircuitNo, string Role)>();

        int r = t.TashkeelJudgeStartRow;
        int emptyCount = 0;

        while (emptyCount < 8 && r <= 300)
        {
            string jNo = GetCellText(ws, r, t.TashkeelJudgeNoCol);
            string jName = GetCellText(ws, r, t.TashkeelJudgeNameCol);
            string cNo = GetCellText(ws, r, t.TashkeelJudgeCircuitNoCol);
            string role = GetCellText(ws, r, t.TashkeelJudgeRoleCol);

            if (!string.IsNullOrWhiteSpace(jName) && jName != "اسم القاضي" && !string.IsNullOrWhiteSpace(jNo))
            {
                list.Add((jNo.Trim(), jName.Trim(), cNo.Trim(), role.Trim()));
                emptyCount = 0;
            }
            else
            {
                emptyCount++;
            }
            r++;
        }

        return list;
    }

    private static Domain.Organization.Session? MatchSession(
        (string SessionNo, string SessionName, string CircuitName, int StartRow) ps,
        List<Domain.Organization.Session> allSessions,
        List<Domain.Organization.Circuit> circuits)
    {
        // 1. البحث المباشر بـ ID الجلسة في النظام
        if (long.TryParse(ps.SessionNo, out long sId))
        {
            var matchById = allSessions.FirstOrDefault(s => s.Id == sId);
            if (matchById != null) return matchById;
        }

        // 2. البحث برقم الجلسة وكودها
        int.TryParse(ps.SessionNo, out int sNum);
        var matchByCode = allSessions.FirstOrDefault(s => s.Code.EndsWith($"{sNum:D2}") || s.Code.EndsWith($"-{sNum}"));
        if (matchByCode != null) return matchByCode;

        // 3. البحث بالاسم
        var matchByName = allSessions.FirstOrDefault(s => s.NameAr.Trim().Equals(ps.SessionName, StringComparison.OrdinalIgnoreCase));
        if (matchByName != null) return matchByName;

        // 3. البحث بالاسم الجزئي
        var matchPartial = allSessions.FirstOrDefault(s => ps.SessionName.Contains(s.NameAr.Trim()) || s.NameAr.Contains(ps.SessionName));
        return matchPartial;
    }

    private static Domain.Organization.Judge? MatchJudge(string excelNo, string? excelName, List<Domain.Organization.CircuitJudgeAssignment> assignedJudges)
    {
        if (assignedJudges.Count == 0) return null;

        // 1. مطابقة بالاسم إن وجد
        if (!string.IsNullOrWhiteSpace(excelName))
        {
            var matchName = assignedJudges.FirstOrDefault(ja => ja.Judge != null && ja.Judge.FullNameAr.Trim().Equals(excelName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchName?.Judge != null) return matchName.Judge;

            var matchFuzzy = assignedJudges.FirstOrDefault(ja => ja.Judge != null && (ja.Judge.FullNameAr.Contains(excelName.Trim()) || excelName.Trim().Contains(ja.Judge.FullNameAr)));
            if (matchFuzzy?.Judge != null) return matchFuzzy.Judge;
        }

        // 2. مطابقة بالـ ID إن كان رقماً
        if (long.TryParse(excelNo, out long jId))
        {
            var matchId = assignedJudges.FirstOrDefault(ja => ja.JudgeId == jId);
            if (matchId?.Judge != null) return matchId.Judge;
        }

        // 3. افتراضياً بالقاضي في نفس الترتيب
        if (int.TryParse(excelNo, out int idx) && idx >= 1 && idx <= assignedJudges.Count)
        {
            return assignedJudges[idx - 1].Judge;
        }

        return assignedJudges.FirstOrDefault()?.Judge;
    }

    private static string GetCellText(IXLWorksheet ws, int row, string colLetter)
    {
        if (string.IsNullOrWhiteSpace(colLetter)) return "";
        try
        {
            return ws.Cell($"{colLetter}{row}").GetString().Trim();
        }
        catch
        {
            return "";
        }
    }

    private static int GetCellInt(IXLWorksheet ws, int row, string colLetter)
    {
        if (string.IsNullOrWhiteSpace(colLetter)) return 0;
        try
        {
            var cell = ws.Cell($"{colLetter}{row}");
            if (cell.TryGetValue<int>(out int val)) return val;
            var str = cell.GetString().Trim();
            if (int.TryParse(str, out int parsed)) return parsed;
            if (double.TryParse(str, out double dVal)) return (int)Math.Round(dVal);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static ExcelImportTemplateDto MapToDto(ExcelImportTemplate entity)
    {
        return new ExcelImportTemplateDto
        {
            Id = entity.Id,
            NameAr = entity.NameAr,
            DisciplineKind = entity.DisciplineKind,
            StatisticsSheetName = entity.StatisticsSheetName,
            TashkeelSheetName = entity.TashkeelSheetName,
            IsDefault = entity.IsDefault,

            TashkeelCircuitStartRow = entity.TashkeelCircuitStartRow,
            TashkeelCircuitNoCol = entity.TashkeelCircuitNoCol,
            TashkeelCircuitNameCol = entity.TashkeelCircuitNameCol,

            TashkeelSessionStartRow = entity.TashkeelSessionStartRow,
            TashkeelSessionCircuitNoCol = entity.TashkeelSessionCircuitNoCol,
            TashkeelSessionNoCol = entity.TashkeelSessionNoCol,
            TashkeelSessionNameCol = entity.TashkeelSessionNameCol,
            TashkeelSessionTypeCol = entity.TashkeelSessionTypeCol,
            TashkeelSessionDayCol = entity.TashkeelSessionDayCol,
            TashkeelSessionStartBlockCol = entity.TashkeelSessionStartBlockCol,
            TashkeelSessionCourtCol = entity.TashkeelSessionCourtCol,

            TashkeelJudgeStartRow = entity.TashkeelJudgeStartRow,
            TashkeelJudgeNoCol = entity.TashkeelJudgeNoCol,
            TashkeelJudgeNameCol = entity.TashkeelJudgeNameCol,
            TashkeelJudgeCircuitNoCol = entity.TashkeelJudgeCircuitNoCol,
            TashkeelJudgeRoleCol = entity.TashkeelJudgeRoleCol,
            TashkeelJudgeStatusCol = entity.TashkeelJudgeStatusCol,

            BlockTotalRows = entity.BlockTotalRows,
            YearBandsCount = entity.YearBandsCount,
            PrimaryDataRowOffset = entity.PrimaryDataRowOffset,
            SecondaryJudgesRowOffset = entity.SecondaryJudgesRowOffset,
            Judge1HeaderRowOffset = entity.Judge1HeaderRowOffset,
            Judge3HeaderRowOffset = entity.Judge3HeaderRowOffset,
            Judge1HeaderCol = entity.Judge1HeaderCol,
            Judge2HeaderCol = entity.Judge2HeaderCol,
            Judge5HeaderCol = entity.Judge5HeaderCol,

            ColumnMappings = entity.ColumnMappings
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new ExcelImportColumnMappingDto
                {
                    Id = m.Id,
                    ExcelImportTemplateId = m.ExcelImportTemplateId,
                    Section = m.Section,
                    ColumnLetter = m.ColumnLetter,
                    TargetItemCode = m.TargetItemCode,
                    TargetItemNameAr = m.TargetItemNameAr,
                    JudgeSlotIndex = m.JudgeSlotIndex,
                    TargetMonthOffset = m.TargetMonthOffset,
                    DisplayOrder = m.DisplayOrder
                }).ToList()
        };
    }
}
