using ExcelDataReader;
using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel.Interfaces;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Service for parsing tournament Excel files.
/// Reads the xlsx stream into rows of cell strings, then delegates to
/// ExcelMetadataExtractor and ExcelGameParser which work on plain string[][].
/// </summary>
public class ExcelParserService(
    IExcelMetadataExtractor metadataExtractor,
    IExcelGameParser gameParser,
    IPairStructureConverter pairConverter,
    IRoundCalculationService roundCalculationService,
    ILogger<ExcelParserService> logger) : IExcelParserService
{
    static ExcelParserService()
    {
        // Required by ExcelDataReader on .NET Core for non-UTF encodings
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Parse tournament data from an Excel (.xlsx / .xls) stream
    /// </summary>
    public Task<Tournament> ParseTournamentAsync(Stream excelStream, string fileName)
    {
        try
        {
            var tournament = new Tournament
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                BlobFileName = fileName
            };

            var rows = ReadFirstSheetRows(excelStream);

            if (rows.Length == 0)
            {
                return Task.FromResult(tournament);
            }

            // Extract metadata from filename
            metadataExtractor.ExtractFromFileName(tournament, fileName);

            // Try to extract metadata from cell content (optional)
            metadataExtractor.ExtractFromExcel(tournament, rows);

            var games = gameParser.ParseGames(rows, tournament.Id);

            // Convert game-centered data to pair-centered structure
            tournament.Pairs = pairConverter.ConvertGamesToPairCentricStructure(games, tournament.Id);

            // Calculate round schedules based on tournament metadata and games
            tournament.Rounds = roundCalculationService.CalculateRounds(tournament);

            // Auto-determine tournament status based on dates
            metadataExtractor.DetermineStatus(tournament);

            var totalGames = tournament.Pairs.Sum(p => p.Games.Count);
            logger.LogInformation(
                "Parsed {PairCount} pairs with {GameCount} total games and {RoundCount} rounds from tournament {TournamentName}",
                tournament.Pairs.Count, totalGames, tournament.Rounds.Count, tournament.Name);

            return Task.FromResult(tournament);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing Excel file {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Reads the first worksheet of an Excel file into a jagged string array.
    /// rows[rowIndex][columnIndex] — both indices are 0-based.
    /// </summary>
    private static string[][] ReadFirstSheetRows(Stream excelStream)
    {
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        var rows = new List<string[]>();

        while (reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.GetValue(i)?.ToString() ?? string.Empty;
            }
            rows.Add(row);
        }

        return rows.ToArray();
    }
}
