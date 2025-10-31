using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SuledFunctions.Models;

namespace SuledFunctions.Services;

/// <summary>
/// Service for managing tournaments in Cosmos DB
/// </summary>
public class TournamentService : ITournamentService
{
    private readonly ILogger<TournamentService> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;

    public TournamentService(
        ILogger<TournamentService> logger,
        CosmosClient cosmosClient)
    {
        _logger = logger;
        _cosmosClient = cosmosClient;
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
            
            // Build query
            var queryText = "SELECT * FROM c WHERE 1=1";
            var queryDefinition = new QueryDefinition(queryText);
            
            // Add filters
            var whereClauses = new List<string>();
            
            if (startDateFrom.HasValue)
            {
                whereClauses.Add("c.StartDate >= @startDateFrom");
                queryDefinition = queryDefinition.WithParameter("@startDateFrom", startDateFrom.Value);
            }
            
            if (startDateTo.HasValue)
            {
                whereClauses.Add("c.StartDate <= @startDateTo");
                queryDefinition = queryDefinition.WithParameter("@startDateTo", startDateTo.Value);
            }
            
            if (!string.IsNullOrWhiteSpace(location))
            {
                whereClauses.Add("CONTAINS(LOWER(c.Location), @location)");
                queryDefinition = queryDefinition.WithParameter("@location", location.ToLower());
            }
            
            if (!string.IsNullOrWhiteSpace(division))
            {
                whereClauses.Add("CONTAINS(LOWER(c.Division), @division)");
                queryDefinition = queryDefinition.WithParameter("@division", division.ToLower());
            }
            
            if (status.HasValue)
            {
                whereClauses.Add("c.Status = @status");
                queryDefinition = queryDefinition.WithParameter("@status", (int)status.Value);
            }
            
            if (whereClauses.Any())
            {
                queryText = $"SELECT * FROM c WHERE {string.Join(" AND ", whereClauses)}";
                queryDefinition = new QueryDefinition(queryText);
                
                // Re-add all parameters
                if (startDateFrom.HasValue)
                    queryDefinition = queryDefinition.WithParameter("@startDateFrom", startDateFrom.Value);
                if (startDateTo.HasValue)
                    queryDefinition = queryDefinition.WithParameter("@startDateTo", startDateTo.Value);
                if (!string.IsNullOrWhiteSpace(location))
                    queryDefinition = queryDefinition.WithParameter("@location", location.ToLower());
                if (!string.IsNullOrWhiteSpace(division))
                    queryDefinition = queryDefinition.WithParameter("@division", division.ToLower());
                if (status.HasValue)
                    queryDefinition = queryDefinition.WithParameter("@status", (int)status.Value);
            }
            
            queryText += " ORDER BY c.StartDate DESC";
            queryDefinition = new QueryDefinition(queryText);
            
            // Re-add parameters one more time for the final query
            if (startDateFrom.HasValue)
                queryDefinition = queryDefinition.WithParameter("@startDateFrom", startDateFrom.Value);
            if (startDateTo.HasValue)
                queryDefinition = queryDefinition.WithParameter("@startDateTo", startDateTo.Value);
            if (!string.IsNullOrWhiteSpace(location))
                queryDefinition = queryDefinition.WithParameter("@location", location.ToLower());
            if (!string.IsNullOrWhiteSpace(division))
                queryDefinition = queryDefinition.WithParameter("@division", division.ToLower());
            if (status.HasValue)
                queryDefinition = queryDefinition.WithParameter("@status", (int)status.Value);

            var tournaments = new List<Tournament>();
            using var iterator = container.GetItemQueryIterator<Tournament>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { MaxItemCount = maxResults });

            while (iterator.HasMoreResults && tournaments.Count < maxResults)
            {
                var response = await iterator.ReadNextAsync();
                tournaments.AddRange(response);
            }

            _logger.LogInformation("Retrieved {Count} tournaments", tournaments.Count);
            return tournaments;
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
