using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.Functions;

/// <summary>
/// Blob trigger function to process tournament Excel files uploaded to storage
/// </summary>
public class ProcessTournamentBlobFunction
{
    private readonly ILogger<ProcessTournamentBlobFunction> _logger;
    private readonly IExcelParserService _excelParser;

    public ProcessTournamentBlobFunction(
        ILogger<ProcessTournamentBlobFunction> logger,
        IExcelParserService excelParser)
    {
        _logger = logger;
        _excelParser = excelParser;
    }

    [Function("ProcessTournamentBlob")]
    [CosmosDBOutput(
        databaseName: "%CosmosDbName%",
        containerName: "%CosmosContainerName%",
        Connection = "CosmosDbConnection",
        CreateIfNotExists = true)]
    public async Task<Tournament> Run(
        [BlobTrigger("tournaments/{name}", 
            Connection = "AzureWebJobsStorage")] 
        Stream blobStream,
        string name)
    {
        _logger.LogInformation("Processing blob: {BlobName}, Size: {Size} bytes", 
            name, blobStream.Length);

        try
        {
            // Parse the tournament from the blob
            var tournament = await _excelParser.ParseTournamentAsync(blobStream, name);
            
            _logger.LogInformation("Successfully processed tournament {TournamentId} with {GameCount} games",
                tournament.Id, tournament.Games.Count);

            // Return tournament to be saved to Cosmos DB via output binding
            return tournament;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing blob {BlobName}", name);
            throw;
        }
    }
}
