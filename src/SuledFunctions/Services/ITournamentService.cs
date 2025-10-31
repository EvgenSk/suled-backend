using SuledFunctions.Models;

namespace SuledFunctions.Services;

/// <summary>
/// Service for managing tournaments
/// </summary>
public interface ITournamentService
{
    /// <summary>
    /// Get tournaments with optional filtering
    /// </summary>
    Task<List<Tournament>> GetTournamentsAsync(
        DateTime? startDateFrom = null,
        DateTime? startDateTo = null,
        string? location = null,
        string? division = null,
        TournamentStatus? status = null,
        int maxResults = 100);
    
    /// <summary>
    /// Get tournament by ID
    /// </summary>
    Task<Tournament?> GetTournamentByIdAsync(string id);
}
