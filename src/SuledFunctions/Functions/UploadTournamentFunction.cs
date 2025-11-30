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

    public UploadTournamentFunction(
        IExcelParserService excelParser,
        ITournamentRepository repository,
        IOptions<TournamentSettings> settings,
        ILogger<UploadTournamentFunction> logger)
    {
        _excelParser = excelParser;
        _repository = repository;
        _logger = logger;
        _settings = settings.Value;
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
            throw new ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.MissingContentType);
        }

        var contentType = contentTypeValues.FirstOrDefault();
        if (string.IsNullOrEmpty(contentType) || !contentType.Contains(Constants.Http.ContentTypeMultipartFormData, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
            throw new ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.InvalidContentType);
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
        
        if (memoryStream.Length == 0)
        {
            _logger.LogWarning("Empty request body");
            throw new ValidationException("file", Constants.ErrorMessages.EmptyFile);
        }

        if (memoryStream.Length > _settings.MaxUploadSizeBytes)
        {
            _logger.LogWarning("File size {ByteCount} exceeds maximum {MaxBytes}", memoryStream.Length, _settings.MaxUploadSizeBytes);
            throw new ValidationException("file", $"File size exceeds maximum allowed size of {_settings.MaxUploadSizeBytes / (1024 * 1024)} MB");
        }
        
        memoryStream.Position = 0;

        // Extract filename from Content-Disposition header if present
        var fileName = Constants.Files.DefaultTournamentFileName;
        if (req.Headers.TryGetValues(Constants.Http.HeaderContentDisposition, out var dispositionValues))
        {
            var contentDisposition = dispositionValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                // Extract filename from Content-Disposition header (e.g., "attachment; filename="my-file.xlsx"")
                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(contentDisposition, @"filename=""?([^""]+)""?");
                if (fileNameMatch.Success)
                {
                    fileName = fileNameMatch.Groups[1].Value;
                    _logger.LogInformation("Extracted filename from Content-Disposition: {FileName}", fileName);
                }
            }
        }

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

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                id = tournament.Id,
                name = tournament.Name,
                gameCount = tournament.Games.Count,
                message = Constants.SuccessMessages.TournamentUploaded
            });

            return response;
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "Error parsing or saving tournament");
            throw new FileProcessingException(Constants.ErrorMessages.ProcessingError, ex, fileName);
        }
    }
}
