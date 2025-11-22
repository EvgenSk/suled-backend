using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel;

namespace SuledFunctions.Services;

/// <summary>
/// Service for parsing tournament Excel files
/// Expected format: Each row contains court number and pairs playing
/// </summary>
public class ExcelParserService : IExcelParserService
{
    private readonly ILogger<ExcelParserService> _logger;
    private readonly IExcelMetadataExtractor _metadataExtractor;
    private readonly IExcelGameParser _gameParser;
    private readonly IPairStructureConverter _pairConverter;

    public ExcelParserService(
        ILogger<ExcelParserService> logger,
        IExcelMetadataExtractor metadataExtractor,
        IExcelGameParser gameParser,
        IPairStructureConverter pairConverter)
    {
        _logger = logger;
        _metadataExtractor = metadataExtractor;
        _gameParser = gameParser;
        _pairConverter = pairConverter;
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

            // Extract metadata from filename
            _metadataExtractor.ExtractFromFileName(tournament, fileName);
            
            // Try to extract metadata from Excel (optional)
            _metadataExtractor.ExtractFromExcel(tournament, worksheet);

            var games = _gameParser.ParseGames(worksheet, tournament.Id);
            
            // Convert game-centered data to pair-centered structure
            tournament.Pairs = _pairConverter.ConvertGamesToPairCentricStructure(games, tournament.Id);
            
            // Auto-determine tournament status based on dates
            _metadataExtractor.DetermineStatus(tournament);

            var totalGames = tournament.Pairs.Sum(p => p.Games.Count);
            _logger.LogInformation("Parsed {PairCount} pairs with {GameCount} total games from tournament {TournamentName}", 
                tournament.Pairs.Count, totalGames, tournament.Name);

            return tournament;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Excel file {FileName}", fileName);
            throw;
        }
    }
}
