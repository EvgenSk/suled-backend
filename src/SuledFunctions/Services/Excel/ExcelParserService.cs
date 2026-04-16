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
                return Task.FromResult(tournament);

            metadataExtractor.ExtractFromFileName(tournament, fileName);
            metadataExtractor.ExtractFromExcel(tournament, rows);

            var games = gameParser.ParseGames(rows, tournament.Id);
            tournament.Pairs = pairConverter.ConvertGamesToPairCentricStructure(games, tournament.Id);
            tournament.Rounds = roundCalculationService.CalculateRounds(tournament);
            metadataExtractor.DetermineStatus(tournament);

            logger.LogInformation(
                "Parsed {PairCount} pairs with {GameCount} total games and {RoundCount} rounds from tournament {TournamentName}",
                tournament.Pairs.Count, tournament.Pairs.Sum(p => p.Games.Count), tournament.Rounds.Count, tournament.Name);

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

        // ExcelDataReader returns time-only cells as either a DateTime with year 1899
        // (the Excel OADate epoch) or a TimeSpan, depending on the cell format.
        // Normalise both to "HH:mm:ss" strings so TimeSpan.TryParse works downstream.
        // Rounding absorbs floating-point noise from the double-precision xlsx serial.
        static string NormaliseTimeValue(TimeSpan ts) =>
            TimeSpan.FromSeconds(Math.Round(ts.TotalSeconds)).ToString(@"hh\:mm\:ss");

        while (reader.Read())
        {
            var row = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[i] = value switch
                {
                    DateTime { Year: 1899 } dt => NormaliseTimeValue(dt.TimeOfDay),
                    TimeSpan ts               => NormaliseTimeValue(ts),
                    _                         => value?.ToString() ?? string.Empty
                };
            }
            rows.Add(row);
        }

        return rows.ToArray();
    }
}
