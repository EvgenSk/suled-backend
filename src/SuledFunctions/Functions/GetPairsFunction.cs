using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get all available pairs from tournaments
/// </summary>
public class GetPairsFunction(
    ITournamentService tournamentService,
    IPairService pairService,
    ILogger<GetPairsFunction> logger)
{
    [Function("GetPairs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pairs")] 
        HttpRequestData req)
    {
        logger.LogInformation("Getting all pairs from all tournaments");

        try
        {
            // Fetch all tournaments; int.MaxValue removes the default 100-item cap.
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
