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
    private readonly IPairService _pairService;
    private readonly ILogger<GetPairsFunction> _logger;

    public GetPairsFunction(IPairService pairService, ILogger<GetPairsFunction> logger)
    {
        _pairService = pairService;
        _logger = logger;
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
            var allPairs = _pairService.GetUniquePairs(tournaments).ToList();

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
