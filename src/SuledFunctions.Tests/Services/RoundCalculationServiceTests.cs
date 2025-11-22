using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.Tests.Services;

public class RoundCalculationServiceTests
{
    private readonly Mock<ILogger<RoundCalculationService>> _loggerMock;
    private readonly RoundCalculationService _service;

    public RoundCalculationServiceTests()
    {
        _loggerMock = new Mock<ILogger<RoundCalculationService>>();
        _service = new RoundCalculationService(_loggerMock.Object);
    }

    [Fact]
    public void CalculateRounds_WithNoGames_ReturnsEmptyList()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            StartDate = new DateTime(2025, 11, 22),
            Pairs = new List<TournamentPair>()
        };

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateRounds_WithSingleRound_CalculatesCorrectTiming()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 4 },
            courts: 2
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(1);
        result[0].RoundNumber.Should().Be(1);
        result[0].GameCount.Should().Be(4);
        result[0].StartTime.Should().Be(new TimeOnly(9, 0, 0));
        // 4 games / 2 courts = 2 games per court × 15 min = 30 minutes
        result[0].EndTime.Should().Be(new TimeOnly(9, 30, 0));
    }

    [Fact]
    public void CalculateRounds_WithMultipleRounds_CalculatesSequentialTiming()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            rounds: new[] { 1, 2, 3 },
            gamesPerRound: new[] { 4, 4, 2 },
            courts: 2
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(3);
        
        // Round 1: 9:00 - 9:30 (4 games / 2 courts = 30 min)
        result[0].RoundNumber.Should().Be(1);
        result[0].StartTime.Should().Be(new TimeOnly(9, 0, 0));
        result[0].EndTime.Should().Be(new TimeOnly(9, 30, 0));
        result[0].GameCount.Should().Be(4);
        
        // Round 2: 9:35 - 10:05 (5 min break + 30 min)
        result[1].RoundNumber.Should().Be(2);
        result[1].StartTime.Should().Be(new TimeOnly(9, 35, 0));
        result[1].EndTime.Should().Be(new TimeOnly(10, 5, 0));
        result[1].GameCount.Should().Be(4);
        
        // Round 3: 10:10 - 10:25 (5 min break + 15 min)
        result[2].RoundNumber.Should().Be(3);
        result[2].StartTime.Should().Be(new TimeOnly(10, 10, 0));
        result[2].EndTime.Should().Be(new TimeOnly(10, 25, 0));
        result[2].GameCount.Should().Be(2);
    }

    [Fact]
    public void CalculateRounds_WithSingleCourt_CalculatesSequentialGames()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(10, 0, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 3 },
            courts: 1
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(1);
        result[0].GameCount.Should().Be(3);
        // 3 games / 1 court = 3 games sequentially × 15 min = 45 minutes
        result[0].StartTime.Should().Be(new TimeOnly(10, 0, 0));
        result[0].EndTime.Should().Be(new TimeOnly(10, 45, 0));
    }

    [Fact]
    public void CalculateRounds_WithManyCourts_CalculatesParallelGames()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(10, 0, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 6 },
            courts: 6
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(1);
        result[0].GameCount.Should().Be(6);
        // 6 games / 6 courts = 1 game per court × 15 min = 15 minutes
        result[0].StartTime.Should().Be(new TimeOnly(10, 0, 0));
        result[0].EndTime.Should().Be(new TimeOnly(10, 15, 0));
    }

    [Fact]
    public void CalculateRounds_WithNoStartDate_UsesCurrentDate()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: null,
            startTime: null,
            rounds: new[] { 1 },
            gamesPerRound: new[] { 2 },
            courts: 1
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(1);
        result[0].StartTime.Hour.Should().Be(9);
        result[0].StartTime.Minute.Should().Be(0); // Default start time
    }

    [Fact]
    public void CalculateRounds_WithCustomStartTime_UsesProvidedTime()
    {
        // Arrange
        var customStartTime = new TimeSpan(14, 30, 0); // 2:30 PM
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 25),
            startTime: customStartTime,
            rounds: new[] { 1 },
            gamesPerRound: new[] { 2 },
            courts: 1
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(1);
        result[0].StartTime.Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void CalculateRounds_WithNonSequentialRounds_HandlesCorrectly()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            rounds: new[] { 1, 3, 5 }, // Non-sequential rounds
            gamesPerRound: new[] { 2, 2, 2 },
            courts: 1
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(3);
        result[0].RoundNumber.Should().Be(1);
        result[1].RoundNumber.Should().Be(3);
        result[2].RoundNumber.Should().Be(5);
        
        // Each should still be calculated sequentially
        result[0].StartTime.Should().Be(new TimeOnly(9, 0, 0));
        result[1].StartTime.Should().Be(new TimeOnly(9, 35, 0));
        result[2].StartTime.Should().Be(new TimeOnly(10, 10, 0));
    }

    [Fact]
    public void CalculateRounds_WithUnevenGameDistribution_CalculatesCorrectly()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            rounds: new[] { 1, 2 },
            gamesPerRound: new[] { 5, 1 }, // 5 games then 1 game
            courts: 2
        );

        // Act
        var result = _service.CalculateRounds(tournament);

        // Assert
        result.Should().HaveCount(2);
        
        // Round 1: 5 games / 2 courts = 3 games per court (ceiling) × 15 min = 45 min
        result[0].GameCount.Should().Be(5);
        result[0].EndTime.Should().Be(new TimeOnly(9, 45, 0));
        
        // Round 2: 1 game / 2 courts = 1 game per court × 15 min = 15 min
        result[1].GameCount.Should().Be(1);
        result[1].StartTime.Should().Be(new TimeOnly(9, 50, 0));
        result[1].EndTime.Should().Be(new TimeOnly(10, 5, 0));
    }

    [Fact]
    public void CalculateRounds_LogsInformation()
    {
        // Arrange
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            rounds: new[] { 1, 2 },
            gamesPerRound: new[] { 2, 2 },
            courts: 1
        );

        // Act
        _service.CalculateRounds(tournament);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Calculated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private Tournament CreateTournamentWithGames(
        DateTime? startDate,
        TimeSpan? startTime,
        int[] rounds,
        int[] gamesPerRound,
        int courts)
    {
        var tournament = new Tournament
        {
            Id = "test-tournament",
            Name = "Test Tournament",
            StartDate = startDate,
            StartTime = startTime,
            Pairs = new List<TournamentPair>()
        };

        var pairId = 1;
        var gameId = 1;

        for (int i = 0; i < rounds.Length; i++)
        {
            var roundNumber = rounds[i];
            var gameCount = gamesPerRound[i];

            // Create pairs with games for this round
            // Each game involves 2 pairs, so we create games in pair-centered structure
            for (int gameNum = 0; gameNum < gameCount; gameNum++)
            {
                var court = (gameNum % courts) + 1;
                
                // Create or get pair 1
                var pair1Id = $"pair-{pairId}";
                var pair1 = tournament.Pairs.FirstOrDefault(p => p.Id == pair1Id);
                if (pair1 == null)
                {
                    pair1 = new TournamentPair
                    {
                        PairInfo = new Pair
                        {
                            Id = pair1Id,
                            Player1 = new Player { Name = $"Player{pairId}A", Surname = "Test" },
                            Player2 = new Player { Name = $"Player{pairId}B", Surname = "Test" }
                        },
                        Games = new List<PairGame>()
                    };
                    tournament.Pairs.Add(pair1);
                    pairId++;
                }

                // Create or get pair 2
                var pair2Id = $"pair-{pairId}";
                var pair2 = tournament.Pairs.FirstOrDefault(p => p.Id == pair2Id);
                if (pair2 == null)
                {
                    pair2 = new TournamentPair
                    {
                        PairInfo = new Pair
                        {
                            Id = pair2Id,
                            Player1 = new Player { Name = $"Player{pairId}A", Surname = "Test" },
                            Player2 = new Player { Name = $"Player{pairId}B", Surname = "Test" }
                        },
                        Games = new List<PairGame>()
                    };
                    tournament.Pairs.Add(pair2);
                    pairId++;
                }

                // Add game to pair1's perspective
                pair1.Games.Add(new PairGame
                {
                    Id = $"game-{gameId}",
                    TournamentId = tournament.Id,
                    Round = roundNumber,
                    CourtNumber = court,
                    OpponentPair = pair2.PairInfo
                });

                // Add game to pair2's perspective
                pair2.Games.Add(new PairGame
                {
                    Id = $"game-{gameId}",
                    TournamentId = tournament.Id,
                    Round = roundNumber,
                    CourtNumber = court,
                    OpponentPair = pair1.PairInfo
                });

                gameId++;
            }
        }

        return tournament;
    }
}
