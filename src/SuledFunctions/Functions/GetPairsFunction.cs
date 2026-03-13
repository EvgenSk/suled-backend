using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get all available pairs from tournaments
/// </summary>
public class GetPairsFunction
{
    private readonly ITournamentService _tournamentService;
    private readonly IPairService _pairService;
    private readonly ILogger<GetPairsFunction> _logger;

    public GetPairsFunction(
        ITournamentService tournamentService,
        IPairService pairService,
        ILogger<GetPairsFunction> logger)
    {
        _tournamentService = tournamentService;
        _pairService = pairService;
        _logger = logger;
    }

    [Function("GetPairs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pairs")] 
        HttpRequestData req)
    {
        _logger.LogInformation("Getting all pairs from all tournaments");

        try
        {
            // Fetch all tournaments; int.MaxValue removes the default 100-item cap.
            var tournaments = await _tournamentService.GetTournamentsAsync(maxResults: int.MaxValue);
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
