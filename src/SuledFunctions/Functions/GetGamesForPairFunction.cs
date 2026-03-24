using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get games for a specific pair
/// </summary>
public class GetGamesForPairFunction(
    IGamesService gamesService,
    ILogger<GetGamesForPairFunction> logger)
{
    [Function("GetGamesForPair")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "games/pair/{pairId}")] 
        HttpRequestData req,
        string pairId)
    {
        logger.LogInformation("Getting games for pair: {PairId}", pairId);

        try
        {
            var games = (await gamesService.GetGamesForPairAsync(pairId)).ToArray();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                pairId,
                games,
                totalGames = games.Length
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting games for pair {PairId}", pairId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve games" });
            return errorResponse;
        }
    }
}
