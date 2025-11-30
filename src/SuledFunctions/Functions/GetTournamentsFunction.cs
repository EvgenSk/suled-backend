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
using SuledFunctions.Validators;
using SuledFunctions.Exceptions;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to get list of tournaments with filtering
/// </summary>
public class GetTournamentsFunction
{
    private readonly ITournamentService _tournamentService;
    private readonly ILogger<GetTournamentsFunction> _logger;
    private readonly IValidator<GetTournamentsQuery> _validator;

    public GetTournamentsFunction(
        ITournamentService tournamentService,
        IValidator<GetTournamentsQuery> validator,
        ILogger<GetTournamentsFunction> logger)
    {
        _tournamentService = tournamentService;
        _validator = validator;
        _logger = logger;
    }

    [Function("GetTournaments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournaments")] 
        HttpRequestData req)
    {
        _logger.LogInformation("Processing get tournaments request");

        // Parse and validate query parameters
        var queryString = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var query = GetTournamentsQuery.Parse(queryString);

        // Validate query
        var validationResult = await _validator.ValidateAsync(query);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            throw new Exceptions.ValidationException("Query validation failed", errors);
        }

        _logger.LogInformation(
            "Querying tournaments: startDateFrom={StartDateFrom}, startDateTo={StartDateTo}, location={Location}, division={Division}, status={Status}, maxResults={MaxResults}",
            query.StartDateFrom, query.StartDateTo, query.Location, query.Division, query.Status, query.MaxResults);

        // Get tournaments
        var tournaments = await _tournamentService.GetTournamentsAsync(
            query.StartDateFrom, 
            query.StartDateTo, 
            query.Location, 
            query.Division, 
            query.Status, 
            query.MaxResults ?? 100);

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
            Warmup = t.Warmup,
            Status = t.Status.ToString(),
            GameCount = t.Games?.Count ?? 0,
            CreatedDate = t.CreatedDate,
            Rounds = t.Rounds?.Select(r => new TournamentRoundDto
            {
                RoundNumber = r.RoundNumber,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                GameCount = r.GameCount
            }).ToList() ?? new List<TournamentRoundDto>()
        }).ToList();

        _logger.LogInformation("Returning {Count} tournaments", tournamentDtos.Count);

        // Return unified response
        var apiResponse = ApiResponse<List<TournamentListDto>>.Ok(
            tournamentDtos, 
            $"Retrieved {tournamentDtos.Count} tournament(s)");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(apiResponse);
        return response;
    }
}
