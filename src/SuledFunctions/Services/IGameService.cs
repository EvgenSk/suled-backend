using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;

namespace SuledFunctions.Services;

/// <summary>
/// Interface for game-related business logic
/// </summary>
public interface IGameService
{
    /// <summary>
    /// Gets all games for a specific pair, ordered by round and court number
    /// </summary>
    /// <param name="tournaments">Collection of tournaments to search</param>
    /// <param name="pairId">The ID of the pair to find games for</param>
    /// <returns>List of games involving the specified pair</returns>
    IEnumerable<GameDto> GetGamesForPair(IEnumerable<Tournament> tournaments, string pairId);
}
