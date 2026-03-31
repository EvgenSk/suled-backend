using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services.Handlers;

public class GetPairsRequestHandler(
    ITournamentService tournamentService,
    IPairService pairService,
    ILogger<GetPairsRequestHandler> logger) : IGetPairsRequestHandler
{
    public async Task<HttpResponseData> HandleAsync(HttpRequestData req)
    {
        logger.LogInformation("Getting all pairs from all tournaments");

        try
        {
            var tournaments = await tournamentService.GetTournamentsAsync(maxResults: int.MaxValue);
            var allPairs = pairService.GetUniquePairs(tournaments).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                pairs = allPairs,
                totalPairs = allPairs.Count
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting pairs");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve pairs" });
            return errorResponse;
        }
    }
}
