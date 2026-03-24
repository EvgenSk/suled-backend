using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get a tournament by ID
/// </summary>
public class GetTournamentFunction(ITournamentService tournamentService, ILogger<GetTournamentFunction> logger)
{
    [Function("GetTournament")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournament/{id}")] 
        HttpRequestData req,
        string id)
    {
        logger.LogInformation("Getting tournament with ID: {TournamentId}", id);

        try
        {
            var tournament = await tournamentService.GetTournamentByIdAsync(id);

            if (tournament == null)
            {
                logger.LogWarning("Tournament not found: {TournamentId}", id);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { error = $"Tournament with ID '{id}' not found" });
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(tournament);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving tournament {TournamentId}", id);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve tournament" });
            return errorResponse;
        }
    }
}
