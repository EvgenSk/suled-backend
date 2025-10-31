using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.IntegrationTests.Infrastructure;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.IntegrationTests.TournamentMetadata;

/// <summary>
/// Integration tests for Tournament metadata querying
/// Tests TournamentService with real Cosmos DB
/// </summary>
[Collection("CosmosDb")]
public class TournamentServiceIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbFixture _fixture;
    private Container _container = null!;
    private TournamentService _service = null!;
    private const string DatabaseId = "SuledTournamentMetadataDb";
    private const string ContainerId = "tournaments";

    public TournamentServiceIntegrationTests(CosmosDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _container = await _fixture.CreateContainerAsync(DatabaseId, ContainerId, "/id");
        
        // Set environment variables for service
        Environment.SetEnvironmentVariable("CosmosDbName", DatabaseId);
        Environment.SetEnvironmentVariable("CosmosContainerName", ContainerId);
        
        // Create service with real container
        var logger = new Mock<ILogger<TournamentService>>();
        _service = new TournamentService(logger.Object, _fixture.CosmosClient);
        
        // Seed test data
        var tournaments = CreateTestTournaments();
        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetTournamentsAsync_WithNoFilters_ReturnsAllTournaments()
    {
        // Act
        var result = await _service.GetTournamentsAsync();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDateRangeFilter_ReturnsMatchingTournaments()
    {
        // Arrange
        var startDateFrom = new DateTime(2025, 2, 1);
        var startDateTo = new DateTime(2025, 2, 28);

        // Act
        var result = await _service.GetTournamentsAsync(
            startDateFrom: startDateFrom,
            startDateTo: startDateTo);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("February Tournament");
        result.First().StartDate.Should().BeOnOrAfter(startDateFrom);
        result.First().StartDate.Should().BeOnOrBefore(startDateTo);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithLocationFilter_ReturnsMatchingTournaments()
    {
        // Act
        var result = await _service.GetTournamentsAsync(location: "York");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Location == "New York");
        result.Should().Contain(t => t.Location == "York");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithLocationFilterCaseInsensitive_ReturnsMatches()
    {
        // Act
        var result = await _service.GetTournamentsAsync(location: "YORK");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDivisionFilter_ReturnsMatchingTournaments()
    {
        // Act
        var result = await _service.GetTournamentsAsync(division: "Pro");

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.Division.Contains("Pro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetTournamentsAsync_WithStatusFilter_ReturnsMatchingTournaments()
    {
        // Act
        var result = await _service.GetTournamentsAsync(status: TournamentStatus.InProgress);

        // Assert
        result.Should().HaveCountGreaterThan(0);
        result.Should().OnlyContain(t => t.Status == TournamentStatus.InProgress);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithMultipleFilters_ReturnsMatchingTournaments()
    {
        // Arrange
        var startDateFrom = new DateTime(2025, 1, 1);
        var startDateTo = new DateTime(2025, 12, 31);

        // Act
        var result = await _service.GetTournamentsAsync(
            startDateFrom: startDateFrom,
            startDateTo: startDateTo,
            division: "Pro",
            status: TournamentStatus.InProgress);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("January Tournament");
        result.First().Division.Should().Be("Pro");
        result.First().Status.Should().Be(TournamentStatus.InProgress);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithMaxResults_LimitsReturnedCount()
    {
        // Act
        var result = await _service.GetTournamentsAsync(maxResults: 2);

        // Assert
        result.Should().HaveCountLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetTournamentsAsync_OrdersByStartDateDescending()
    {
        // Act
        var result = await _service.GetTournamentsAsync();

        // Assert
        result.Should().BeInDescendingOrder(t => t.StartDate);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithNoMatchingFilters_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetTournamentsAsync(location: "NonExistentCity");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithValidId_ReturnsTournament()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var expectedId = tournaments.First().Id;

        // Act
        var result = await _service.GetTournamentByIdAsync(expectedId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expectedId);
        result.Name.Should().Be("January Tournament");
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _service.GetTournamentByIdAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTournamentsAsync_PartialLocationMatch_ReturnsMatches()
    {
        // Act
        var result = await _service.GetTournamentsAsync(location: "New");

        // Assert
        result.Should().HaveCount(1);
        result.First().Location.Should().Be("New York");
    }

    [Fact]
    public async Task GetTournamentsAsync_PartialDivisionMatch_ReturnsMatches()
    {
        // Act
        var result = await _service.GetTournamentsAsync(division: "Ama");

        // Assert
        result.Should().HaveCount(1);
        result.First().Division.Should().Be("Amateur");
    }

    [Fact]
    public async Task GetTournamentsAsync_DateRangeWithNoEndDate_FiltersCorrectly()
    {
        // Arrange
        var startDateFrom = new DateTime(2025, 3, 1);

        // Act
        var result = await _service.GetTournamentsAsync(startDateFrom: startDateFrom);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().OnlyContain(t => t.StartDate >= startDateFrom);
    }

    [Fact]
    public async Task GetTournamentsAsync_DateRangeWithNoStartDate_FiltersCorrectly()
    {
        // Arrange
        var startDateTo = new DateTime(2025, 2, 28);

        // Act
        var result = await _service.GetTournamentsAsync(startDateTo: startDateTo);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().OnlyContain(t => t.StartDate <= startDateTo);
    }

    [Fact]
    public async Task GetTournamentsAsync_IncludesAllMetadataFields()
    {
        // Act
        var result = await _service.GetTournamentsAsync(maxResults: 1);

        // Assert
        result.Should().HaveCount(1);
        var tournament = result.First();
        tournament.Id.Should().NotBeNullOrEmpty();
        tournament.Name.Should().NotBeNullOrEmpty();
        tournament.StartDate.Should().NotBeNull();
        tournament.Location.Should().NotBeNullOrEmpty();
        tournament.Division.Should().NotBeNullOrEmpty();
        tournament.Games.Should().NotBeNull();
    }

    // Helper method
    private List<Tournament> CreateTestTournaments()
    {
        return new List<Tournament>
        {
            new Tournament
            {
                Id = Guid.NewGuid().ToString(),
                Name = "January Tournament",
                BlobFileName = "jan.xlsx",
                Location = "New York",
                Division = "Pro",
                StartDate = new DateTime(2025, 1, 15),
                EndDate = new DateTime(2025, 1, 17),
                Status = TournamentStatus.InProgress,
                Description = "Winter championship",
                Games = new List<Game>()
            },
            new Tournament
            {
                Id = Guid.NewGuid().ToString(),
                Name = "February Tournament",
                BlobFileName = "feb.xlsx",
                Location = "York",
                Division = "Amateur",
                StartDate = new DateTime(2025, 2, 15),
                EndDate = new DateTime(2025, 2, 17),
                Status = TournamentStatus.Completed,
                Description = "Regional qualifier",
                Games = new List<Game>()
            },
            new Tournament
            {
                Id = Guid.NewGuid().ToString(),
                Name = "March Tournament",
                BlobFileName = "mar.xlsx",
                Location = "Boston",
                Division = "Open",
                StartDate = new DateTime(2025, 3, 15),
                EndDate = new DateTime(2025, 3, 17),
                Status = TournamentStatus.Upcoming,
                Description = "Spring classic",
                Games = new List<Game>()
            },
            new Tournament
            {
                Id = Guid.NewGuid().ToString(),
                Name = "April Tournament",
                BlobFileName = "apr.xlsx",
                Location = "Chicago",
                Division = "Pro",
                StartDate = new DateTime(2025, 4, 15),
                EndDate = new DateTime(2025, 4, 17),
                Status = TournamentStatus.InProgress,
                Description = "National championship",
                Games = new List<Game>()
            }
        };
    }
}
