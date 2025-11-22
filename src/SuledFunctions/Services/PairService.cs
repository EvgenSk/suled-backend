using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Services.Interfaces;

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

        // Extract all tournament pairs and their game counts
        var pairGameCounts = tournaments
            .Where(t => t.Pairs != null)
            .SelectMany(t => t.Pairs)
            .GroupBy(tp => tp.PairInfo.Id)
            .Select(g => new 
            { 
                PairInfo = g.First().PairInfo,
                GameCount = g.Sum(tp => tp.Games?.Count ?? 0)
            })
            .OrderBy(pg => pg.PairInfo.DisplayName)
            .ToList();

        return pairGameCounts
            .Select(pg => new PairDto
            {
                Id = pg.PairInfo.Id,
                DisplayName = pg.PairInfo.DisplayName,
                Player1 = pg.PairInfo.Player1.FullName,
                Player2 = pg.PairInfo.Player2.FullName,
                GameCount = pg.GameCount
            })
            .ToList();
    }
}
