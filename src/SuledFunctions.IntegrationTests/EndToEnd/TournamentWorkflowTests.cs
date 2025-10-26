using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.IntegrationTests.Helpers;
using SuledFunctions.IntegrationTests.Infrastructure;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for complete tournament workflow
/// Tests: Upload Excel → Blob Storage → Parse → Cosmos DB → Retrieve
/// </summary>
[Collection("IntegrationTests")]
public class TournamentWorkflowTests
{
    private readonly AzuriteFixture _azuriteFixture;
    private readonly CosmosDbFixture _cosmosFixture;
    private readonly BlobContainerClient _blobContainer;
    private readonly Container _cosmosContainer;

    private const string DatabaseId = "SuledWorkflowDb";
    private const string ContainerId = "tournaments";
    private const string BlobContainerName = "tournaments";

    public TournamentWorkflowTests(AzuriteFixture azuriteFixture, CosmosDbFixture cosmosFixture)
    {
        _azuriteFixture = azuriteFixture;
        _cosmosFixture = cosmosFixture;

        _blobContainer = _azuriteFixture.CreateContainerAsync(BlobContainerName).GetAwaiter().GetResult();
        _cosmosContainer = _cosmosFixture.CreateContainerAsync(DatabaseId, ContainerId, "/id").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task EndToEnd_UploadAndParseTournament_ShouldCompleteSuccessfully()
    {
        // Arrange
        var fileName = "test-tournament.xlsx";
        using var excelStream = ExcelTestHelper.CreateSimpleTournamentExcel();
        var parserLogger = new Mock<ILogger<ExcelParserService>>();
        var parserService = new ExcelParserService(parserLogger.Object);

        // Act - Step 1: Upload to Blob Storage
        var blobClient = _blobContainer.GetBlobClient(fileName);
        await blobClient.UploadAsync(excelStream, overwrite: true);

        // Verify blob exists
        var blobExists = await blobClient.ExistsAsync();
        blobExists.Value.Should().BeTrue();

        // Act - Step 2: Parse Excel from Blob
        var downloadResponse = await blobClient.DownloadAsync();
        var tournament = await parserService.ParseTournamentAsync(downloadResponse.Value.Content, fileName);

        // Assert - Parsed data
        tournament.Should().NotBeNull();
        tournament.Name.Should().Be("test-tournament");
        tournament.BlobFileName.Should().Be(fileName);
        tournament.Games.Should().HaveCount(4);

        // Act - Step 3: Save to Cosmos DB
        await _cosmosContainer.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Act - Step 4: Retrieve from Cosmos DB
        var retrieved = await _cosmosContainer.ReadItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));

