using FluentAssertions;
using SuledFunctions.Models;
using SuledFunctions.Services;
using Xunit;

namespace SuledFunctions.Tests.Services;

public class GameServiceTests
{
    private readonly GameService _gameService;

    public GameServiceTests()
    {
        _gameService = new GameService();
    }

    [Fact]
    public void GetGamesForPair_WithValidPairId_ReturnsMatchingGames()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var tournaments = new[] { CreateTournamentWithGames("t1", pair1, pair2, 3) };

        // Act
        var result = _gameService.GetGamesForPair(tournaments, "p1").ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(g => g.Pair1 == pair1.DisplayName || g.Pair2 == pair1.DisplayName);
    }

    [Fact]
    public void GetGamesForPair_WithNonExistentPairId_ReturnsEmptyList()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var tournaments = new[] { CreateTournamentWithGames("t1", pair1, pair2, 2) };

        // Act
        var result = _gameService.GetGamesForPair(tournaments, "p999");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGamesForPair_OrdersGamesByRoundThenCourtNumber()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        
        var games = new[]
        {
            CreateGame("g1", 2, 3, pair1, pair2),
            CreateGame("g2", 1, 2, pair1, pair2),
            CreateGame("g3", 2, 1, pair1, pair2),
            CreateGame("g4", 1, 1, pair1, pair2)
        };

        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test Tournament",
            Games = games.ToList()
        };

        // Act
        var result = _gameService.GetGamesForPair(new[] { tournament }, "p1").ToList();

        // Assert
        result.Should().HaveCount(4);
        result[0].Round.Should().Be(1);
        result[0].CourtNumber.Should().Be(1);
        result[1].Round.Should().Be(1);
        result[1].CourtNumber.Should().Be(2);
        result[2].Round.Should().Be(2);
        result[2].CourtNumber.Should().Be(1);
        result[3].Round.Should().Be(2);
        result[3].CourtNumber.Should().Be(3);
    }

    [Fact]
    public void GetGamesForPair_IncludesIsOurGameFlag_WhenPairIsPair1()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var game = CreateGame("g1", 1, 1, pair1, pair2);
        
        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test",
            Games = new List<Game> { game }
        };

        // Act
        var result = _gameService.GetGamesForPair(new[] { tournament }, "p1").First();

        // Assert
        result.IsOurGame.Should().BeTrue();
    }

    [Fact]
    public void GetGamesForPair_IncludesIsOurGameFlag_WhenPairIsPair2()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var game = CreateGame("g1", 1, 1, pair1, pair2);
        
        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test",
            Games = new List<Game> { game }
        };

        // Act
        var result = _gameService.GetGamesForPair(new[] { tournament }, "p2").First();

        // Assert
        result.IsOurGame.Should().BeFalse();
    }

    [Fact]
    public void GetGamesForPair_IncludesAllRequiredGameFields()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var game = CreateGame("g1", 1, 5, pair1, pair2);
        game.Status = GameStatus.InProgress;
        game.ScheduledTime = DateTime.UtcNow;
        
        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test",
            Games = new List<Game> { game }
        };

        // Act
        var result = _gameService.GetGamesForPair(new[] { tournament }, "p1").First();

        // Assert
        result.Id.Should().Be("g1");
        result.Round.Should().Be(1);
        result.CourtNumber.Should().Be(5);
        result.Status.Should().Be("InProgress");
        result.ScheduledTime.Should().NotBeNull();
        result.Pair1.Should().NotBeNullOrEmpty();
        result.Pair2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetGamesForPair_WithEmptyTournaments_ReturnsEmptyList()
    {
        // Act
        var result = _gameService.GetGamesForPair(new Tournament[0], "p1");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGamesForPair_WithNullTournaments_ReturnsEmptyList()
    {
        // Act
        var result = _gameService.GetGamesForPair(null, "p1");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGamesForPair_WithNullOrEmptyPairId_ReturnsEmptyList()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var tournaments = new[] { CreateTournamentWithGames("t1", pair1, pair2, 2) };

        // Act
        var resultNull = _gameService.GetGamesForPair(tournaments, null);
        var resultEmpty = _gameService.GetGamesForPair(tournaments, "");

        // Assert
        resultNull.Should().BeEmpty();
        resultEmpty.Should().BeEmpty();
    }

    [Fact]
    public void GetGamesForPair_WithMultipleTournaments_ReturnsAllMatchingGames()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        
        var tournaments = new[]
        {
            CreateTournamentWithGames("t1", pair1, pair2, 2),
            CreateTournamentWithGames("t2", pair1, pair2, 3)
        };

        // Act
        var result = _gameService.GetGamesForPair(tournaments, "p1");

        // Assert
        result.Should().HaveCount(5);
    }

    // Helper methods
    private Tournament CreateTournamentWithGames(string id, Pair pair1, Pair pair2, int gameCount)
    {
        var games = new List<Game>();
        for (int i = 0; i < gameCount; i++)
        {
            games.Add(CreateGame($"{id}-game{i}", i / 3 + 1, i % 3 + 1, pair1, pair2));
        }

        return new Tournament
        {
            Id = id,
            Name = $"Tournament {id}",
            Games = games
        };
    }

    private Game CreateGame(string id, int round, int courtNumber, Pair pair1, Pair pair2)
    {
        return new Game
        {
            Id = id,
            Round = round,
            CourtNumber = courtNumber,
            Pair1 = pair1,
            Pair2 = pair2,
            Status = GameStatus.Scheduled
        };
    }

    private Pair CreatePair(string id, string player1FirstName, string player1LastName,
        string player2FirstName, string player2LastName)
    {
        var player1 = new Player
        {
            Name = player1FirstName,
            Surname = player1LastName
        };

        var player2 = new Player
        {
            Name = player2FirstName,
            Surname = player2LastName
        };

        return new Pair
        {
            Id = id,
            Player1 = player1,
            Player2 = player2
        };
    }
}
