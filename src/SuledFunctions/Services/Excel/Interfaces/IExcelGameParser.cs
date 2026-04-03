using SuledFunctions.Models;

namespace SuledFunctions.Services.Excel.Interfaces;

/// <summary>
/// Interface for parsing game data from rows of cell values
/// </summary>
public interface IExcelGameParser
{
    /// <summary>
    /// Parse all games from rows (0-indexed: rows[rowIndex][columnIndex])
    /// </summary>
    List<Game> ParseGames(string[][] rows, string tournamentId);
}