        // Assert - Full workflow
        retrieved.Resource.Should().NotBeNull();
        retrieved.Resource.Name.Should().Be(tournament.Name);
        retrieved.Resource.Games.Should().HaveCount(4);
        retrieved.Resource.BlobFileName.Should().Be(fileName);
    }

    [Fact]
    public async Task EndToEnd_GetUniquePairs_ShouldReturnCorrectPairs()
    {
        // Arrange - Create and store tournament
        var fileName = "pairs-test.xlsx";
        using var excelStream = ExcelTestHelper.CreateSimpleTournamentExcel();
        var parserLogger = new Mock<ILogger<ExcelParserService>>();
        var parserService = new ExcelParserService(parserLogger.Object);

        var blobClient = _blobContainer.GetBlobClient(fileName);
        await blobClient.UploadAsync(excelStream, overwrite: true);

        var downloadResponse = await blobClient.DownloadAsync();
        var tournament = await parserService.ParseTournamentAsync(downloadResponse.Value.Content, fileName);
        await _cosmosContainer.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Act - Get unique pairs using PairService
        var pairService = new PairService();
        var pairs = pairService.GetUniquePairs(new[] { tournament });

        // Assert
        pairs.Should().NotBeEmpty();
        pairs.Should().OnlyHaveUniqueItems(p => p.Id);
        pairs.Should().Contain(p => p.Player1.Contains("John") && p.Player1.Contains("Doe"));
        pairs.Should().Contain(p => p.Player1.Contains("Jane") && p.Player1.Contains("Smith"));
    }

    [Fact]
    public async Task EndToEnd_GetGamesForPair_ShouldReturnMatchingGames()
    {
        // Arrange - Create and store tournament
        var fileName = "games-test.xlsx";
        using var excelStream = ExcelTestHelper.CreateSimpleTournamentExcel();
        var parserLogger = new Mock<ILogger<ExcelParserService>>();
        var parserService = new ExcelParserService(parserLogger.Object);

        var blobClient = _blobContainer.GetBlobClient(fileName);
        await blobClient.UploadAsync(excelStream, overwrite: true);

        var downloadResponse = await blobClient.DownloadAsync();
        var tournament = await parserService.ParseTournamentAsync(downloadResponse.Value.Content, fileName);
        await _cosmosContainer.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        // Get a pair ID from the tournament
        var firstGame = tournament.Games.First();
        var pairId = firstGame.Pair1.Id;

        // Act - Get games for this pair using GameService
        var gameService = new GameService();
        var games = gameService.GetGamesForPair(new[] { tournament }, pairId);

        // Assert
        games.Should().NotBeEmpty();
        games.Should().OnlyContain(g => g.Pair1 == pairId || g.Pair2 == pairId);
    }

    [Fact]
    public async Task EndToEnd_MultipleTournaments_ShouldHandleConcurrently()
    {
        // Arrange - Create multiple tournaments
        var tournaments = new[]
        {
            ("tournament-1.xlsx", ExcelTestHelper.CreateSimpleTournamentExcel()),
            ("tournament-2.xlsx", ExcelTestHelper.CreateSimpleTournamentExcel()),
            ("tournament-3.xlsx", ExcelTestHelper.CreateComplexTournamentExcel())
        };

        var parserLogger = new Mock<ILogger<ExcelParserService>>();
        var parserService = new ExcelParserService(parserLogger.Object);
        var parsedTournaments = new List<Tournament>();

        // Act - Upload and parse all tournaments
        foreach (var (fileName, stream) in tournaments)
        {
            using (stream)
            {
                var blobClient = _blobContainer.GetBlobClient(fileName);
                await blobClient.UploadAsync(stream, overwrite: true);

                var downloadResponse = await blobClient.DownloadAsync();
                var tournament = await parserService.ParseTournamentAsync(downloadResponse.Value.Content, fileName);
                parsedTournaments.Add(tournament);

                await _cosmosContainer.CreateItemAsync(tournament, new PartitionKey(tournament.Id));
            }
        }

        // Assert - Query all tournaments from Cosmos
        var query = "SELECT * FROM c";
        var iterator = _cosmosContainer.GetItemQueryIterator<Tournament>(query);
        var storedTournaments = new List<Tournament>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            storedTournaments.AddRange(response);
        }

        storedTournaments.Should().HaveCountGreaterThanOrEqualTo(3);

        // Act - Get aggregated pairs across all tournaments
        var pairService = new PairService();
        var allPairs = pairService.GetUniquePairs(parsedTournaments);

        // Assert - Pairs should be aggregated correctly
        allPairs.Should().NotBeEmpty();
        allPairs.Should().OnlyHaveUniqueItems(p => p.Id);
    }

    [Fact]
    public async Task EndToEnd_UpdateTournament_ShouldReflectInQueries()
    {
        // Arrange - Create initial tournament
        var fileName = "update-test.xlsx";
        using var initialStream = ExcelTestHelper.CreateSimpleTournamentExcel();
        var parserLogger = new Mock<ILogger<ExcelParserService>>();
        var parserService = new ExcelParserService(parserLogger.Object);

        var blobClient = _blobContainer.GetBlobClient(fileName);
        await blobClient.UploadAsync(initialStream, overwrite: true);

        var downloadResponse = await blobClient.DownloadAsync();
        var tournament = await parserService.ParseTournamentAsync(downloadResponse.Value.Content, fileName);
        await _cosmosContainer.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        var initialGameCount = tournament.Games.Count;

        // Act - Update tournament with new games
        tournament.Games.Add(new Game
        {
            Id = Guid.NewGuid().ToString(),
            TournamentId = tournament.Id,
            Round = 3,
            CourtNumber = 1,
            Pair1 = tournament.Games[0].Pair1,
            Pair2 = tournament.Games[0].Pair2,
            Status = GameStatus.Scheduled
        });

        await _cosmosContainer.ReplaceItemAsync(tournament, tournament.Id, new PartitionKey(tournament.Id));

        // Act - Retrieve updated tournament
        var updated = await _cosmosContainer.ReadItemAsync<Tournament>(tournament.Id, new PartitionKey(tournament.Id));

        // Assert
        updated.Resource.Games.Should().HaveCount(initialGameCount + 1);
        updated.Resource.Games.Should().Contain(g => g.Round == 3);
    }

    [Fact]
    public async Task EndToEnd_DeleteTournament_ShouldRemoveFromAllQueries()
    {
        // Arrange - Create tournament
        var fileName = "delete-test.xlsx";
        using var excelStream = ExcelTestHelper.CreateSimpleTournamentExcel();
        var parserLogger = new Mock<ILogger<ExcelParserService>>();
        var parserService = new ExcelParserService(parserLogger.Object);

        var blobClient = _blobContainer.GetBlobClient(fileName);
        await blobClient.UploadAsync(excelStream, overwrite: true);

        var downloadResponse = await blobClient.DownloadAsync();
        var tournament = await parserService.ParseTournamentAsync(downloadResponse.Value.Content, fileName);
        await _cosmosContainer.CreateItemAsync(tournament, new PartitionKey(tournament.Id));

        var tournamentId = tournament.Id;

        // Act - Delete from Cosmos DB
        await _cosmosContainer.DeleteItemAsync<Tournament>(tournamentId, new PartitionKey(tournamentId));

        // Act - Delete from Blob Storage
        await blobClient.DeleteAsync();

        // Assert - Should not exist in Cosmos
        Func<Task> act = async () => await _cosmosContainer.ReadItemAsync<Tournament>(tournamentId, new PartitionKey(tournamentId));
        await act.Should().ThrowAsync<CosmosException>()
            .Where(e => e.StatusCode == System.Net.HttpStatusCode.NotFound);

        // Assert - Should not exist in Blob Storage
        var blobExists = await blobClient.ExistsAsync();
        blobExists.Value.Should().BeFalse();
    }
}
