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
using SuledFunctions.Models.DTOs;
using SuledFunctions.Validators;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to upload tournament Excel file
/// </summary>
public class UploadTournamentFunction(
    ITournamentUploadService uploadService,
    IOptions<TournamentSettings> settings,
    IValidator<Stream> fileValidator,
    ILogger<UploadTournamentFunction> logger)
{
    private readonly TournamentSettings _settings = settings.Value;

    [Function("UploadTournament")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tournament/upload")] 
        HttpRequestData req)
    {
        logger.LogInformation("Processing tournament upload request");

        // Validate Content-Type header
        if (!req.Headers.TryGetValues(Constants.Http.HeaderContentType, out var contentTypeValues))
        {
            logger.LogWarning("Missing Content-Type header");
            throw new Exceptions.ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.MissingContentType);
        }

        var contentType = contentTypeValues.FirstOrDefault();
        if (string.IsNullOrEmpty(contentType) || !contentType.Contains(Constants.Http.ContentTypeMultipartFormData, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
            throw new Exceptions.ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.InvalidContentType);
        }

        logger.LogInformation("Request received");
        
        using var memoryStream = new MemoryStream();
        
        logger.LogInformation("Reading file from request body");
        
        // Set a timeout for reading
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds));
        try
        {
            await req.Body.CopyToAsync(memoryStream, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Timeout reading request body");
            throw new FileProcessingException(Constants.ErrorMessages.RequestTimeout);
        }
        
        logger.LogInformation("Received {ByteCount} bytes", memoryStream.Length);
        
        memoryStream.Position = 0;

        // Validate file using FileUploadValidator
        var validationResult = await fileValidator.ValidateAsync(memoryStream);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            throw new Exceptions.ValidationException("File validation failed", errors);
        }

        // Extract filename from Content-Disposition header if present
        var fileName = ExtractFileName(req);
        logger.LogInformation("Processing file: {FileName}", fileName);

        // Validate file extension (.xlsx / .xls only)
        if (!FileUploadValidator.ValidateFileExtension(fileName))
        {
            throw new Exceptions.ValidationException("fileName", Constants.ErrorMessages.InvalidFileType);
        }

        logger.LogInformation("Parsing tournament from file");
        
        try
        {
            var result = await uploadService.UploadAsync(memoryStream, fileName);

            logger.LogInformation("Tournament {TournamentId} saved with {PairCount} pairs",
                result.Id, result.PairCount);

            var responseData = new
            {
                id = result.Id,
                name = result.Name,
                gameCount = result.GameCount,
                pairCount = result.PairCount
            };

            var apiResponse = CreatedResponse<dynamic>.Created(
                result.Id,
                responseData,
                Constants.SuccessMessages.TournamentUploaded);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(apiResponse);
            return response;
        }
        catch (Exception ex) when (ex is not AppException)
        {
            logger.LogError(ex, "Error parsing or saving tournament");
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
