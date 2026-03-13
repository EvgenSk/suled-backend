using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Configuration;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using System.Net;

namespace SuledFunctions.Tests.Repositories;

public class TournamentRepositoryTests
{
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<Database> _mockDatabase;
    private readonly Mock<Container> _mockContainer;
    private readonly Mock<ILogger<TournamentRepository>> _mockLogger;
    private readonly IOptions<CosmosDbSettings> _settings;
    private readonly TournamentRepository _repository;

    public TournamentRepositoryTests()
    {
        _mockCosmosClient = new Mock<CosmosClient>();
        _mockDatabase = new Mock<Database>();
        _mockContainer = new Mock<Container>();
        _mockLogger = new Mock<ILogger<TournamentRepository>>();

        _settings = Options.Create(new CosmosDbSettings
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "TestDb",
            ContainerName = "Tournaments"
        });

        _mockCosmosClient
            .Setup(c => c.GetDatabase(It.IsAny<string>()))
            .Returns(_mockDatabase.Object);

        _mockDatabase
            .Setup(d => d.GetContainer(It.IsAny<string>()))
            .Returns(_mockContainer.Object);

        _repository = new TournamentRepository(_mockCosmosClient.Object, _settings, _mockLogger.Object);
    }

    // Helper: create a mock FeedIterator that yields the given items once then has no more results.
    private static Mock<FeedIterator<TournamentCompact>> CreateMockIterator(
        IEnumerable<TournamentCompact> items)
    {
        var list = items.ToList();
        var mockResponse = new Mock<FeedResponse<TournamentCompact>>();
        mockResponse.Setup(r => r.GetEnumerator()).Returns(() => list.GetEnumerator());

        var mockIterator = new Mock<FeedIterator<TournamentCompact>>();
        mockIterator.SetupSequence(i => i.HasMoreResults).Returns(list.Count > 0).Returns(false);
        mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);
        return mockIterator;
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenTournamentNotFound()
    {
        // Arrange
        var tournamentId = "nonexistent-id";
        var mockIterator = CreateMockIterator(Enumerable.Empty<TournamentCompact>());
        _mockContainer
            .Setup(c => c.GetItemQueryIterator<TournamentCompact>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        // Act
        var result = await _repository.GetByIdAsync(tournamentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTournament_WhenFound()
    {
        // Arrange
        var tournamentId = "test-id";
        var expectedTournament = new TournamentCompact { Id = tournamentId, Name = "Test Tournament" };
        var mockIterator = CreateMockIterator(new[] { expectedTournament });
        _mockContainer
            .Setup(c => c.GetItemQueryIterator<TournamentCompact>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        // Act
        var result = await _repository.GetByIdAsync(tournamentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tournamentId, result.Id);
        Assert.Equal("Test Tournament", result.Name);
    }

    [Fact]
    public async Task CreateAsync_ReturnsTournament_WhenSuccessful()
    {
        // Arrange
        var tournament = new TournamentCompact
        {
            Id = "new-id",
            Pk = "2025",
            Name = "New Tournament"
        };

        var mockResponse = new Mock<ItemResponse<TournamentCompact>>();
        mockResponse.Setup(r => r.Resource).Returns(tournament);

        _mockContainer
            .Setup(c => c.CreateItemAsync(
                tournament,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _repository.CreateAsync(tournament);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tournament.Id, result.Id);
        _mockContainer.Verify(c => c.CreateItemAsync(
            It.IsAny<TournamentCompact>(),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsException_WhenTournamentNotFound()
    {
        // Arrange
        var tournament = new TournamentCompact
        {
            Id = "nonexistent-id",
            Name = "Test Tournament"
        };

        _mockContainer
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<TournamentCompact>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "", 0));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _repository.UpdateAsync(tournament));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenTournamentNotFound()
    {
        // Arrange: GetByIdAsync queries and finds nothing → DeleteAsync returns false
        var tournamentId = "nonexistent-id";
        var mockIterator = CreateMockIterator(Enumerable.Empty<TournamentCompact>());
        _mockContainer
            .Setup(c => c.GetItemQueryIterator<TournamentCompact>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        // Act
        var result = await _repository.DeleteAsync(tournamentId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenSuccessful()
    {
        // Arrange: GetByIdAsync finds the item, then DeleteItemAsync succeeds
        var tournamentId = "test-id";
        var existingItem = new TournamentCompact { Id = tournamentId, Pk = "2025" };
        var mockIterator = CreateMockIterator(new[] { existingItem });
        _mockContainer
            .Setup(c => c.GetItemQueryIterator<TournamentCompact>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        var mockDeleteResponse = new Mock<ItemResponse<TournamentCompact>>();
        _mockContainer
            .Setup(c => c.DeleteItemAsync<TournamentCompact>(
                tournamentId,
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResponse.Object);

        // Act
        var result = await _repository.DeleteAsync(tournamentId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TournamentQuerySpec_BuildsCorrectWhereClauses()
    {
        // Arrange
        var querySpec = new TournamentQuerySpec
        {
            StartDateFrom = new DateTime(2025, 1, 1),
            Location = "Test Location",
            Division = "A"
        };

        // Act
        var clauses = querySpec.BuildWhereClauses();

        // Assert
        Assert.Equal(3, clauses.Count);
        Assert.Contains("c.startDate >= @startDateFrom", clauses);
        Assert.Contains("CONTAINS(c.location, @location, true)", clauses);
        Assert.Contains("CONTAINS(c.division, @division, true)", clauses);
    }

    [Fact]
    public void TournamentQuerySpec_BuildsEmptyWhereClauses_WhenNoFilters()
    {
        // Arrange
        var querySpec = new TournamentQuerySpec();

        // Act
        var clauses = querySpec.BuildWhereClauses();

        // Assert
        Assert.Empty(clauses);
    }
}
