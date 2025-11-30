using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Configuration;
using SuledFunctions.IntegrationTests.Infrastructure;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services;

namespace SuledFunctions.IntegrationTests.CosmosDb;

/// <summary>
/// Integration tests for TournamentService using a locally running Cosmos DB Emulator
/// Tests the actual service implementation with real database operations
/// Prerequisites: Cosmos DB Emulator must be running at https://localhost:8081
/// </summary>
[Collection("LocalCosmosDb")]
public class TournamentServiceIntegrationTests : IAsyncLifetime
{
    private readonly LocalCosmosDbFixture _fixture;
    private TournamentService _tournamentService = null!;
    private TournamentRepository _tournamentRepository = null!;
    private Container _container = null!;
    private const string DatabaseName = "TournamentDb";
    private const string ContainerName = "Tournaments";

    public TournamentServiceIntegrationTests(LocalCosmosDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create test database and container
        _container = await _fixture.CreateContainerAsync(DatabaseName, ContainerName, "/id");

        // Create settings
        var cosmosSettings = Options.Create(new CosmosDbSettings
        {
            ConnectionString = "not-used-directly",
            DatabaseName = DatabaseName,
            ContainerName = ContainerName
        });

        var tournamentSettings = Options.Create(new TournamentSettings
        {
            MaxResultsDefault = 100
        });

        // Create repository and service with real CosmosClient
        var mockRepoLogger = new Mock<ILogger<TournamentRepository>>();
        var mockServiceLogger = new Mock<ILogger<TournamentService>>();
        
        _tournamentRepository = new TournamentRepository(_fixture.CosmosClient, cosmosSettings, mockRepoLogger.Object);
        _tournamentService = new TournamentService(_tournamentRepository, tournamentSettings, mockServiceLogger.Object);
    }

