using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;

namespace SuledFunctions.Services;

/// <summary>
/// Interface for pair-related business logic
/// </summary>
public interface IPairService
{
    /// <summary>
    /// Extracts all unique pairs from tournaments and returns them sorted by display name
    /// </summary>
    /// <param name="tournaments">Collection of tournaments to extract pairs from</param>
    /// <returns>List of unique pairs sorted by display name</returns>
    IEnumerable<PairDto> GetUniquePairs(IEnumerable<Tournament> tournaments);
}
