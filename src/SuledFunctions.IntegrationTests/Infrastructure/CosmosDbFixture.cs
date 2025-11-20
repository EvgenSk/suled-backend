using Microsoft.Azure.Cosmos;
using Testcontainers.CosmosDb;

namespace SuledFunctions.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture for Cosmos DB emulator using Testcontainers
/// </summary>
public class CosmosDbFixture : IAsyncLifetime
{
    private readonly CosmosDbContainer _cosmosContainer = new CosmosDbBuilder()
        .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
        .Build();

    public string ConnectionString => _cosmosContainer.GetConnectionString();
    public CosmosClient CosmosClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _cosmosContainer.StartAsync();
        
        var clientOptions = new CosmosClientOptions
        {
            // Accept self-signed certificates for local emulator
            HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            }),
            ConnectionMode = ConnectionMode.Gateway,
            RequestTimeout = TimeSpan.FromSeconds(30)
        };
        
        CosmosClient = new CosmosClient(ConnectionString, clientOptions);
        
        // Wait for Cosmos DB to be fully ready by attempting to list databases
        // CosmosDB emulator can take 60-120 seconds to fully initialize
        var maxRetries = 60;
        var retryDelay = 2000; // 2 seconds
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var iterator = CosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
                await iterator.ReadNextAsync();
                break; // Success
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1)
                {
                    throw new TimeoutException(
                        $"CosmosDB emulator failed to become ready after {maxRetries * retryDelay / 1000} seconds. " +
                        $"Last error: {ex.Message}", ex);
                }
                await Task.Delay(retryDelay);
            }
        }
    }

    public async Task DisposeAsync()
    {
        CosmosClient?.Dispose();
        await _cosmosContainer.DisposeAsync();
    }

    /// <summary>
    /// Create a test database
    /// </summary>
    public async Task<Database> CreateDatabaseAsync(string databaseId)
    {
        var response = await CosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
        return response.Database;
    }

    /// <summary>
    /// Create a test container
    /// </summary>
    public async Task<Container> CreateContainerAsync(string databaseId, string containerId, string partitionKeyPath)
    {
        var database = await CreateDatabaseAsync(databaseId);
        var containerResponse = await database.CreateContainerIfNotExistsAsync(containerId, partitionKeyPath);
        return containerResponse.Container;
    }

    /// <summary>
    /// Clean up all databases (useful between tests)
    /// </summary>
    public async Task CleanupDatabasesAsync()
    {
        using var iterator = CosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
        while (iterator.HasMoreResults)
        {
            var databases = await iterator.ReadNextAsync();
            foreach (var database in databases)
            {
                await CosmosClient.GetDatabase(database.Id).DeleteAsync();
            }
        }
    }
}
