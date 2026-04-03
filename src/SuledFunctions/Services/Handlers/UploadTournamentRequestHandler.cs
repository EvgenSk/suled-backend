using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using SuledFunctions.Common;
using SuledFunctions.Configuration;
using SuledFunctions.Exceptions;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services.Handlers;

public class UploadTournamentRequestHandler(
    ITournamentUploadService uploadService,
    IOptions<TournamentSettings> settings,
    ILogger<UploadTournamentRequestHandler> logger) : IUploadTournamentRequestHandler
{
    private readonly TournamentSettings _settings = settings.Value;

    public async Task<HttpResponseData> HandleAsync(HttpRequestData req)
    {
        logger.LogInformation("Processing tournament upload request");

        ValidateContentType(req);

        var (body, multipartFileName) = await ReadMultipartFileAsync(req);
        using (body)
        {
            var fileName = multipartFileName ?? ExtractFileName(req);
            logger.LogInformation("Processing file: {FileName}", fileName);

            try
            {
                var result = await uploadService.UploadAsync(body, fileName);
                logger.LogInformation("Tournament {TournamentId} saved with {PairCount} pairs", result.Id, result.PairCount);
                return await BuildResponseAsync(req, result);
            }
            catch (Exception ex) when (ex is not AppException)
            {
                logger.LogError(ex, "Error parsing or saving tournament");
                throw new FileProcessingException(Constants.ErrorMessages.ProcessingError, ex, fileName);
            }
        }
    }

    private void ValidateContentType(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues(Constants.Http.HeaderContentType, out var contentTypeValues))
        {
            logger.LogWarning("Missing Content-Type header");
            throw new ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.MissingContentType);
        }

        var contentType = contentTypeValues.FirstOrDefault();
        if (string.IsNullOrEmpty(contentType) || !contentType.Contains(Constants.Http.ContentTypeMultipartFormData, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
            throw new ValidationException(Constants.Http.HeaderContentType, Constants.ErrorMessages.InvalidContentType);
        }
    }

    private async Task<(MemoryStream Content, string? FileName)> ReadMultipartFileAsync(HttpRequestData req)
    {
        req.Headers.TryGetValues(Constants.Http.HeaderContentType, out var contentTypeValues);
        var contentType = contentTypeValues?.FirstOrDefault() ?? string.Empty;
        var boundary = GetMultipartBoundary(contentType);

        var memoryStream = new MemoryStream();
        string? extractedFileName = null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds));
        try
        {
            var reader = new MultipartReader(boundary, req.Body);
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(cts.Token)) != null)
            {
                if (string.IsNullOrEmpty(section.ContentDisposition))
                    continue;

                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(
                    section.ContentDisposition, @"filename=""?([^"";\r\n]+)""?",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (fileNameMatch.Success)
                    extractedFileName = fileNameMatch.Groups[1].Value.Trim();

                await section.Body.CopyToAsync(memoryStream, cts.Token);
                break; // only the first file section is used
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Timeout reading request body");
            throw new FileProcessingException(Constants.ErrorMessages.RequestTimeout);
        }

        if (memoryStream.Length == 0)
            throw new ValidationException("file", "No file content found in the request.");

        logger.LogInformation("Received {ByteCount} bytes", memoryStream.Length);
        memoryStream.Position = 0;
        return (memoryStream, extractedFileName);
    }

    private static string GetMultipartBoundary(string contentType)
    {
        var elements = contentType.Split(';');
        var boundaryElement = elements
            .FirstOrDefault(e => e.TrimStart().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase));
        return boundaryElement?.Trim()["boundary=".Length..].Trim('"') ?? string.Empty;
    }

    private static async Task<HttpResponseData> BuildResponseAsync(HttpRequestData req, TournamentUploadResult result)
    {
        var apiResponse = CreatedResponse<dynamic>.Created(
            result.Id,
            new { id = result.Id, name = result.Name, gameCount = result.GameCount, pairCount = result.PairCount },
            Constants.SuccessMessages.TournamentUploaded);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(apiResponse);
        return response;
    }

    private static string ExtractFileName(HttpRequestData req)
    {
        if (req.Headers.TryGetValues(Constants.Http.HeaderContentDisposition, out var dispositionValues))
        {
            var contentDisposition = dispositionValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(
                    contentDisposition, @"filename=""?([^""]+)""?");
                if (fileNameMatch.Success)
                    return fileNameMatch.Groups[1].Value;
            }
        }
        return Constants.Files.DefaultTournamentFileName;
    }
}
