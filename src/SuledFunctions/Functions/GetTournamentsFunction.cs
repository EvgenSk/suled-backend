using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services;
using SuledFunctions.Contracts.DTOs;
using SuledFunctions.Models;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get list of tournaments with filtering
/// </summary>
public class GetTournamentsFunction
{
    private readonly ILogger<GetTournamentsFunction> _logger;
    private readonly ITournamentService _tournamentService;

    public GetTournamentsFunction(
        ILogger<GetTournamentsFunction> logger,
        ITournamentService tournamentService)
    {
        _logger = logger;
        _tournamentService = tournamentService;
    }

    [Function("GetTournaments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tournaments")] 
        HttpRequestData req)
    {
        _logger.LogInformation("Processing get tournaments request");

        try
        {
            // Parse query parameters
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            
            DateTime? startDateFrom = null;
            DateTime? startDateTo = null;
            string? location = null;
            string? division = null;
            TournamentStatus? status = null;
            int maxResults = 100;

            // Parse startDateFrom
            if (query["startDateFrom"] != null && DateTime.TryParse(query["startDateFrom"], out var dateFrom))
            {
                startDateFrom = dateFrom;
            }

            // Parse startDateTo
            if (query["startDateTo"] != null && DateTime.TryParse(query["startDateTo"], out var dateTo))
            {
                startDateTo = dateTo;
            }

            // Parse location
            location = query["location"];

            // Parse division
            division = query["division"];

            // Parse status
            if (query["status"] != null && Enum.TryParse<TournamentStatus>(query["status"], true, out var parsedStatus))
            {
                status = parsedStatus;
            }

            // Parse maxResults
            if (query["maxResults"] != null && int.TryParse(query["maxResults"], out var max))
            {
                maxResults = Math.Min(max, 500); // Cap at 500
            }

            _logger.LogInformation(
                "Querying tournaments: startDateFrom={StartDateFrom}, startDateTo={StartDateTo}, location={Location}, division={Division}, status={Status}",
                startDateFrom, startDateTo, location, division, status);

            // Get tournaments
            var tournaments = await _tournamentService.GetTournamentsAsync(
                startDateFrom, startDateTo, location, division, status, maxResults);

            // Map to DTOs
            var tournamentDtos = tournaments.Select(t => new TournamentListDto
            {
                Id = t.Id,
                Name = t.Name,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                Location = t.Location,
                Division = t.Division,
                Description = t.Description,
                Status = t.Status.ToString(),
                GameCount = t.Games?.Count ?? 0,
                CreatedDate = t.CreatedDate
            }).ToList();

            _logger.LogInformation("Returning {Count} tournaments", tournamentDtos.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(tournamentDtos);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournaments");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = "An error occurred while retrieving tournaments" });
            return response;
        }
    }
}