    public async Task DisposeAsync()
    {
        // Clean up test data
        await _fixture.CleanupContainerAsync(DatabaseName, ContainerName);
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithExistingTournament_ShouldReturnTournament()
    {
        // Arrange
        var tournament = CreateTestTournament("Test Tournament");
        await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Act
        var result = await _tournamentService.GetTournamentByIdAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tournament.Id);
        result.Name.Should().Be("Test Tournament");
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithNonExistingTournament_ShouldReturnNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid().ToString();

        // Act
        var result = await _tournamentService.GetTournamentByIdAsync(nonExistingId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTournamentsAsync_WithNoFilters_ShouldReturnAllTournaments()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("Tournament 1", new DateTime(2025, 12, 1)),
            CreateTestTournament("Tournament 2", new DateTime(2025, 12, 5)),
            CreateTestTournament("Tournament 3", new DateTime(2025, 12, 10))
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Should().Contain(t => t.Name == "Tournament 1");
        result.Should().Contain(t => t.Name == "Tournament 2");
        result.Should().Contain(t => t.Name == "Tournament 3");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDateFilter_ShouldReturnMatchingTournaments()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("Early Tournament", new DateTime(2025, 11, 15)),
            CreateTestTournament("Mid Tournament", new DateTime(2025, 12, 5)),
            CreateTestTournament("Late Tournament", new DateTime(2025, 12, 20))
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync(
            startDateFrom: new DateTime(2025, 12, 1),
            startDateTo: new DateTime(2025, 12, 15));

        // Assert
        result.Should().Contain(t => t.Name == "Mid Tournament");
        result.Should().NotContain(t => t.Name == "Early Tournament");
        result.Should().NotContain(t => t.Name == "Late Tournament");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithLocationFilter_ShouldReturnMatchingTournaments()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("NYC Tournament", location: "New York"),
            CreateTestTournament("LA Tournament", location: "Los Angeles"),
            CreateTestTournament("CHI Tournament", location: "Chicago")
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync(location: "angeles");

        // Assert
        result.Should().ContainSingle(t => t.Name == "LA Tournament");
        result.Should().NotContain(t => t.Name == "NYC Tournament");
        result.Should().NotContain(t => t.Name == "CHI Tournament");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDivisionFilter_ShouldReturnMatchingTournaments()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("Mixed Open", division: "Mixed Open"),
            CreateTestTournament("Men's A", division: "Men's A"),
            CreateTestTournament("Women's Open", division: "Women's Open")
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync(division: "open");

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().Contain(t => t.Name == "Mixed Open");
        result.Should().Contain(t => t.Name == "Women's Open");
        result.Should().NotContain(t => t.Name == "Men's A");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithStatusFilter_ShouldReturnMatchingTournaments()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("Scheduled Tournament", status: TournamentStatus.Upcoming),
            CreateTestTournament("In Progress Tournament", status: TournamentStatus.InProgress),
            CreateTestTournament("Completed Tournament", status: TournamentStatus.Completed)
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync(status: TournamentStatus.InProgress);

        // Assert
        result.Should().ContainSingle(t => t.Name == "In Progress Tournament");
        result.Should().NotContain(t => t.Name == "Scheduled Tournament");
        result.Should().NotContain(t => t.Name == "Completed Tournament");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithMultipleFilters_ShouldReturnMatchingTournaments()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("Match All", 
                new DateTime(2025, 12, 5), 
                "Test Location", 
                "Mixed Open", 
                TournamentStatus.Upcoming),
            CreateTestTournament("Match Some", 
                new DateTime(2025, 12, 5), 
                "Test Location", 
                "Men's A", 
                TournamentStatus.Completed),
            CreateTestTournament("Match None", 
                new DateTime(2025, 11, 1), 
                "Other Location", 
                "Women's B", 
                TournamentStatus.InProgress)
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync(
            startDateFrom: new DateTime(2025, 12, 1),
            location: "Test",
            division: "Mixed",
            status: TournamentStatus.Upcoming);

        // Assert
        result.Should().ContainSingle(t => t.Name == "Match All");
        result.Should().NotContain(t => t.Name == "Match Some");
        result.Should().NotContain(t => t.Name == "Match None");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithMaxResults_ShouldLimitResults()
    {
        // Arrange
        var tournaments = Enumerable.Range(1, 15)
            .Select(i => CreateTestTournament($"Tournament {i}"))
            .ToList();

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync(maxResults: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetTournamentsAsync_OrderByStartDate_ShouldReturnDescending()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTestTournament("First", new DateTime(2025, 12, 1)),
            CreateTestTournament("Second", new DateTime(2025, 12, 5)),
            CreateTestTournament("Third", new DateTime(2025, 12, 10))
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var result = await _tournamentService.GetTournamentsAsync();

        // Assert
        result.Should().NotBeEmpty();
        // The first tournament in results should have the most recent date
        var firstThree = result.Where(t => t.StartDate.HasValue).Take(3).ToList();
        if (firstThree.Count >= 3)
        {
            firstThree[0].StartDate!.Value.Should().BeOnOrAfter(firstThree[1].StartDate!.Value);
            firstThree[1].StartDate!.Value.Should().BeOnOrAfter(firstThree[2].StartDate!.Value);
        }
    }

    [Fact]
    public async Task GetTournamentsAsync_WithComplexGameData_ShouldPreserveStructure()
    {
        // Arrange
        var pair1 = new Pair 
        { 
            Player1 = new Player { Name = "John", Surname = "Doe" },
            Player2 = new Player { Name = "Jane", Surname = "Smith" }
        };
        var pair2 = new Pair 
        { 
            Player1 = new Player { Name = "Alice", Surname = "Brown" },
            Player2 = new Player { Name = "Bob", Surname = "White" }
        };
        _ = pair1.Id; // Trigger ID generation
        _ = pair2.Id;

        var tournament = CreateTestTournament("Tournament with Games");
        tournament.Pairs = new List<TournamentPair>
        {
            new()
            {
                PairInfo = pair1,
                Games = new List<PairGame>
                {
                    new() 
                    { 
                        Round = 1, 
                        CourtNumber = 1, 
                        OpponentPair = pair2,
                        Status = GameStatus.Scheduled
                    },
                    new() 
                    { 
                        Round = 2, 
                        CourtNumber = 2, 
                        OpponentPair = pair2,
                        Status = GameStatus.InProgress
                    }
                }
            },
            new()
            {
                PairInfo = pair2,
                Games = new List<PairGame>
                {
                    new() { Round = 1, CourtNumber = 1, OpponentPair = pair1, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 2, OpponentPair = pair1, Status = GameStatus.InProgress }
                }
            }
        };

        // Convert to compact format before saving (this is what the upload function does)
        var compactTournament = TournamentCompactMapper.ToCompact(tournament);
        await _container.CreateItemAsync(compactTournament, new PartitionKey(compactTournament.Id));

        // Act
        var result = await _tournamentService.GetTournamentByIdAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Pairs.Should().HaveCount(2);
        result.Pairs[0].PairInfo.Player1.Name.Should().Be("John");
        result.Pairs[0].Games.Should().NotBeNull();
        result.Pairs[0].Games.Should().HaveCount(2);
        result.Pairs[0].Games![0].Status.Should().Be(GameStatus.Scheduled);
        result.Pairs[0].Games![1].Status.Should().Be(GameStatus.InProgress);
    }

    #region Helper Methods

    private Tournament CreateTestTournament(
        string name,
        DateTime? startDate = null,
        string? location = null,
        string? division = null,
        TournamentStatus status = TournamentStatus.Upcoming)
    {
        return new Tournament
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            BlobFileName = $"{name.Replace(" ", "-").ToLower()}.xlsx",
            StartDate = startDate,
            EndDate = startDate?.AddDays(2),
            Location = location ?? "Test Location",
            Division = division ?? "Mixed Open",
            Status = status
        };
    }

    #endregion
}
