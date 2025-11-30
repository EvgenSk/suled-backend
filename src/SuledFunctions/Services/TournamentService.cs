using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Service for managing tournaments in Cosmos DB
/// </summary>
public class TournamentService : ITournamentService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private readonly ILogger<TournamentService> _logger;

    public TournamentService(
        CosmosClient cosmosClient,
        ILogger<TournamentService> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        _databaseName = Environment.GetEnvironmentVariable("CosmosDbName") 
            ?? throw new InvalidOperationException("CosmosDbName not configured");
        _containerName = Environment.GetEnvironmentVariable("CosmosContainerName") 
            ?? throw new InvalidOperationException("CosmosContainerName not configured");
    }

    public async Task<List<Tournament>> GetTournamentsAsync(
        DateTime? startDateFrom = null,
        DateTime? startDateTo = null,
        string? location = null,
        string? division = null,
        TournamentStatus? status = null,
        int maxResults = 100)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            
            // Build query with filters
            var whereClauses = new List<string>();
            
            if (startDateFrom.HasValue)
            {
                whereClauses.Add("c.startDate >= @startDateFrom");
            }
            
            if (startDateTo.HasValue)
            {
                whereClauses.Add("c.startDate <= @startDateTo");
            }
            
            if (!string.IsNullOrWhiteSpace(location))
            {
                whereClauses.Add("CONTAINS(c.location, @location, true)");
            }
            
            if (!string.IsNullOrWhiteSpace(division))
            {
                whereClauses.Add("CONTAINS(c.division, @division, true)");
            }
            
            if (status.HasValue)
            {
                whereClauses.Add("c.status = @status");
            }
            
            // Build the complete query text (without ORDER BY to avoid issues with nullable fields)
            var queryText = whereClauses.Any() 
                ? $"SELECT * FROM c WHERE {string.Join(" AND ", whereClauses)}"
                : "SELECT * FROM c";
            
            // Create query definition and add all parameters
            var queryDefinition = new QueryDefinition(queryText);
            
            if (startDateFrom.HasValue)
                queryDefinition = queryDefinition.WithParameter("@startDateFrom", startDateFrom.Value);
            if (startDateTo.HasValue)
                queryDefinition = queryDefinition.WithParameter("@startDateTo", startDateTo.Value);
            if (!string.IsNullOrWhiteSpace(location))
                queryDefinition = queryDefinition.WithParameter("@location", location);
            if (!string.IsNullOrWhiteSpace(division))
                queryDefinition = queryDefinition.WithParameter("@division", division);
            if (status.HasValue)
                queryDefinition = queryDefinition.WithParameter("@status", (int)status.Value);

            _logger.LogInformation("Executing query: {Query}", queryText);

            var tournaments = new List<Tournament>();
            using var iterator = container.GetItemQueryIterator<Tournament>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { MaxItemCount = maxResults });

            while (iterator.HasMoreResults && tournaments.Count < maxResults)
            {
                var response = await iterator.ReadNextAsync();
                tournaments.AddRange(response);
            }

            // Sort by StartDate in memory (descending - most recent first)
            var sortedTournaments = tournaments
                .OrderByDescending(t => t.StartDate ?? DateTime.MinValue)
                .Take(maxResults)
                .ToList();

            _logger.LogInformation("Retrieved {Count} tournaments", sortedTournaments.Count);
            return sortedTournaments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournaments");
            throw;
        }
    }

    public async Task<Tournament?> GetTournamentByIdAsync(string id)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            var response = await container.ReadItemAsync<Tournament>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Tournament {TournamentId} not found", id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournament {TournamentId}", id);
            throw;
        }
    }
}
