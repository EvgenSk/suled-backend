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
    public void CalculateRounds_WithSingleRound_FillsAvailableWindow()
    {
        // 9:00–10:00 = 60 min, default warmup 5 min, 1 round, 0 breaks
        // roundDuration = (60 - 5 - 0) / 1 = 55 min → Round 1: 9:05–10:00
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 0, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 4 },
            courts: 2
        );

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(1);
        result[0].RoundNumber.Should().Be(1);
        result[0].GameCount.Should().Be(4);
        result[0].StartTime.Should().Be(new TimeOnly(9, 5, 0));
        result[0].EndTime.Should().Be(new TimeOnly(10, 0, 0));
    }

    [Fact]
    public void CalculateRounds_WithMultipleRounds_DistributesTimeEvenly()
    {
        // 9:00–10:30 = 90 min, default warmup 5 min, 3 rounds, 2 breaks (10 min)
        // roundDuration = (90 - 5 - 10) / 3 = 25 min
        // Round 1: 9:05–9:30, Round 2: 9:35–10:00, Round 3: 10:05–10:30
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 2, 3 },
            gamesPerRound: new[] { 4, 4, 2 },
            courts: 2
        );

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(3);

        result[0].RoundNumber.Should().Be(1);
        result[0].StartTime.Should().Be(new TimeOnly(9, 5, 0));
        result[0].EndTime.Should().Be(new TimeOnly(9, 30, 0));
        result[0].GameCount.Should().Be(4);

        result[1].RoundNumber.Should().Be(2);
        result[1].StartTime.Should().Be(new TimeOnly(9, 35, 0));
        result[1].EndTime.Should().Be(new TimeOnly(10, 0, 0));
        result[1].GameCount.Should().Be(4);

        result[2].RoundNumber.Should().Be(3);
        result[2].StartTime.Should().Be(new TimeOnly(10, 5, 0));
        result[2].EndTime.Should().Be(new TimeOnly(10, 30, 0));
        result[2].GameCount.Should().Be(2);
    }

    [Fact]
    public void CalculateRounds_AllRoundsHaveEqualDuration()
    {
        // Rounds with different game counts must still have equal duration
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 2, 3 },
            gamesPerRound: new[] { 6, 2, 4 },
            courts: 2
        );

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(3);
        var durations = result.Select(r => r.EndTime.ToTimeSpan() - r.StartTime.ToTimeSpan()).ToList();
        durations[0].Should().Be(durations[1]);
        durations[1].Should().Be(durations[2]);
    }

    [Fact]
    public void CalculateRounds_BreakBetweenRoundsIsFiveMinutes()
    {
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 2, 3 },
            gamesPerRound: new[] { 2, 2, 2 },
            courts: 1
        );
        tournament.Warmup = TimeSpan.Zero;

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(3);
        for (int i = 0; i < result.Count - 1; i++)
        {
            var breakDuration = result[i + 1].StartTime.ToTimeSpan() - result[i].EndTime.ToTimeSpan();
            breakDuration.Should().Be(TimeSpan.FromMinutes(5));
        }
    }

    [Fact]
    public void CalculateRounds_LastRoundEndsAtTournamentEndTime()
    {
        // Verify the distribution exactly fills the window
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 2, 3 },
            gamesPerRound: new[] { 2, 2, 2 },
            courts: 1
        );
        tournament.Warmup = TimeSpan.Zero;

        var result = _service.CalculateRounds(tournament);

        var expectedEnd = new TimeSpan(10, 30, 0);
        var actualEnd = result.Last().EndTime.ToTimeSpan();
        Math.Abs((actualEnd - expectedEnd).TotalSeconds).Should().BeLessThan(1);
    }

    [Fact]
    public void CalculateRounds_WithNoStartDate_UsesCurrentDate()
    {
        var tournament = CreateTournamentWithGames(
            startDate: null,
            startTime: null,
            endTime: null,
            rounds: new[] { 1 },
            gamesPerRound: new[] { 2 },
            courts: 1
        );

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(1);
        // Default start 9:00 + default warmup 5 min → 9:05
        result[0].StartTime.Hour.Should().Be(9);
        result[0].StartTime.Minute.Should().Be(5);
    }

    [Fact]
    public void CalculateRounds_WithCustomStartTime_UsesProvidedTime()
    {
        // 14:30–15:30 = 60 min, default warmup 5 min, 1 round
        // roundDuration = 55 min → Round 1: 14:35–15:30
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 25),
            startTime: new TimeSpan(14, 30, 0),
            endTime: new TimeSpan(15, 30, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 2 },
            courts: 1
        );

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(1);
        result[0].StartTime.Should().Be(new TimeOnly(14, 35, 0));
        result[0].EndTime.Should().Be(new TimeOnly(15, 30, 0));
    }

    [Fact]
    public void CalculateRounds_WithNonSequentialRounds_PreservesRoundNumbers()
    {
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 3, 5 },
            gamesPerRound: new[] { 2, 2, 2 },
            courts: 1
        );
        tournament.Warmup = TimeSpan.Zero;

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(3);
        result[0].RoundNumber.Should().Be(1);
        result[1].RoundNumber.Should().Be(3);
        result[2].RoundNumber.Should().Be(5);

        // Rounds are still sequential in time regardless of round numbers
        result[1].StartTime.Should().BeAfter(result[0].EndTime);
        result[2].StartTime.Should().BeAfter(result[1].EndTime);
    }

    [Fact]
    public void CalculateRounds_GameCountReflectsActualGamesInRound()
    {
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 2 },
            gamesPerRound: new[] { 5, 1 },
            courts: 2
        );

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(2);
        result[0].GameCount.Should().Be(5);
        result[1].GameCount.Should().Be(1);
    }

    [Fact]
    public void CalculateRounds_WithDefaultWarmup_UsesFiveMinutes()
    {
        // warmup = null → defaults to 5 min; first round starts at startTime + 5 min
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 0, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 2 },
            courts: 1
        );
        // tournament.Warmup is intentionally left null

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(1);
        result[0].StartTime.Should().Be(new TimeOnly(9, 5, 0));
    }

    [Fact]
    public void CalculateRounds_WithZeroWarmup_StartsAtTournamentStartTime()
    {
        // 9:00–10:05 = 65 min, warmup 0, 2 rounds, 1 break (5 min)
        // available = 65 - 0 - 5 = 60, roundDuration = 30 min
        // Round 1: 9:00–9:30, Round 2: 9:35–10:05
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 5, 0),
            rounds: new[] { 1, 2 },
            gamesPerRound: new[] { 2, 2 },
            courts: 1
        );
        tournament.Warmup = TimeSpan.Zero;

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(2);
        result[0].StartTime.Should().Be(new TimeOnly(9, 0, 0));
        result[0].EndTime.Should().Be(new TimeOnly(9, 30, 0));
        result[1].StartTime.Should().Be(new TimeOnly(9, 35, 0));
        result[1].EndTime.Should().Be(new TimeOnly(10, 5, 0));
    }

    [Fact]
    public void CalculateRounds_WithExplicitWarmup_ShiftsFirstRoundStart()
    {
        // 9:00–10:00 = 60 min, warmup 5 min, 1 round
        // available = 55 min → Round 1: 9:05–10:00
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 0, 0),
            rounds: new[] { 1 },
            gamesPerRound: new[] { 2 },
            courts: 1
        );
        tournament.Warmup = new TimeSpan(0, 5, 0);

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(1);
        result[0].StartTime.Should().Be(new TimeOnly(9, 5, 0));
        result[0].EndTime.Should().Be(new TimeOnly(10, 0, 0));
    }

    [Fact]
    public void CalculateRounds_WithExplicitWarmup_AffectsAvailableTimeForAllRounds()
    {
        // 9:00–11:05 = 125 min, warmup 10 min, 3 rounds, 2 breaks (10 min)
        // available = 125 - 10 - 10 = 105, roundDuration = 35 min
        // Round 1: 9:10–9:45, Round 2: 9:50–10:25, Round 3: 10:30–11:05
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(11, 5, 0),
            rounds: new[] { 1, 2, 3 },
            gamesPerRound: new[] { 2, 2, 2 },
            courts: 1
        );
        tournament.Warmup = new TimeSpan(0, 10, 0);

        var result = _service.CalculateRounds(tournament);

        result.Should().HaveCount(3);
        result[0].StartTime.Should().Be(new TimeOnly(9, 10, 0));
        result[0].EndTime.Should().Be(new TimeOnly(9, 45, 0));
        result[1].StartTime.Should().Be(new TimeOnly(9, 50, 0));
        result[1].EndTime.Should().Be(new TimeOnly(10, 25, 0));
        result[2].StartTime.Should().Be(new TimeOnly(10, 30, 0));
        result[2].EndTime.Should().Be(new TimeOnly(11, 5, 0));
    }

    [Fact]
    public void CalculateRounds_LogsInformation()
    {
        var tournament = CreateTournamentWithGames(
            startDate: new DateTime(2025, 11, 22),
            startTime: new TimeSpan(9, 0, 0),
            endTime: new TimeSpan(10, 30, 0),
            rounds: new[] { 1, 2 },
            gamesPerRound: new[] { 2, 2 },
            courts: 1
        );

        _service.CalculateRounds(tournament);

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
        TimeSpan? endTime,
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
            EndTime = endTime,
            Pairs = new List<TournamentPair>()
        };

        var pairId = 1;
        var gameId = 1;

        for (int i = 0; i < rounds.Length; i++)
        {
            var roundNumber = rounds[i];
            var gameCount = gamesPerRound[i];

            for (int gameNum = 0; gameNum < gameCount; gameNum++)
            {
                var court = (gameNum % courts) + 1;

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

                pair1.Games.Add(new PairGame
                {
                    Id = $"game-{gameId}",
                    TournamentId = tournament.Id,
                    Round = roundNumber,
                    CourtNumber = court,
                    OpponentPair = pair2.PairInfo
                });

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
