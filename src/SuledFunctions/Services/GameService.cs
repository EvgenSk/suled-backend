using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;

namespace SuledFunctions.Services;

/// <summary>
/// Service for game-related business logic
/// </summary>
public class GameService : IGameService
{
    /// <summary>
    /// Gets all games for a specific pair, ordered by round and court number
    /// </summary>
    /// <param name="tournaments">Collection of tournaments to search</param>
    /// <param name="pairId">The ID of the pair to find games for</param>
    /// <returns>List of games involving the specified pair</returns>
    public IEnumerable<GameDto> GetGamesForPair(IEnumerable<Tournament> tournaments, string pairId)
    {
        if (tournaments == null || string.IsNullOrEmpty(pairId))
        {
            return Enumerable.Empty<GameDto>();
        }

        return tournaments
            .Where(t => t.Games != null)
            .SelectMany(t => t.Games)
            .Where(g => g.Pair1?.Id == pairId || g.Pair2?.Id == pairId)
            .OrderBy(g => g.Round)
            .ThenBy(g => g.CourtNumber)
            .Select(g => new GameDto
            {
                Id = g.Id,
                Round = g.Round,
                CourtNumber = g.CourtNumber,
                Status = g.Status.ToString(),
                ScheduledTime = g.ScheduledTime,
                Pair1 = g.Pair1.DisplayName,
                Pair2 = g.Pair2.DisplayName,
                IsOurGame = g.Pair1.Id == pairId
            })
            .ToList();
    }
}
