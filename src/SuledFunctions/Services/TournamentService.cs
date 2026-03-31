using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuledFunctions.Configuration;
using SuledFunctions.Exceptions;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services.Excel.Interfaces;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Service for managing tournaments with business logic
/// </summary>
public class TournamentService : ITournamentService
{
    private readonly ITournamentRepository _repository;
    private readonly IExcelMetadataExtractor _metadataExtractor;
    private readonly ILogger<TournamentService> _logger;
    private readonly TournamentSettings _settings;

    public TournamentService(
        ITournamentRepository repository,
        IExcelMetadataExtractor metadataExtractor,
        IOptions<TournamentSettings> settings,
        ILogger<TournamentService> logger)
    {
        _repository = repository;
        _metadataExtractor = metadataExtractor;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<List<Tournament>> GetTournamentsAsync(
        DateTime? startDateFrom = null,
        DateTime? startDateTo = null,
        string? location = null,
        string? division = null,
        TournamentStatus? status = null,
        int maxResults = 100)
    {
        try
        {
            var querySpec = BuildQuerySpec(startDateFrom, startDateTo, location, division, status, maxResults);
            var compactTournaments = await _repository.QueryAsync(querySpec);
            var tournaments = ExpandAndRefreshStatus(compactTournaments);
            var result = FilterAndSort(tournaments, querySpec);
            _logger.LogInformation("Retrieved {Count} tournaments", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournaments");
            throw;
        }
    }

    public async Task<Tournament?> GetTournamentByIdAsync(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ValidationException("id", "Tournament ID cannot be empty");

            var compact = await _repository.GetByIdAsync(id);
            return compact == null ? null : TournamentCompactMapper.FromCompact(compact);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournament {TournamentId}", id);
            throw;
        }
    }

    private TournamentQuerySpec BuildQuerySpec(DateTime? startDateFrom, DateTime? startDateTo,
        string? location, string? division, TournamentStatus? status, int maxResults) => new()
    {
        StartDateFrom = startDateFrom,
        StartDateTo = startDateTo,
        Location = location,
        Division = division,
        Status = status,
        MaxResults = maxResults > 0 ? maxResults : _settings.MaxResultsDefault
    };

    private List<Tournament> ExpandAndRefreshStatus(List<TournamentCompact> compactTournaments)
    {
        var tournaments = compactTournaments.Select(TournamentCompactMapper.FromCompact).ToList();
        foreach (var t in tournaments)
            _metadataExtractor.DetermineStatus(t);
        return tournaments;
    }

    private static List<Tournament> FilterAndSort(List<Tournament> tournaments, TournamentQuerySpec querySpec)
    {
        if (querySpec.Status.HasValue)
            tournaments = tournaments.Where(t => t.Status == querySpec.Status.Value).ToList();

        return tournaments
            .OrderByDescending(t => t.StartDate ?? DateTime.MinValue)
            .Take(querySpec.MaxResults)
            .ToList();
    }
}
