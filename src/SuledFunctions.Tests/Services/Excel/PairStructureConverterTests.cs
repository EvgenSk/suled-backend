using FluentAssertions;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel;

namespace SuledFunctions.Tests.Services.Excel;

public class PairStructureConverterTests
{
    private readonly PairStructureConverter _converter;

    public PairStructureConverterTests()
    {
        _converter = new PairStructureConverter();
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_WithSingleGame_CreatesTwoPairs()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            CreateGame(tournamentId, 1, 1, "John", "Doe", "Jane", "Smith", "Alice", "Brown", "Bob", "White")
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        result.Should().HaveCount(2); // Two unique pairs
        result.Should().OnlyContain(p => p.Games.Count == 1); // Each pair has 1 game
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_WithMultipleGames_GroupsByPair()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            CreateGame(tournamentId, 1, 1, "John", "Doe", "Jane", "Smith", "Alice", "Brown", "Bob", "White"),
            CreateGame(tournamentId, 2, 1, "John", "Doe", "Jane", "Smith", "Charlie", "Davis", "Diana", "Evans")
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        result.Should().HaveCount(3); // John/Jane, Alice/Bob, Charlie/Diana
        
        var johnPair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "John");
        johnPair.Should().NotBeNull();
        johnPair!.Games.Should().HaveCount(2); // John/Jane played 2 games
        
        var alicePair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "Alice");
        alicePair.Should().NotBeNull();
        alicePair!.Games.Should().ContainSingle(); // Alice/Bob played 1 game
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_CreatesCorrectOpponents()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            CreateGame(tournamentId, 1, 1, "John", "Doe", "Jane", "Smith", "Alice", "Brown", "Bob", "White")
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        var johnPair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "John");
        johnPair.Should().NotBeNull();
        var johnGame = johnPair!.Games[0];
        johnGame.OpponentPair.Player1.Name.Should().Be("Alice");
        johnGame.OpponentPair.Player2.Name.Should().Be("Bob");

        var alicePair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "Alice");
        alicePair.Should().NotBeNull();
        var aliceGame = alicePair!.Games[0];
        aliceGame.OpponentPair.Player1.Name.Should().Be("John");
        aliceGame.OpponentPair.Player2.Name.Should().Be("Jane");
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_PreservesGameMetadata()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var gameId = Guid.NewGuid().ToString();
        var scheduledTime = DateTime.UtcNow;
        
        var games = new List<Game>
        {
            new Game
            {
                Id = gameId,
                TournamentId = tournamentId,
                Round = 5,
                CourtNumber = 3,
                ScheduledTime = scheduledTime,
                Status = GameStatus.InProgress,
                Pair1 = CreatePair("John", "Doe", "Jane", "Smith"),
                Pair2 = CreatePair("Alice", "Brown", "Bob", "White")
            }
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        var johnPair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "John");
        johnPair.Should().NotBeNull();
        var johnGame = johnPair!.Games[0];
        
        johnGame.Id.Should().Be(gameId);
        johnGame.TournamentId.Should().Be(tournamentId);
        johnGame.Round.Should().Be(5);
        johnGame.CourtNumber.Should().Be(3);
        johnGame.ScheduledTime.Should().Be(scheduledTime);
        johnGame.Status.Should().Be(GameStatus.InProgress);
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_SortsGames_ByRoundThenCourt()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            CreateGame(tournamentId, 2, 3, "John", "Doe", "Jane", "Smith", "Alice", "Brown", "Bob", "White"),
            CreateGame(tournamentId, 1, 2, "John", "Doe", "Jane", "Smith", "Charlie", "Davis", "Diana", "Evans"),
            CreateGame(tournamentId, 1, 1, "John", "Doe", "Jane", "Smith", "Frank", "Green", "Grace", "Harris")
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        var johnPair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "John");
        johnPair.Should().NotBeNull();
        johnPair!.Games.Should().HaveCount(3);
        
        // Games should be sorted by round, then court
        johnPair.Games[0].Round.Should().Be(1);
        johnPair.Games[0].CourtNumber.Should().Be(1);
        johnPair.Games[1].Round.Should().Be(1);
        johnPair.Games[1].CourtNumber.Should().Be(2);
        johnPair.Games[2].Round.Should().Be(2);
        johnPair.Games[2].CourtNumber.Should().Be(3);
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_SortsPairs_ByDisplayName()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            CreateGame(tournamentId, 1, 1, "Zoe", "Young", "Xavier", "Wilson", "Alice", "Brown", "Bob", "White")
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        result.Should().HaveCount(2);
        // Pairs should be sorted alphabetically by display name
        string.Compare(result[0].DisplayName, result[1].DisplayName, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_WithEmptyGames_ReturnsEmptyList()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>();

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_MaintainsSamePairIdAcrossGames()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            CreateGame(tournamentId, 1, 1, "John", "Doe", "Jane", "Smith", "Alice", "Brown", "Bob", "White"),
            CreateGame(tournamentId, 2, 1, "John", "Doe", "Jane", "Smith", "Charlie", "Davis", "Diana", "Evans")
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        var johnPairs = result.Where(p => p.PairInfo.Player1.Name == "John").ToList();
        johnPairs.Should().ContainSingle(); // Should be single pair, not duplicated
        johnPairs[0].Games.Should().HaveCount(2); // With both games
    }

    [Fact]
    public void ConvertGamesToPairCentricStructure_BothPairsSeeSameGame()
    {
        // Arrange
        var tournamentId = Guid.NewGuid().ToString();
        var gameId = Guid.NewGuid().ToString();
        var games = new List<Game>
        {
            new Game
            {
                Id = gameId,
                TournamentId = tournamentId,
                Round = 1,
                CourtNumber = 1,
                Pair1 = CreatePair("John", "Doe", "Jane", "Smith"),
                Pair2 = CreatePair("Alice", "Brown", "Bob", "White")
            }
        };

        // Act
        var result = _converter.ConvertGamesToPairCentricStructure(games, tournamentId);

        // Assert
        result.Should().HaveCount(2);
        
        var johnPair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "John");
        var alicePair = result.FirstOrDefault(p => p.PairInfo.Player1.Name == "Alice");
        
        johnPair!.Games[0].Id.Should().Be(gameId);
        alicePair!.Games[0].Id.Should().Be(gameId);
        
        // Both should reference the same game, just with different perspectives
        johnPair.Games[0].OpponentPair.Should().BeEquivalentTo(alicePair.PairInfo);
        alicePair.Games[0].OpponentPair.Should().BeEquivalentTo(johnPair.PairInfo);
    }

    private Game CreateGame(
        string tournamentId, 
        int round, 
        int court,
        string p1_1Name, string p1_1Surname,
        string p1_2Name, string p1_2Surname,
        string p2_1Name, string p2_1Surname,
        string p2_2Name, string p2_2Surname)
    {
        return new Game
        {
            TournamentId = tournamentId,
            Round = round,
            CourtNumber = court,
            Pair1 = CreatePair(p1_1Name, p1_1Surname, p1_2Name, p1_2Surname),
            Pair2 = CreatePair(p2_1Name, p2_1Surname, p2_2Name, p2_2Surname),
            Status = GameStatus.Scheduled
        };
    }

    private Pair CreatePair(string player1Name, string player1Surname, string player2Name, string player2Surname)
    {
        var pair = new Pair
        {
            Player1 = new Player { Name = player1Name, Surname = player1Surname },
            Player2 = new Player { Name = player2Name, Surname = player2Surname }
        };
        // Trigger ID generation
        _ = pair.Id;
        return pair;
    }
}
