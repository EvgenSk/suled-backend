using FluentAssertions;
using Microsoft.Azure.Cosmos;
using SuledFunctions.IntegrationTests.Infrastructure;
using SuledFunctions.Models;

namespace SuledFunctions.IntegrationTests.CosmosDb;

/// <summary>
/// Integration tests for Cosmos DB operations
/// Tests CRUD operations and queries with real Cosmos DB container
/// </summary>
[Collection("CosmosDb")]
public class CosmosDbIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbFixture _fixture;
    private Container _container = null!;
    private const string DatabaseId = "SuledTestDb";
    private const string ContainerId = "tournaments";

    public CosmosDbIntegrationTests(CosmosDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _container = await _fixture.CreateContainerAsync(DatabaseId, ContainerId, "/id");
    }

    public Task DisposeAsync()
    {
        // Cleanup is handled by the fixture
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateTournament_WithValidData_ShouldPersist()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Tournament",
            BlobFileName = "test.xlsx",
            Games = new List<Game>()
        };

        // Act
        var response = await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        response.Resource.Id.Should().Be(tournament.Id);
        response.Resource.Name.Should().Be("Test Tournament");
    }

    [Fact]
    public async Task ReadTournament_AfterCreate_ShouldReturnSameData()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Read Test Tournament",
            BlobFileName = "read-test.xlsx",
            Games = new List<Game>()
        };
        await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Act
        var response = await _container.ReadItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));

        // Assert
        response.Resource.Should().NotBeNull();
        response.Resource.Name.Should().Be("Read Test Tournament");
        response.Resource.BlobFileName.Should().Be("read-test.xlsx");
    }

    [Fact]
    public async Task QueryTournaments_WithMultipleItems_ShouldReturnMatching()
    {
        // Arrange
        var tournaments = new[]
        {
            new Tournament { Id = Guid.NewGuid().ToString(), Name = "Tournament A", BlobFileName = "a.xlsx", Games = new List<Game>() },
            new Tournament { Id = Guid.NewGuid().ToString(), Name = "Tournament B", BlobFileName = "b.xlsx", Games = new List<Game>() },
            new Tournament { Id = Guid.NewGuid().ToString(), Name = "Tournament C", BlobFileName = "c.xlsx", Games = new List<Game>() }
        };

        foreach (var tournament in tournaments)
        {
            await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
        }

        // Act
        var query = "SELECT * FROM c WHERE CONTAINS(c.Name, 'Tournament')";
        var iterator = _container.GetItemQueryIterator<Tournament>(query);
        var results = new List<Tournament>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        // Assert
        results.Should().HaveCountGreaterThanOrEqualTo(3);
        results.Should().Contain(t => t.Name == "Tournament A");
        results.Should().Contain(t => t.Name == "Tournament B");
    }

    [Fact]
    public async Task UpdateTournament_ShouldModifyExistingItem()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Original Name",
            BlobFileName = "original.xlsx",
            Games = new List<Game>()
        };
        await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Act
        tournament.Name = "Updated Name";
        await _container.ReplaceItemAsync(tournament, tournament.Id, new PartitionKey(tournament.Id));

        // Assert
        var updated = await _container.ReadItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));
        updated.Resource.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteTournament_ShouldRemoveItem()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = Guid.NewGuid().ToString(),
            Name = "To Delete",
            BlobFileName = "delete.xlsx",
            Games = new List<Game>()
        };
        await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Act
        await _container.DeleteItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));

        // Assert
        Func<Task> act = async () => await _container.ReadItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));
        await act.Should().ThrowAsync<CosmosException>()
            .Where(e => e.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTournament_WithGames_ShouldStoreNestedData()
    {
        // Arrange
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var pair1 = new Pair { Id = "pair-1", Player1 = player1, Player2 = player2 };
        var pair2 = new Pair { Id = "pair-2", Player1 = player1, Player2 = player2 };

        var tournament = new Tournament
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Tournament with Games",
            BlobFileName = "games.xlsx",
            Games = new List<Game>
            {
                new Game { Id = "g1", Pair1 = pair1, Pair2 = pair2, Round = 1, CourtNumber = 1, Status = GameStatus.Scheduled },
                new Game { Id = "g2", Pair1 = pair1, Pair2 = pair2, Round = 1, CourtNumber = 2, Status = GameStatus.Scheduled }
            }
        };

        // Act
        await _container.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Assert
        var retrieved = await _container.ReadItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));
        retrieved.Resource.Games.Should().HaveCount(2);
        retrieved.Resource.Games[0].Round.Should().Be(1);
        retrieved.Resource.Games[0].Status.Should().Be(GameStatus.Scheduled);
    }

    [Fact]
    public async Task QueryByPartitionKey_ShouldBeEfficient()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Partition Key Test",
            BlobFileName = "pk-test.xlsx",
            Games = new List<Game>()
        };
        await _container.CreateItemAsync(tournament, new PartitionKey(tournamentId));

        // Act
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", tournamentId);
        
        var iterator = _container.GetItemQueryIterator<Tournament>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tournamentId) });

        var results = new List<Tournament>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(tournamentId);
    }
}
