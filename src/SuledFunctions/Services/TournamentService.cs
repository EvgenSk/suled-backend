using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuledFunctions.Configuration;
using SuledFunctions.Exceptions;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Service for managing tournaments with business logic
/// </summary>
public class TournamentService : ITournamentService
{
    private readonly ITournamentRepository _repository;
    private readonly ILogger<TournamentService> _logger;
    private readonly TournamentSettings _settings;

    public TournamentService(
        ITournamentRepository repository,
        IOptions<TournamentSettings> settings,
        ILogger<TournamentService> logger)
    {
        _repository = repository;
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
            var querySpec = new TournamentQuerySpec
            {
                StartDateFrom = startDateFrom,
                StartDateTo = startDateTo,
                Location = location,
                Division = division,
                Status = status,
                MaxResults = maxResults > 0 ? maxResults : _settings.MaxResultsDefault
            };

            var compactTournaments = await _repository.QueryAsync(querySpec);

            // Expand compact format to full Tournament models
            var tournaments = compactTournaments.Select(TournamentCompactMapper.FromCompact).ToList();

            // Sort by StartDate in memory (descending - most recent first)
            var sortedTournaments = tournaments
                .OrderByDescending(t => t.StartDate ?? DateTime.MinValue)
                .Take(querySpec.MaxResults)
                .ToList();

            _logger.LogInformation("Retrieved {Count} tournaments", sortedTournaments.Count);
            return sortedTournaments;
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
            {
                throw new ValidationException("id", "Tournament ID cannot be empty");
            }

            var compactTournament = await _repository.GetByIdAsync(id);
            
            if (compactTournament == null)
            {
                return null;
            }

            return TournamentCompactMapper.FromCompact(compactTournament);
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
}
