using SuledFunctions.Models;

namespace SuledFunctions.Services;

/// <summary>
/// Interface for parsing tournament Excel files
/// </summary>
public interface IExcelParserService
{
    /// <summary>
    /// Parse tournament data from Excel stream
    /// </summary>
    /// <param name="excelStream">Stream containing Excel file data</param>
    /// <param name="fileName">Name of the Excel file</param>
    /// <returns>Parsed tournament with games</returns>
    Task<Tournament> ParseTournamentAsync(Stream excelStream, string fileName);
}
