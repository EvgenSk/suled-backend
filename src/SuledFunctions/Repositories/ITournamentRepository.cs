using SuledFunctions.Models.Optimized;

namespace SuledFunctions.Repositories;

/// <summary>
/// Repository interface for tournament data access
/// </summary>
public interface ITournamentRepository
{
    /// <summary>
    /// Queries tournaments based on the provided specification
    /// </summary>
    Task<List<TournamentCompact>> QueryAsync(TournamentQuerySpec querySpec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tournament by its ID
    /// </summary>
    Task<TournamentCompact?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tournament
    /// </summary>
    Task<TournamentCompact> CreateAsync(TournamentCompact tournament, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tournament
    /// </summary>
    Task<TournamentCompact> UpdateAsync(TournamentCompact tournament, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tournament by its ID
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tournament exists
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
}
