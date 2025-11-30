using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using SuledFunctions.Common;
using SuledFunctions.Configuration;
using SuledFunctions.Exceptions;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Repositories;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Validators;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to upload tournament Excel file
/// </summary>
public class UploadTournamentFunction
{
    private readonly IExcelParserService _excelParser;
    private readonly ITournamentRepository _repository;
    private readonly ILogger<UploadTournamentFunction> _logger;
    private readonly TournamentSettings _settings;
    private readonly IValidator<Stream> _fileValidator;

    public UploadTournamentFunction(
        IExcelParserService excelParser,
        ITournamentRepository repository,
        IOptions<TournamentSettings> settings,
        IValidator<Stream> fileValidator,
        ILogger<UploadTournamentFunction> logger)
    {
        _excelParser = excelParser;
        _repository = repository;
        _logger = logger;
        _settings = settings.Value;
        _fileValidator = fileValidator;
    }

    [Function("UploadTournament")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tournament/upload")] 
        HttpRequestData req)
    {
        _logger.LogInformation("Processing tournament upload request");

        // Validate Content-Type header
        if (!req.Headers.TryGetValues(Constants.Http.HeaderContentType, out var contentTypeValues))
        {
            _logger.LogWarning("Missing Content-Type header");
            throw new Exceptions.ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.MissingContentType);
        }

        var contentType = contentTypeValues.FirstOrDefault();
        if (string.IsNullOrEmpty(contentType) || !contentType.Contains(Constants.Http.ContentTypeMultipartFormData, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
            throw new Exceptions.ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.InvalidContentType);
        }

        _logger.LogInformation("Request received");
        
        using var memoryStream = new MemoryStream();
        
        _logger.LogInformation("Reading file from request body");
        
        // Set a timeout for reading
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds));
        try
        {
            await req.Body.CopyToAsync(memoryStream, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout reading request body");
            throw new FileProcessingException(Constants.ErrorMessages.RequestTimeout);
        }
        
        _logger.LogInformation("Received {ByteCount} bytes", memoryStream.Length);
        
        memoryStream.Position = 0;

        // Validate file using FileUploadValidator
        var validationResult = await _fileValidator.ValidateAsync(memoryStream);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            throw new Exceptions.ValidationException("File validation failed", errors);
        }

        // Extract filename from Content-Disposition header if present
        var fileName = ExtractFileName(req);
        _logger.LogInformation("Processing file: {FileName}", fileName);

        _logger.LogInformation("Parsing tournament from file");
        
        try
        {
            // Parse the tournament
            var tournament = await _excelParser.ParseTournamentAsync(memoryStream, fileName);

            // Convert to compact format for storage (80-94% size reduction)
            var compactTournament = TournamentCompactMapper.ToCompact(tournament);

            // Save compact format using repository
            await _repository.CreateAsync(compactTournament);

            _logger.LogInformation("Tournament {TournamentId} saved to Cosmos DB in compact format with {PairCount} pairs",
                tournament.Id, tournament.Pairs.Count);

            // Create success response using unified response model
            var responseData = new
            {
                id = tournament.Id,
                name = tournament.Name,
                gameCount = tournament.Pairs.Sum(p => p.Games.Count),
                pairCount = tournament.Pairs.Count
            };

            var apiResponse = CreatedResponse<dynamic>.Created(
                tournament.Id,
                responseData,
                Constants.SuccessMessages.TournamentUploaded);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(apiResponse);
            return response;
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "Error parsing or saving tournament");
            throw new FileProcessingException(Constants.ErrorMessages.ProcessingError, ex, fileName);
        }
    }

    private string ExtractFileName(HttpRequestData req)
    {
        if (req.Headers.TryGetValues(Constants.Http.HeaderContentDisposition, out var dispositionValues))
        {
            var contentDisposition = dispositionValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                // Extract filename from Content-Disposition header (e.g., "attachment; filename="my-file.xlsx"")
                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(contentDisposition, @"filename=""?([^""]+)""?");
                if (fileNameMatch.Success)
                {
                    return fileNameMatch.Groups[1].Value;
                }
            }
        }
        return Constants.Files.DefaultTournamentFileName;
    }
}
