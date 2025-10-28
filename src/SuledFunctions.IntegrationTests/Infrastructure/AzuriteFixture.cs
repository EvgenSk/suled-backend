using Azure.Storage.Blobs;
using Docker.DotNet.Models;
using Testcontainers.Azurite;

namespace SuledFunctions.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture for Azurite (local Azure Storage emulator) using Testcontainers
/// </summary>
public class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:3.34.0")
        .WithCreateParameterModifier(parameters =>
        {
            // Add --skipApiVersionCheck to the command arguments
            if (parameters.Cmd != null)
            {
                var cmdList = parameters.Cmd.ToList();
                cmdList.Add("--skipApiVersionCheck");
                parameters.Cmd = cmdList;
            }
        })
        .Build();

    public string ConnectionString => _azuriteContainer.GetConnectionString();
    public BlobServiceClient BlobServiceClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _azuriteContainer.StartAsync();
        BlobServiceClient = new BlobServiceClient(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _azuriteContainer.DisposeAsync();
    }

    /// <summary>
    /// Create a test blob container
    /// </summary>
    public async Task<BlobContainerClient> CreateContainerAsync(string containerName)
    {
        var containerClient = BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();
        return containerClient;
    }

    /// <summary>
    /// Clean up all containers (useful between tests)
    /// </summary>
    public async Task CleanupContainersAsync()
    {
        await foreach (var container in BlobServiceClient.GetBlobContainersAsync())
        {
            await BlobServiceClient.DeleteBlobContainerAsync(container.Name);
        }
    }
}
