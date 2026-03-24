using SuledFunctions.Models.DTOs;

namespace SuledFunctions.Services.Interfaces;

/// <summary>
/// Service for games-related business logic
/// </summary>
public interface IGamesService
{
    /// <summary>
    /// Returns all games played by the given pair, ordered by round then court number.
    /// </summary>
    Task<IEnumerable<GameDto>> GetGamesForPairAsync(string pairId);
}
