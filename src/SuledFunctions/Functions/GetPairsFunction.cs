using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Models;
using SuledFunctions.Services;

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
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "pairs")] 
        HttpRequestData req,
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            SqlQuery = "SELECT * FROM c WHERE c.Games != null")]
        IEnumerable<Tournament> tournaments)
    {
        _logger.LogInformation("Getting all pairs");

        try
        {
            // Use service to extract unique pairs
            var pairs = _pairService.GetUniquePairs(tournaments);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                pairs,
                totalPairs = pairs.Count()
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
