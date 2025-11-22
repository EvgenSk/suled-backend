using OfficeOpenXml;
using SuledFunctions.Models;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Interface for extracting tournament metadata from filename and Excel cells
/// </summary>
public interface IExcelMetadataExtractor
{
    /// <summary>
    /// Extract metadata from filename pattern: tournament_2025-11-15_Chicago_DivisionA.xlsx
    /// </summary>
    void ExtractFromFileName(Tournament tournament, string fileName);

    /// <summary>
    /// Extract metadata from Excel worksheet cells
    /// </summary>
    void ExtractFromExcel(Tournament tournament, ExcelWorksheet worksheet);

    /// <summary>
    /// Determine tournament status based on dates
    /// </summary>
    void DetermineStatus(Tournament tournament);
}
