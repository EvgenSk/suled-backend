using Microsoft.Azure.Cosmos;

namespace SuledFunctions.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture for connecting to a locally running Cosmos DB Emulator
/// This fixture assumes the emulator is already running at localhost:8081
/// </summary>
public class LocalCosmosDbFixture : IAsyncLifetime
{
    // Standard Cosmos DB Emulator connection details
    private const string EmulatorEndpoint = "https://localhost:8081";
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    
    public CosmosClient CosmosClient { get; private set; } = null!;
    public string ConnectionString => $"AccountEndpoint={EmulatorEndpoint};AccountKey={EmulatorKey}";

    public async Task InitializeAsync()
    {
        var clientOptions = new CosmosClientOptions
        {
            // Accept self-signed certificates for local emulator
            HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            }),
            ConnectionMode = ConnectionMode.Gateway,
            RequestTimeout = TimeSpan.FromSeconds(30),
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };
        
        CosmosClient = new CosmosClient(ConnectionString, clientOptions);
        
        // Verify connection to emulator
        try
        {
            using var iterator = CosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
            await iterator.ReadNextAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to Cosmos DB Emulator. " +
                "Make sure the emulator is running at https://localhost:8081. " +
                "You can start it manually or use the setup-cosmos-emulator.ps1 script.", ex);
        }
    }

    public Task DisposeAsync()
    {
        CosmosClient?.Dispose();
        return Task.CompletedTask;
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
    /// Delete a test database
    /// </summary>
    public async Task DeleteDatabaseAsync(string databaseId)
    {
        try
        {
            await CosmosClient.GetDatabase(databaseId).DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Database doesn't exist, nothing to delete
        }
    }

    /// <summary>
    /// Delete all items in a container (useful for cleanup between tests)
    /// </summary>
    public async Task CleanupContainerAsync(string databaseId, string containerId)
    {
        try
        {
            var container = CosmosClient.GetContainer(databaseId, containerId);
            
            // Query all items
            var query = "SELECT c.id, c._partitionKey FROM c";
            using var iterator = container.GetItemQueryIterator<dynamic>(query);
            
            var deleteTasks = new List<Task>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    string id = item.id;
                    // Use the id as partition key if _partitionKey is not available
                    var partitionKey = new PartitionKey(id);
                    deleteTasks.Add(container.DeleteItemAsync<dynamic>(id, partitionKey));
                }
            }
            
            await Task.WhenAll(deleteTasks);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Container doesn't exist, nothing to cleanup
        }
    }
}
