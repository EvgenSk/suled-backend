using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get a tournament by ID
/// </summary>
public class GetTournamentFunction
{
    private readonly ILogger<GetTournamentFunction> _logger;

    public GetTournamentFunction(ILogger<GetTournamentFunction> logger)
    {
        _logger = logger;
    }

    [Function("GetTournament")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournament/{id}")] 
        HttpRequestData req,
        string id,
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            Id = "{id}",
            PartitionKey = "{id}")]
        TournamentCompact? compactTournament)
    {
        _logger.LogInformation("Getting tournament with ID: {TournamentId}", id);

        if (compactTournament == null)
        {
            _logger.LogWarning("Tournament not found: {TournamentId}", id);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = $"Tournament with ID '{id}' not found" });
            return notFoundResponse;
        }

        try
        {
            // Expand compact format to full Tournament for API response
            var tournament = TournamentCompactMapper.FromCompact(compactTournament);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(tournament);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournament {TournamentId}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve tournament" });
            return errorResponse;
        }
    }
}
