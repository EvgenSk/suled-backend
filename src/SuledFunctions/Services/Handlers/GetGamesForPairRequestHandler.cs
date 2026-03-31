using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services.Handlers;

public class GetGamesForPairRequestHandler(
    IGamesService gamesService,
    ILogger<GetGamesForPairRequestHandler> logger) : IGetGamesForPairRequestHandler
{
    public async Task<HttpResponseData> HandleAsync(HttpRequestData req, string pairId)
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
