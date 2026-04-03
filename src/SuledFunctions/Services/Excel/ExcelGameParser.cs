using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel.Interfaces;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Parses game data from rows of cell values
/// </summary>
public class ExcelGameParser(ILogger<ExcelGameParser> logger) : IExcelGameParser
{

    /// <summary>
    /// Parse all games from rows (rows[rowIndex][columnIndex], both 0-based; row 0 is the header)
    /// </summary>
    public List<Game> ParseGames(string[][] rows, string tournamentId)
    {
        var games = new List<Game>();
        
        // Check if there is any data beyond the header
        if (rows.Length <= 1)
        {
            logger.LogWarning("Worksheet is empty, no data to parse");
            return games;
        }
        
        int currentRound = 1;
        
        // Start from row index 1 (skipping header row 0)
        for (int rowIdx = 1; rowIdx < rows.Length; rowIdx++)
        {
            try
            {
                // Skip empty rows
                if (IsEmptyRow(rows, rowIdx))
                {
                    continue;
                }

                // Check if this is a round header (game data starts on the same row)
                var firstCell = GetCell(rows, rowIdx, 0).Trim();
                if (firstCell.StartsWith("Round", StringComparison.OrdinalIgnoreCase) ||
                    firstCell.StartsWith("Runde", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract round number if present
                    var roundText = firstCell.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (roundText.Length > 1 && int.TryParse(roundText[1], out int roundNumber))
                    {
                        currentRound = roundNumber;
                    }
                    // Don't skip - game data is on the same row, continue parsing below
                }

                var game = ParseGameRow(rows, rowIdx, currentRound, tournamentId);
                if (game == null) continue;
                
                games.Add(game);
                logger.LogDebug("Parsed game: Court {Court}, Round {Round}, {Pair1} vs {Pair2}",
                    game.CourtNumber, game.Round, game.Pair1.DisplayName, game.Pair2.DisplayName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error parsing row {Row}, skipping", rowIdx + 1);
            }
        }

        return games;
    }

    private Game? ParseGameRow(string[][] rows, int rowIdx, int round, string tournamentId)
    {
        // Expected column layout (0-based index):
        // 0: Round header (e.g., "Round 1") - optional on game rows
        // 1: Court number
        // 2: Player 1 of Pair 1
        // 3: Player 2 of Pair 1
        // 6: Player 1 of Pair 2
        // 7: Player 2 of Pair 2
        
        var courtText = GetCell(rows, rowIdx, 1).Trim();
        
        // Skip if court column is not a number
        if (!int.TryParse(courtText, out int courtNumber))
        {
            return null;
        }

        // Parse individual players for Pair 1
        var player1_1Text = GetCell(rows, rowIdx, 2).Trim();
        var player1_2Text = GetCell(rows, rowIdx, 3).Trim();
        
        // Parse individual players for Pair 2
        var player2_1Text = GetCell(rows, rowIdx, 6).Trim();
        var player2_2Text = GetCell(rows, rowIdx, 7).Trim();

        // Build pairs from individual players
        var pair1 = BuildPairFromPlayers(player1_1Text, player1_2Text);
        var pair2 = BuildPairFromPlayers(player2_1Text, player2_2Text);

        if (pair1 != null && pair2 != null)
            return new Game
            {
                TournamentId = tournamentId,
                Round = round,
                CourtNumber = courtNumber,
                Pair1 = pair1,
                Pair2 = pair2,
                Status = GameStatus.Scheduled
            };
        logger.LogWarning("Could not parse pairs in row {Row}: Pair1 ({P1_1}, {P1_2}), Pair2 ({P2_1}, {P2_2})", 
            rowIdx + 1, player1_1Text, player1_2Text, player2_1Text, player2_2Text);
        return null;

    }

    private Pair? BuildPairFromPlayers(string player1Text, string player2Text)
    {
        if (string.IsNullOrWhiteSpace(player1Text) && string.IsNullOrWhiteSpace(player2Text))
        {
            return null;
        }

        var player1 = ParsePlayer(player1Text);
        var player2 = ParsePlayer(player2Text);

        var pair = new Pair
        {
            Player1 = player1,
            Player2 = player2
        };
        
        // Access Id to trigger generation (ensures consistent IDs)
        _ = pair.Id;
        
        return pair;
    }

    private Player ParsePlayer(string playerText)
    {
        if (string.IsNullOrWhiteSpace(playerText))
        {
            return new Player { Name = "Unknown" };
        }

        // Clean up extra spaces
        playerText = System.Text.RegularExpressions.Regex.Replace(playerText, @"\s+", " ").Trim();
        
        var parts = playerText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            1 => new Player { Name = parts[0] },
            >= 2 => new Player { Name = parts[0], Surname = string.Join(" ", parts.Skip(1)) },
            _ => new Player { Name = playerText }
        };
    }

    private static bool IsEmptyRow(string[][] rows, int rowIdx)
    {
        if (rowIdx >= rows.Length) return true;
        var row = rows[rowIdx];
        for (int col = 0; col < Math.Min(row.Length, 4); col++)
        {
            if (!string.IsNullOrWhiteSpace(row[col]))
            {
                return false;
            }
        }
        return true;
    }

    private static string GetCell(string[][] rows, int rowIdx, int colIdx)
    {
        if (rowIdx >= rows.Length) return string.Empty;
        var row = rows[rowIdx];
        if (colIdx >= row.Length) return string.Empty;
        return row[colIdx] ?? string.Empty;
    }
}
