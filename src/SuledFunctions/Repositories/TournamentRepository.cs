using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuledFunctions.Configuration;
using SuledFunctions.Models.Optimized;
using System.Net;

namespace SuledFunctions.Repositories;

/// <summary>
/// Cosmos DB implementation of tournament repository
/// </summary>
public class TournamentRepository : ITournamentRepository
{
    private readonly Container _container;
    private readonly ILogger<TournamentRepository> _logger;

    public TournamentRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<TournamentRepository> logger)
    {
        _logger = logger;
        var cosmosSettings = settings.Value;
        
        var database = cosmosClient.GetDatabase(cosmosSettings.DatabaseName);
        _container = database.GetContainer(cosmosSettings.ContainerName);
    }

    public async Task<List<TournamentCompact>> QueryAsync(
        TournamentQuerySpec querySpec, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var whereClauses = querySpec.BuildWhereClauses();
            
            // Build the complete query text. ORDER BY is pushed to Cosmos so the
            // MaxResults cutoff is applied to the already-sorted result set.
            var orderBy = "ORDER BY c.startDate DESC";
            var queryText = whereClauses.Any() 
                ? $"SELECT * FROM c WHERE {string.Join(" AND ", whereClauses)} {orderBy}"
                : $"SELECT * FROM c {orderBy}";
            
            // Create query definition and add all parameters
            var queryDefinition = new QueryDefinition(queryText);
            
            if (querySpec.StartDateFrom.HasValue)
                queryDefinition = queryDefinition.WithParameter("@startDateFrom", querySpec.StartDateFrom.Value);
            if (querySpec.StartDateTo.HasValue)
                queryDefinition = queryDefinition.WithParameter("@startDateTo", querySpec.StartDateTo.Value);
            if (!string.IsNullOrWhiteSpace(querySpec.Location))
                queryDefinition = queryDefinition.WithParameter("@location", querySpec.Location);
            if (!string.IsNullOrWhiteSpace(querySpec.Division))
                queryDefinition = queryDefinition.WithParameter("@division", querySpec.Division);

            _logger.LogInformation("Executing query: {Query}", queryText);

            var results = new List<TournamentCompact>();
            using var iterator = _container.GetItemQueryIterator<TournamentCompact>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { MaxItemCount = querySpec.MaxResults });

            while (iterator.HasMoreResults && results.Count < querySpec.MaxResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            _logger.LogInformation("Query returned {Count} tournaments", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying tournaments with spec: {@QuerySpec}", querySpec);
            throw;
        }
    }

    public async Task<TournamentCompact?> GetByIdAsync(
        string id, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Cross-partition query by id because the pk (year) is not known at call time.
            // The id field is indexed, so this remains a fast, single-document lookup.
            var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.id = @id")
                .WithParameter("@id", id);

            using var iterator = _container.GetItemQueryIterator<TournamentCompact>(
                query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

            if (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                var item = page.FirstOrDefault();
                if (item != null)
                {
                    _logger.LogInformation("Retrieved tournament {TournamentId}", id);
                    return item;
                }
            }

            _logger.LogWarning("Tournament {TournamentId} not found", id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tournament {TournamentId}", id);
            throw;
        }
    }

    public async Task<TournamentCompact> CreateAsync(
        TournamentCompact tournament, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.CreateItemAsync(
                tournament,
                new PartitionKey(tournament.Pk),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created tournament {TournamentId}", tournament.Id);
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tournament {TournamentId}", tournament.Id);
            throw;
        }
    }

    public async Task<TournamentCompact> UpdateAsync(
        TournamentCompact tournament, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReplaceItemAsync(
                tournament,
                tournament.Id,
                new PartitionKey(tournament.Pk),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Updated tournament {TournamentId}", tournament.Id);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Tournament {TournamentId} not found for update", tournament.Id);
            throw new InvalidOperationException($"Tournament {tournament.Id} not found", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tournament {TournamentId}", tournament.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        string id, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Look up the item first to retrieve its pk (year partition key).
            var item = await GetByIdAsync(id, cancellationToken);
            if (item == null)
            {
                _logger.LogWarning("Tournament {TournamentId} not found for deletion", id);
                return false;
            }

            await _container.DeleteItemAsync<TournamentCompact>(
                id,
                new PartitionKey(item.Pk),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted tournament {TournamentId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tournament {TournamentId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(
        string id, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryDefinition = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.id = @id")
                .WithParameter("@id", id);

            using var iterator = _container.GetItemQueryIterator<int>(queryDefinition);
            var response = await iterator.ReadNextAsync(cancellationToken);
            
            return response.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of tournament {TournamentId}", id);
            throw;
        }
    }
}
