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
    /// <returns>List of unique pairs sorted by display name</returns>
    public IEnumerable<PairDto> GetUniquePairs(IEnumerable<Tournament> tournaments)
    {
        if (tournaments == null)
        {
            return Enumerable.Empty<PairDto>();
        }

        return tournaments
            .Where(t => t.Games != null)
            .SelectMany(t => t.Games)
            .SelectMany(g => new[] { g.Pair1, g.Pair2 })
            .Where(p => p != null)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.DisplayName)
            .Select(p => new PairDto
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                Player1 = p.Player1.FullName,
                Player2 = p.Player2.FullName
            })
            .ToList();
    }
}
