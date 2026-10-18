using System.Threading.Tasks;

namespace JudicialStatistics.Application.Import;

public interface IFormationExportService
{
    Task<byte[]> ExportFormationWorkbookAsync(long judicialYearId, long? periodId = null);
}
