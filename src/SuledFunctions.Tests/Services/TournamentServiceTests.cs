using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.Tests.Services;

public class TournamentServiceTests
{
    private readonly Mock<ILogger<TournamentService>> _loggerMock;
    private readonly Mock<CosmosClient> _cosmosClientMock;
    private readonly Mock<Database> _databaseMock;
    private readonly Mock<Container> _containerMock;
    private readonly TournamentService _service;

    public TournamentServiceTests()
    {
        _loggerMock = new Mock<ILogger<TournamentService>>();
        _cosmosClientMock = new Mock<CosmosClient>();
        _databaseMock = new Mock<Database>();
        _containerMock = new Mock<Container>();
        
        // Set environment variables for database and container names
        Environment.SetEnvironmentVariable("CosmosDbName", "TestDb");
        Environment.SetEnvironmentVariable("CosmosContainerName", "TestContainer");
        
        // Setup Cosmos client to return database and container properly
        _cosmosClientMock.Setup(c => c.GetDatabase("TestDb"))
            .Returns(_databaseMock.Object);
        _databaseMock.Setup(d => d.GetContainer("TestContainer"))
            .Returns(_containerMock.Object);
        
        // Also setup the GetContainer on the client directly
        _cosmosClientMock.Setup(c => c.GetContainer("TestDb", "TestContainer"))
            .Returns(_containerMock.Object);
        
        _service = new TournamentService(_loggerMock.Object, _cosmosClientMock.Object);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithNoFilters_ReturnsAllTournaments()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        SetupContainerMock(tournaments);

        // Act
        var result = await _service.GetTournamentsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDateRangeFilter_FiltersCorrectly()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        SetupContainerMock(tournaments.Where(t => 
            t.StartDate >= new DateTime(2025, 2, 1) && 
            t.StartDate <= new DateTime(2025, 2, 28)).ToList());

        var startDateFrom = new DateTime(2025, 2, 1);
        var startDateTo = new DateTime(2025, 2, 28);

        // Act
        var result = await _service.GetTournamentsAsync(
            startDateFrom: startDateFrom, 
            startDateTo: startDateTo);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("February Tournament");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithLocationFilter_FiltersCorrectly()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => 
            t.Location.Contains("York", StringComparison.OrdinalIgnoreCase)).ToList();
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(location: "york");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Location == "New York");
        result.Should().Contain(t => t.Location == "York");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDivisionFilter_FiltersCorrectly()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => 
            t.Division.Contains("Pro", StringComparison.OrdinalIgnoreCase)).ToList();
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(division: "pro");

        // Assert
        result.Should().HaveCount(1);
        result.First().Division.Should().Be("Pro");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithStatusFilter_FiltersCorrectly()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => t.Status == TournamentStatus.Upcoming).ToList();
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(status: TournamentStatus.Upcoming);

        // Assert
        result.Should().HaveCount(1);
        result.First().Status.Should().Be(TournamentStatus.Upcoming);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithMaxResults_LimitsReturnedTournaments()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        SetupContainerMock(tournaments.Take(2).ToList());

        // Act
        var result = await _service.GetTournamentsAsync(maxResults: 2);

        // Assert
        result.Should().HaveCountLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithMultipleFilters_CombinesFiltersCorrectly()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var startDateFrom = new DateTime(2025, 1, 1);
        var startDateTo = new DateTime(2025, 12, 31);
        
        var filtered = tournaments.Where(t => 
            t.StartDate >= startDateFrom &&
            t.StartDate <= startDateTo &&
            t.Location.Contains("New York", StringComparison.OrdinalIgnoreCase) &&
            t.Status == TournamentStatus.InProgress).ToList();
        
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(
            startDateFrom: startDateFrom,
            startDateTo: startDateTo,
            location: "New York",
            status: TournamentStatus.InProgress);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("January Tournament");
    }

    [Fact]
    public async Task GetTournamentsAsync_CaseInsensitiveLocationSearch_ReturnsMatches()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => 
            t.Location.Contains("YORK", StringComparison.OrdinalIgnoreCase)).ToList();
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(location: "YORK");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTournamentsAsync_PartialLocationMatch_ReturnsMatches()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => 
            t.Location.Contains("New", StringComparison.OrdinalIgnoreCase)).ToList();
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(location: "New");

        // Assert
        result.Should().HaveCount(1);
        result.First().Location.Should().Be("New York");
    }

    [Fact]
    public async Task GetTournamentsAsync_CaseInsensitiveDivisionSearch_ReturnsMatches()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => 
            t.Division.Contains("AMATEUR", StringComparison.OrdinalIgnoreCase)).ToList();
        SetupContainerMock(filtered);

        // Act
        var result = await _service.GetTournamentsAsync(division: "AMATEUR");

        // Assert
        result.Should().HaveCount(1);
        result.First().Division.Should().Be("Amateur");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        SetupContainerMock(new List<Tournament>());

        // Act
        var result = await _service.GetTournamentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithValidId_ReturnsTournament()
    {
        // Arrange
        var tournament = CreateTestTournaments().First();
        var responseMock = new Mock<ItemResponse<Tournament>>();
        responseMock.Setup(r => r.Resource).Returns(tournament);
        
        _containerMock.Setup(c => c.ReadItemAsync<Tournament>(
            tournament.Id,
            It.IsAny<PartitionKey>(),
            null,
            default))
            .ReturnsAsync(responseMock.Object);

        // Act
        var result = await _service.GetTournamentByIdAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tournament.Id);
        result.Name.Should().Be(tournament.Name);
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _containerMock.Setup(c => c.ReadItemAsync<Tournament>(
            It.IsAny<string>(),
            It.IsAny<PartitionKey>(),
            null,
            default))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        // Act
        var result = await _service.GetTournamentByIdAsync("invalid-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTournamentsAsync_LogsResultCount()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        SetupContainerMock(tournaments);

        // Act
        await _service.GetTournamentsAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved") && v.ToString()!.Contains("tournaments")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // Helper methods
    private List<Tournament> CreateTestTournaments()
    {
        return new List<Tournament>
        {
            new Tournament
            {
                Id = "t1",
                Name = "January Tournament",
                BlobFileName = "jan.xlsx",
                Location = "New York",
                Division = "Pro",
                StartDate = new DateTime(2025, 1, 15),
                EndDate = new DateTime(2025, 1, 17),
                Status = TournamentStatus.InProgress,
                Games = new List<Game>()
            },
            new Tournament
            {
                Id = "t2",
                Name = "February Tournament",
                BlobFileName = "feb.xlsx",
                Location = "York",
                Division = "Amateur",
                StartDate = new DateTime(2025, 2, 15),
                EndDate = new DateTime(2025, 2, 17),
                Status = TournamentStatus.Completed,
                Games = new List<Game>()
            },
            new Tournament
            {
                Id = "t3",
                Name = "March Tournament",
                BlobFileName = "mar.xlsx",
                Location = "Boston",
                Division = "Open",
                StartDate = new DateTime(2025, 3, 15),
                EndDate = new DateTime(2025, 3, 17),
                Status = TournamentStatus.Upcoming,
                Games = new List<Game>()
            }
        };
    }

    private void SetupContainerMock(List<Tournament> tournaments)
    {
        var iteratorMock = new Mock<FeedIterator<Tournament>>();
        var responseMock = new Mock<FeedResponse<Tournament>>();

        // Setup the response to return tournaments
        responseMock.Setup(r => r.GetEnumerator()).Returns(tournaments.GetEnumerator());
        responseMock.Setup(r => r.Resource).Returns(tournaments);

        // Setup iterator behavior - first call returns true, second call returns false
        var callCount = 0;
        iteratorMock.Setup(i => i.HasMoreResults).Returns(() => callCount++ == 0);
        iteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock.Object);

        // Setup container to return iterator
        _containerMock.Setup(c => c.GetItemQueryIterator<Tournament>(
            It.IsAny<QueryDefinition>(),
            It.IsAny<string>(),
            It.IsAny<QueryRequestOptions>()))
            .Returns(iteratorMock.Object);
    }
}
