using OfficeOpenXml;
using SuledFunctions.Models;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Interface for parsing game data from Excel worksheets
/// </summary>
public interface IExcelGameParser
{
    /// <summary>
    /// Parse all games from the worksheet
    /// </summary>
    List<Game> ParseGames(ExcelWorksheet worksheet, string tournamentId);
}
