using SuledFunctions.Models;

namespace SuledFunctions.Services.Excel.Interfaces;

/// <summary>
/// Interface for extracting tournament metadata from filename and cell rows
/// </summary>
public interface IExcelMetadataExtractor
{
    /// <summary>
    /// Extract metadata from filename pattern: tournament_2025-11-15_Chicago_DivisionA.xlsx
    /// </summary>
    void ExtractFromFileName(Tournament tournament, string fileName);

    /// <summary>
    /// Extract metadata from rows of cell values (0-indexed)
    /// </summary>
    void ExtractFromExcel(Tournament tournament, string[][] rows);

    /// <summary>
    /// Determine tournament status based on dates
    /// </summary>
    void DetermineStatus(Tournament tournament);
}
