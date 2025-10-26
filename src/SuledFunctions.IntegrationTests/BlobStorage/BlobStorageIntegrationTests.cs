using Azure.Storage.Blobs;
using FluentAssertions;
using SuledFunctions.IntegrationTests.Infrastructure;

namespace SuledFunctions.IntegrationTests.BlobStorage;

/// <summary>
/// Integration tests for Azure Blob Storage operations
/// Tests upload, download, and blob metadata handling
/// </summary>
[Collection("Azurite")]
public class BlobStorageIntegrationTests
{
    private readonly AzuriteFixture _fixture;
    private readonly BlobContainerClient _containerClient;

    public BlobStorageIntegrationTests(AzuriteFixture fixture)
    {
        _fixture = fixture;
        _containerClient = _fixture.CreateContainerAsync("tournaments").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task UploadBlob_WithValidData_ShouldSucceed()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var testData = "test blob content"u8.ToArray();
        var blobClient = _containerClient.GetBlobClient(blobName);

        // Act
        await using var stream = new MemoryStream(testData);
        await blobClient.UploadAsync(stream, overwrite: true);

        // Assert
        var exists = await blobClient.ExistsAsync();
        exists.Value.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadBlob_AfterUpload_ShouldReturnSameContent()
    {
        // Arrange
        var blobName = "download-test.xlsx";
        var expectedData = "download test content"u8.ToArray();
        var blobClient = _containerClient.GetBlobClient(blobName);

        await using var uploadStream = new MemoryStream(expectedData);
        await blobClient.UploadAsync(uploadStream, overwrite: true);

        // Act
        var downloadResponse = await blobClient.DownloadAsync();
        await using var downloadStream = new MemoryStream();
        await downloadResponse.Value.Content.CopyToAsync(downloadStream);
        var actualData = downloadStream.ToArray();

        // Assert
        actualData.Should().Equal(expectedData);
    }

    [Fact]
    public async Task ListBlobs_WithMultipleUploads_ShouldReturnAllBlobs()
    {
        // Arrange
        var blobNames = new[] { "blob1.xlsx", "blob2.xlsx", "blob3.xlsx" };
        foreach (var blobName in blobNames)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(new BinaryData("test"), overwrite: true);
        }

        // Act
        var blobs = new List<string>();
        await foreach (var blobItem in _containerClient.GetBlobsAsync())
        {
            blobs.Add(blobItem.Name);
        }

        // Assert
        blobs.Should().Contain(blobNames);
        blobs.Count.Should().BeGreaterThanOrEqualTo(blobNames.Length);
    }

    [Fact]
    public async Task DeleteBlob_AfterUpload_ShouldRemoveBlob()
    {
        // Arrange
        var blobName = "delete-test.xlsx";
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(new BinaryData("test"), overwrite: true);

        // Act
        await blobClient.DeleteAsync();

        // Assert
        var exists = await blobClient.ExistsAsync();
        exists.Value.Should().BeFalse();
    }

    [Fact]
    public async Task UploadBlob_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var blobName = "metadata-test.xlsx";
        var blobClient = _containerClient.GetBlobClient(blobName);
        var metadata = new Dictionary<string, string>
        {
            ["tournamentId"] = "test-123",
            ["uploadedBy"] = "integration-test"
        };

        // Act
        await blobClient.UploadAsync(
            new BinaryData("test"),
            options: new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                Metadata = metadata
            });

        // Assert
        var properties = await blobClient.GetPropertiesAsync();
        properties.Value.Metadata.Should().ContainKeys("tournamentId", "uploadedBy");
        properties.Value.Metadata["tournamentId"].Should().Be("test-123");
    }

    [Fact]
    public async Task UploadLargeBlob_ShouldHandleMultipartUpload()
    {
        // Arrange
        var blobName = "large-file.xlsx";
        var blobClient = _containerClient.GetBlobClient(blobName);
        var largeData = new byte[5 * 1024 * 1024]; // 5 MB
        new Random().NextBytes(largeData);

        // Act
        await using var stream = new MemoryStream(largeData);
        await blobClient.UploadAsync(stream, overwrite: true);

        // Assert
        var properties = await blobClient.GetPropertiesAsync();
        properties.Value.ContentLength.Should().Be(largeData.Length);
    }
}
