using OfficeOpenXml;
using SuledFunctions.Models;
using Microsoft.Extensions.Logging;

namespace SuledFunctions.Services;

/// <summary>
/// Service for parsing tournament Excel files
/// Expected format: Each row contains court number and pairs playing
/// </summary>
public class ExcelParserService : IExcelParserService
{
    private readonly ILogger<ExcelParserService> _logger;

    public ExcelParserService(ILogger<ExcelParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse tournament data from Excel stream
    /// </summary>
    public async Task<Tournament> ParseTournamentAsync(Stream excelStream, string fileName)
    {
        try
        {
            var tournament = new Tournament
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                BlobFileName = fileName
            };
            
            using var package = new ExcelPackage(excelStream);
            var worksheet = package.Workbook.Worksheets[0]; // Get first worksheet
            
            if (worksheet == null)
            {
                throw new InvalidOperationException("No worksheet found in Excel file");
            }

            var games = ParseGames(worksheet, tournament.Id);
            tournament.Games = games;

            _logger.LogInformation("Parsed {GameCount} games from tournament {TournamentName}", 
                games.Count, tournament.Name);

            return tournament;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Excel file {FileName}", fileName);
            throw;
        }
    }

    private List<Game> ParseGames(ExcelWorksheet worksheet, string tournamentId)
    {
        var games = new List<Game>();
        
        // Check if worksheet has any data
        if (worksheet.Dimension == null)
        {
            _logger.LogWarning("Worksheet is empty, no data to parse");
            return games;
        }
        
        int currentRound = 1;
        
        // Start from row 2 (assuming row 1 is header)
        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            try
            {
                // Skip empty rows
                if (IsEmptyRow(worksheet, row))
                {
                    continue;
                }

                // Check if this is a round header (game data starts on the same row)
                var firstCell = worksheet.Cells[row, 1].Text.Trim();
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

                var game = ParseGameRow(worksheet, row, currentRound, tournamentId);
                if (game != null)
                {
                    games.Add(game);
                    _logger.LogDebug("Parsed game: Court {Court}, Round {Round}, {Pair1} vs {Pair2}",
                        game.CourtNumber, game.Round, game.Pair1.DisplayName, game.Pair2.DisplayName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing row {Row}, skipping", row);
            }
        }

        return games;
    }

    private Game? ParseGameRow(ExcelWorksheet worksheet, int row, int round, string tournamentId)
    {
        // Expected format:
        // Column A (1): Round header (e.g., "Round 1") - optional on game rows
        // Column B (2): Court number
        // Column C (3): Player 1 of Pair 1
        // Column D (4): Player 2 of Pair 1
        // Column G (7): Player 1 of Pair 2
        // Column H (8): Player 2 of Pair 2
        
        var courtText = worksheet.Cells[row, 2].Text.Trim();
        
        // Skip if court column is not a number
        if (!int.TryParse(courtText, out int courtNumber))
        {
            return null;
        }

        // Parse individual players for Pair 1
        var player1_1Text = worksheet.Cells[row, 3].Text.Trim();
        var player1_2Text = worksheet.Cells[row, 4].Text.Trim();
        
        // Parse individual players for Pair 2
        var player2_1Text = worksheet.Cells[row, 7].Text.Trim();
        var player2_2Text = worksheet.Cells[row, 8].Text.Trim();

        // Build pairs from individual players
        var pair1 = BuildPairFromPlayers(player1_1Text, player1_2Text);
        var pair2 = BuildPairFromPlayers(player2_1Text, player2_2Text);

        if (pair1 == null || pair2 == null)
        {
            _logger.LogWarning("Could not parse pairs in row {Row}: Pair1 ({P1_1}, {P1_2}), Pair2 ({P2_1}, {P2_2})", 
                row, player1_1Text, player1_2Text, player2_1Text, player2_2Text);
            return null;
        }

        return new Game
        {
            TournamentId = tournamentId,
            Round = round,
            CourtNumber = courtNumber,
            Pair1 = pair1,
            Pair2 = pair2,
            Status = GameStatus.Scheduled
        };
    }

    private Pair? BuildPairFromPlayers(string player1Text, string player2Text)
    {
        if (string.IsNullOrWhiteSpace(player1Text) && string.IsNullOrWhiteSpace(player2Text))
        {
            return null;
        }

        var player1 = ParsePlayer(player1Text);
        var player2 = ParsePlayer(player2Text);

        return new Pair
        {
            Player1 = player1,
            Player2 = player2
        };
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
        
        if (parts.Length == 1)
        {
            return new Player { Name = parts[0] };
        }
        else if (parts.Length >= 2)
        {
            return new Player 
            { 
                Name = parts[0],
                Surname = string.Join(" ", parts.Skip(1))
            };
        }

        return new Player { Name = playerText };
    }

    private bool IsEmptyRow(ExcelWorksheet worksheet, int row)
    {
        // Check if all cells in the row are empty
        for (int col = 1; col <= Math.Min(worksheet.Dimension.End.Column, 4); col++)
        {
            if (!string.IsNullOrWhiteSpace(worksheet.Cells[row, col].Text))
            {
                return false;
            }
        }
        return true;
    }
}
