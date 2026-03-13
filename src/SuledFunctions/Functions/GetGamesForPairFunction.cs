using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Models.Optimized;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get games for a specific pair
/// </summary>
public class GetGamesForPairFunction
{
    private readonly ILogger<GetGamesForPairFunction> _logger;

    public GetGamesForPairFunction(ILogger<GetGamesForPairFunction> logger)
    {
        _logger = logger;
    }

    [Function("GetGamesForPair")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "games/pair/{pairId}")] 
        HttpRequestData req,
        string pairId,
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            SqlQuery = "SELECT * FROM c WHERE c.Pairs != null")]
        IEnumerable<TournamentCompact> compactTournaments)
    {
        _logger.LogInformation("Getting games for pair: {PairId}", pairId);

        // Expand compact storage model to full Tournament model before processing
        var tournaments = compactTournaments.Select(TournamentCompactMapper.FromCompact);

        try
        {
            // Query games from pair-centered structure
            var games = GetGamesForPair(tournaments, pairId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                pairId,
                games,
                totalGames = games.Count()
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting games for pair {PairId}", pairId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve games" });
            return errorResponse;
        }
    }

    private IEnumerable<GameDto> GetGamesForPair(IEnumerable<Tournament> tournaments, string pairId)
    {
        if (tournaments == null || string.IsNullOrEmpty(pairId))
        {
            return Enumerable.Empty<GameDto>();
        }

        var pairGames = new List<GameDto>();
        
        foreach (var tournament in tournaments.Where(t => t.Pairs != null))
        {
            var pair = tournament.Pairs.FirstOrDefault(p => p.PairInfo.Id == pairId);
            if (pair?.Games != null)
            {
                var games = pair.Games
                    .OrderBy(g => g.Round)
                    .ThenBy(g => g.CourtNumber)
                    .Select(g => new GameDto
                    {
                        Id = g.Id,
                        Round = g.Round,
                        CourtNumber = g.CourtNumber,
                        Status = g.Status.ToString(),
                        ScheduledTime = g.ScheduledTime,
                        Pair1 = pair.PairInfo.DisplayName,
                        Pair2 = g.OpponentPair?.DisplayName ?? "Unknown",
                        IsOurGame = true
                    });
                pairGames.AddRange(games);
            }
        }
        
        return pairGames;
    }
}
