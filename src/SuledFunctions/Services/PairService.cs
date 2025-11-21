using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;

namespace SuledFunctions.Services;

/// <summary>
/// Service for pair-related business logic
/// </summary>
public class PairService : IPairService
{
    /// <summary>
    /// Extracts all unique pairs from tournaments and returns them sorted by display name
    /// </summary>
    /// <param name="tournaments">Collection of tournaments to extract pairs from</param>
    /// <returns>List of unique pairs sorted by display name with game counts</returns>
    public IEnumerable<PairDto> GetUniquePairs(IEnumerable<Tournament> tournaments)
    {
        if (tournaments == null)
        {
            return Enumerable.Empty<PairDto>();
        }

        // Get all games and extract pairs with their game counts
        var allGames = tournaments
            .Where(t => t.Games != null)
            .SelectMany(t => t.Games)
            .ToList();

        // Extract all pairs from games and count how many games each pair played
        var pairGameCounts = allGames
            .SelectMany(g => new[] { g.Pair1, g.Pair2 })
            .Where(p => p != null)
            .GroupBy(p => p.Id)
            .Select(g => new 
            { 
                Pair = g.First(),
                GameCount = g.Count()
            })
            .OrderBy(pg => pg.Pair.DisplayName)
            .ToList();

        return pairGameCounts
            .Select(pg => new PairDto
            {
                Id = pg.Pair.Id,
                DisplayName = pg.Pair.DisplayName,
                Player1 = pg.Pair.Player1.FullName,
                Player2 = pg.Pair.Player2.FullName,
                GameCount = pg.GameCount
            })
            .ToList();
    }
}
