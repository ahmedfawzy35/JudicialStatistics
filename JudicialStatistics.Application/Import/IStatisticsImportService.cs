using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace JudicialStatistics.Application.Import;

public interface IStatisticsImportService
{
    Task<List<ExcelImportTemplateDto>> GetTemplatesAsync();
    Task<ExcelImportTemplateDto?> GetTemplateByIdAsync(long id);
    Task<ExcelImportTemplateDto> SaveTemplateAsync(ExcelImportTemplateDto dto);
    Task<ImportPreviewResultDto> PreviewImportAsync(Stream excelStream, long templateId, long judicialYearId, long periodId);
    Task<ImportCommitResultDto> CommitImportAsync(ImportPreviewResultDto preview, long userId);
}
