using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using SuledFunctions.Services;
using Microsoft.Azure.Cosmos;

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
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tournament/upload")] 
        HttpRequestData req,
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection")] 
        CosmosClient cosmosClient)
    {
        _logger.LogInformation("Processing tournament upload request");

        try
        {
            // Validate Content-Type header
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
            {
                _logger.LogWarning("Missing Content-Type header");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { error = "Content-Type header is required" });
                return errorResponse;
            }

            var contentType = contentTypeValues.FirstOrDefault();
            if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid Content-Type: {ContentType}", contentType);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { error = "Content-Type must be multipart/form-data" });
                return errorResponse;
            }

            _logger.LogInformation("Request received");
            
            using var memoryStream = new MemoryStream();
            
            _logger.LogInformation("Reading file from request body");
            
            // Set a timeout for reading
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await req.Body.CopyToAsync(memoryStream, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Timeout reading request body");
                var timeoutResponse = req.CreateResponse(HttpStatusCode.RequestTimeout);
                await timeoutResponse.WriteAsJsonAsync(new { error = "Request timeout while reading file" });
                return timeoutResponse;
            }
            
            _logger.LogInformation("Received {ByteCount} bytes", memoryStream.Length);
            
            if (memoryStream.Length == 0)
            {
                _logger.LogWarning("Empty request body");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { error = "No file data received" });
                return errorResponse;
            }
            
            memoryStream.Position = 0;

            // Extract filename from Content-Disposition header if present
            var fileName = "tournament.xlsx";
            if (req.Headers.TryGetValues("Content-Disposition", out var dispositionValues))
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
            
            // Parse the tournament
            var tournament = await _excelParser.ParseTournamentAsync(memoryStream, fileName);

            // Save to Cosmos DB
            var database = cosmosClient.GetDatabase(Environment.GetEnvironmentVariable("CosmosDbName"));
            var container = database.GetContainer(Environment.GetEnvironmentVariable("CosmosContainerName"));
            await container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

            _logger.LogInformation("Tournament {TournamentId} saved to Cosmos DB with {GameCount} games",
                tournament.Id, tournament.Games.Count);

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                id = tournament.Id,
                name = tournament.Name,
                gameCount = tournament.Games.Count,
                message = "Tournament uploaded successfully"
            });

            return response;
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
