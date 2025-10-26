using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get games for a specific pair
/// </summary>
public class GetGamesForPairFunction
{
    private readonly ILogger<GetGamesForPairFunction> _logger;
    private readonly IGameService _gameService;

    public GetGamesForPairFunction(ILogger<GetGamesForPairFunction> logger, IGameService gameService)
    {
        _logger = logger;
        _gameService = gameService;
    }

    [Function("GetGamesForPair")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "games/pair/{pairId}")] 
        HttpRequestData req,
        string pairId,
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            SqlQuery = "SELECT * FROM c WHERE c.Games != null")]
        IEnumerable<Tournament> tournaments)
    {
        _logger.LogInformation("Getting games for pair: {PairId}", pairId);

        try
        {
            // Use service to get games for the pair
            var games = _gameService.GetGamesForPair(tournaments, pairId);

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
}
