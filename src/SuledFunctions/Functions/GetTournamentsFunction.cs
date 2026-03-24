using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Contracts.DTOs;
using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Models.Requests;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get list of tournaments with filtering
/// </summary>
public class GetTournamentsFunction(
    ITournamentService tournamentService,
    IValidator<GetTournamentsQuery> validator,
    ILogger<GetTournamentsFunction> logger)
{
    [Function("GetTournaments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournaments")] 
        HttpRequestData req)
    {
        logger.LogInformation("Processing get tournaments request");

        // Parse and validate query parameters
        var queryString = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var query = GetTournamentsQuery.Parse(queryString);

        // Validate query
        var validationResult = await validator.ValidateAsync(query);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            throw new Exceptions.ValidationException("Query validation failed", errors);
        }

        logger.LogInformation(
            "Querying tournaments: startDateFrom={StartDateFrom}, startDateTo={StartDateTo}, location={Location}, division={Division}, status={Status}, maxResults={MaxResults}",
            query.StartDateFrom, query.StartDateTo, query.Location, query.Division, query.Status, query.MaxResults);

        // Get tournaments
        var tournaments = await tournamentService.GetTournamentsAsync(
            query.StartDateFrom, 
            query.StartDateTo, 
            query.Location, 
            query.Division, 
            query.Status, 
            query.MaxResults ?? 100);

        var tournamentDtos = tournaments.Select(TournamentDtoMapper.ToListDto).ToList();

        logger.LogInformation("Returning {Count} tournaments", tournamentDtos.Count);

        // Return unified response
        var apiResponse = ApiResponse<List<TournamentListDto>>.Ok(
            tournamentDtos, 
            $"Retrieved {tournamentDtos.Count} tournament(s)");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(apiResponse);
        return response;
    }
}
