using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Service for games-related business logic
/// </summary>
public class GamesService(ITournamentService tournamentService) : IGamesService
{
    public async Task<IEnumerable<GameDto>> GetGamesForPairAsync(string pairId)
    {
        if (string.IsNullOrEmpty(pairId))
            return Enumerable.Empty<GameDto>();

        var tournaments = await tournamentService.GetTournamentsAsync(maxResults: int.MaxValue);

        return tournaments
            .Where(t => t.Pairs != null)
            .SelectMany(t => t.Pairs)
            .Where(p => p.PairInfo.Id == pairId)
            .SelectMany(p => (p.Games ?? Enumerable.Empty<PairGame>())
                .OrderBy(g => g.Round)
                .ThenBy(g => g.CourtNumber)
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Round = g.Round,
                    CourtNumber = g.CourtNumber,
                    Status = g.Status.ToString(),
                    ScheduledTime = g.ScheduledTime,
                    Pair1 = p.PairInfo.DisplayName,
                    Pair2 = g.OpponentPair?.DisplayName ?? "Unknown",
                    IsOurGame = true
                }));
    }
}
