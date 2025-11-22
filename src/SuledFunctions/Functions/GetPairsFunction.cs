using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Models;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get all available pairs from tournaments
/// </summary>
public class GetPairsFunction
{
    private readonly ILogger<GetPairsFunction> _logger;
    private readonly IPairService _pairService;

    public GetPairsFunction(ILogger<GetPairsFunction> logger, IPairService pairService)
    {
        _logger = logger;
        _pairService = pairService;
    }

    [Function("GetPairs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pairs")] 
        HttpRequestData req,
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            SqlQuery = "SELECT * FROM c WHERE c.Pairs != null")]
        IEnumerable<Tournament> tournaments)
    {
        _logger.LogInformation("Getting all pairs from all tournaments");

        try
        {
            // Extract all pairs from all tournaments (pair-centered structure)
            var allPairs = tournaments
                .Where(t => t.Pairs != null)
                .SelectMany(t => t.Pairs)
                .GroupBy(p => p.Id)
                .Select(g => g.First()) // Take first instance of each unique pair
                .OrderBy(p => p.DisplayName)
                .Select(p => new
                {
                    id = p.Id,
                    displayName = p.DisplayName,
                    player1 = p.PairInfo.Player1.FullName,
                    player2 = p.PairInfo.Player2.FullName,
                    gameCount = p.GameCount
                })
                .ToList();

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
            _logger.LogError(ex, "Error getting pairs");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve pairs" });
            return errorResponse;
        }
    }
}
