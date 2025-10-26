using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services;

namespace SuledFunctions.Functions;

/// <summary>
/// HTTP Function to upload tournament Excel file
/// </summary>
public class UploadTournamentFunction
{
    private readonly ILogger<UploadTournamentFunction> _logger;
    private readonly IExcelParserService _excelParser;

    public UploadTournamentFunction(
        ILogger<UploadTournamentFunction> logger,
        IExcelParserService excelParser)
    {
        _logger = logger;
        _excelParser = excelParser;
    }

    [Function("UploadTournament")]
    [CosmosDBOutput(
        databaseName: "%CosmosDbName%",
        containerName: "%CosmosContainerName%",
        Connection = "CosmosDbConnection",
        CreateIfNotExists = true)]
    public async Task<object> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournament/upload")] 
        HttpRequestData req)
    {
        _logger.LogInformation("Processing tournament upload request");

        try
        {
            // Check if request contains multipart form data
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues) ||
                !contentTypeValues.Any(ct => ct.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { error = "Request must be multipart/form-data" });
                return errorResponse;
            }

            // Read the file from the request
            using var memoryStream = new MemoryStream();
            await req.Body.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Parse the file name from content disposition header
            var fileName = "tournament.xlsx";
            if (req.Headers.TryGetValues("Content-Disposition", out var dispositionValues))
            {
                var disposition = dispositionValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(disposition))
                {
                    var fileNameMatch = System.Text.RegularExpressions.Regex.Match(
                        disposition, 
                        @"filename=""?([^""]+)""?");
                    if (fileNameMatch.Success)
                    {
                        fileName = fileNameMatch.Groups[1].Value;
                    }
                }
            }

            // Parse the tournament
            var tournament = await _excelParser.ParseTournamentAsync(memoryStream, fileName);

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                id = tournament.Id,
                name = tournament.Name,
                gameCount = tournament.Games.Count,
                message = "Tournament uploaded successfully"
            });

            // Return both response and tournament for Cosmos DB output binding
            return new
            {
                HttpResponse = response,
                Document = tournament
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading tournament");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to process tournament file" });
            return errorResponse;
        }
    }
}
