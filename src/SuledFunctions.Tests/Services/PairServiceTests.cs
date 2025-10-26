using FluentAssertions;
using SuledFunctions.Models;
using SuledFunctions.Services;
using Xunit;

namespace SuledFunctions.Tests.Services;

public class PairServiceTests
{
    private readonly PairService _pairService;

    public PairServiceTests()
    {
        _pairService = new PairService();
    }

    [Fact]
    public void GetUniquePairs_WithValidTournaments_ReturnsAllUniquePairs()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTournament("t1", 
                CreatePair("p1", "John", "Doe", "Jane", "Smith"),
                CreatePair("p2", "Bob", "Johnson", "Alice", "Brown"))
        };

        // Act
        var result = _pairService.GetUniquePairs(tournaments).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == "p1");
        result.Should().Contain(p => p.Id == "p2");
    }

    [Fact]
    public void GetUniquePairs_WithDuplicatePairs_ReturnsUniqueList()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        
        var tournaments = new[]
        {
            CreateTournament("t1", pair1, pair2),
            CreateTournament("t2", pair1, pair2) // Same pairs in different tournament
        };

        // Act
        var result = _pairService.GetUniquePairs(tournaments).ToList();

        // Assert
        result.Should().HaveCount(2, "duplicate pairs should be filtered out");
        result.Select(p => p.Id).Should().BeEquivalentTo(new[] { "p1", "p2" });
    }

    [Fact]
    public void GetUniquePairs_OrdersPairsByDisplayName()
    {
        // Arrange
        var tournaments = new[]
        {
            CreateTournament("t1",
                CreatePair("p1", "Zara", "Last", "Zoe", "End"), // Should be last
                CreatePair("p2", "Alice", "First", "Bob", "Second"), // Should be first
                CreatePair("p3", "Mike", "Middle", "Mary", "Mid")) // Should be in middle
        };

        // Act
        var result = _pairService.GetUniquePairs(tournaments).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].DisplayName.Should().Contain("Alice");
        result[1].DisplayName.Should().Contain("Mike");
        result[2].DisplayName.Should().Contain("Zara");
    }

    [Fact]
    public void GetUniquePairs_IncludesRequiredFields()
    {
        // Arrange
        var pair1 = CreatePair("p1", "Aaron", "Apple", "Alex", "Anderson");  // Will sort first alphabetically
        var pair2 = CreatePair("p2", "Zack", "Zebra", "Zara", "Zoo");
        var tournaments = new[]
        {
            CreateTournament("t1", pair1, pair2)
        };

        // Act
        var result = _pairService.GetUniquePairs(tournaments).First();

        // Assert
        result.Id.Should().Be("p1");
        result.DisplayName.Should().NotBeNullOrEmpty();
        result.Player1.Should().Be("Aaron Apple");
        result.Player2.Should().Be("Alex Anderson");
    }

    [Fact]
    public void GetUniquePairs_WithMultipleTournaments_CombinesPairs()
    {
        // Arrange
        var p1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var p2 = CreatePair("p2", "Bob", "Johnson", "Alice", "Brown");
        var p3 = CreatePair("p3", "Tom", "Wilson", "Sue", "Davis");
        var tournaments = new[]
        {
            CreateTournament("t1", p1, p2),
            CreateTournament("t2", p2, p3)
        };

        // Act
        var result = _pairService.GetUniquePairs(tournaments).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(p => p.Id).Should().BeEquivalentTo(new[] { "p1", "p2", "p3" });
    }

    [Fact]
    public void GetUniquePairs_WithNullTournaments_ReturnsEmptyList()
    {
        // Act
        var result = _pairService.GetUniquePairs(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUniquePairs_WithEmptyTournaments_ReturnsEmptyPairsList()
    {
        // Act
        var result = _pairService.GetUniquePairs(new Tournament[0]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUniquePairs_WithNullGames_HandlesGracefully()
    {
        // Arrange
        var pair1 = CreatePair("p1", "John", "Doe", "Jane", "Smith");
        var pair2 = CreatePair("p2", "Bob", "Builder", "Alice", "Wonder");
        var tournaments = new[]
        {
            new Tournament { Id = "t1", Name = "Test", Games = null },
            CreateTournament("t2", pair1, pair2)
        };

        // Act
        var result = _pairService.GetUniquePairs(tournaments).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Id).Should().Contain("p1");
    }

    // Helper methods
    private Tournament CreateTournament(string id, params Pair[] pairs)
    {
        var games = new List<Game>();
        for (int i = 0; i < pairs.Length - 1; i++)
        {
            games.Add(new Game
            {
                Id = $"{id}-game{i}",
                Round = 1,
                CourtNumber = i + 1,
                Pair1 = pairs[i],
                Pair2 = pairs[i + 1],
                Status = GameStatus.Scheduled
            });
        }

        return new Tournament
        {
            Id = id,
            Name = $"Tournament {id}",
            Games = games
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
