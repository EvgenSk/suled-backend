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
            ConnectionMode = ConnectionMode.Gateway
        };
        
        CosmosClient = new CosmosClient(ConnectionString, clientOptions);
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
