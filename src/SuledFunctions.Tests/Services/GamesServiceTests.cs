using FluentAssertions;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Tests.Services;

public class GamesServiceTests
{
    private readonly Mock<ITournamentService> _tournamentServiceMock;
    private readonly GamesService _service;

    public GamesServiceTests()
    {
        _tournamentServiceMock = new Mock<ITournamentService>();
        _service = new GamesService(_tournamentServiceMock.Object);
    }

    [Fact]
    public async Task GetGamesForPairAsync_WithMatchingPair_ReturnsGames()
    {
        var (tournaments, pairId) = CreateTestTournamentsWithPair();
        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(tournaments);

        var result = await _service.GetGamesForPairAsync(pairId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetGamesForPairAsync_WithNonExistentPairId_ReturnsEmpty()
    {
        var (tournaments, _) = CreateTestTournamentsWithPair();
        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(tournaments);

        var result = await _service.GetGamesForPairAsync("non-existent");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGamesForPairAsync_WithEmptyPairId_ReturnsEmpty()
    {
        var result = await _service.GetGamesForPairAsync(string.Empty);

        _tournamentServiceMock.Verify(
            s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()),
            Times.Never);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGamesForPairAsync_OrdersGamesByRoundThenCourtNumber()
    {
        var targetPair = CreatePair("John", "Doe", "Jane", "Smith");
        var otherPair = CreatePair("Alice", "Brown", "Bob", "White");

        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame { Id = "g3", TournamentId = "t1", Round = 2, CourtNumber = 2, OpponentPair = otherPair },
                        new PairGame { Id = "g1", TournamentId = "t1", Round = 1, CourtNumber = 1, OpponentPair = otherPair },
                        new PairGame { Id = "g4", TournamentId = "t1", Round = 2, CourtNumber = 3, OpponentPair = otherPair },
                        new PairGame { Id = "g2", TournamentId = "t1", Round = 1, CourtNumber = 2, OpponentPair = otherPair },
                    }
                },
                new TournamentPair { PairInfo = otherPair, Games = new List<PairGame>() }
            }
        };

        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Tournament> { tournament });

        var result = (await _service.GetGamesForPairAsync(targetPair.Id)).ToList();

        result.Should().HaveCount(4);
        result[0].Round.Should().Be(1); result[0].CourtNumber.Should().Be(1);
        result[1].Round.Should().Be(1); result[1].CourtNumber.Should().Be(2);
        result[2].Round.Should().Be(2); result[2].CourtNumber.Should().Be(2);
        result[3].Round.Should().Be(2); result[3].CourtNumber.Should().Be(3);
    }

    [Fact]
    public async Task GetGamesForPairAsync_SetsIsOurGameTrue()
    {
        var targetPair = CreatePair("John", "Doe", "Jane", "Smith");
        var otherPair = CreatePair("Alice", "Brown", "Bob", "White");

        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame { Id = "g1", TournamentId = "t1", Round = 1, CourtNumber = 1, OpponentPair = otherPair }
                    }
                },
                new TournamentPair { PairInfo = otherPair, Games = new List<PairGame>() }
            }
        };

        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Tournament> { tournament });

        var result = (await _service.GetGamesForPairAsync(targetPair.Id)).ToList();

        result.Should().HaveCount(1);
        result[0].IsOurGame.Should().BeTrue();
        result[0].Pair1.Should().Be(targetPair.DisplayName);
        result[0].Pair2.Should().Be(otherPair.DisplayName);
    }

    [Fact]
    public async Task GetGamesForPairAsync_WithMultipleTournaments_ReturnsAllMatchingGames()
    {
        var (tournaments, pairId) = CreateMultiTournamentSetup("t1", "t2");
        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(tournaments);

        var result = await _service.GetGamesForPairAsync(pairId);

        result.Should().HaveCount(4); // 2 games per tournament
    }

    [Fact]
    public async Task GetGamesForPairAsync_SetsUnknownOpponent_WhenOpponentPairIsNull()
    {
        var targetPair = CreatePair("John", "Doe", "Jane", "Smith");
        var tournament = new Tournament
        {
            Id = "t1",
            Name = "Test",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame { Id = "g1", TournamentId = "t1", Round = 1, CourtNumber = 1, OpponentPair = null! }
                    }
                }
            }
        };

        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Tournament> { tournament });

        var result = (await _service.GetGamesForPairAsync(targetPair.Id)).ToList();

        result[0].Pair2.Should().Be("Unknown");
    }

    private static Pair CreatePair(string p1First, string p1Last, string p2First, string p2Last)
    {
        var pair = new Pair
        {
            Player1 = new Player { Name = p1First, Surname = p1Last },
            Player2 = new Player { Name = p2First, Surname = p2Last }
        };
        _ = pair.Id; // trigger deterministic hash generation
        return pair;
    }

    private (List<Tournament> Tournaments, string PairId) CreateTestTournamentsWithPair()
    {
        var targetPair = CreatePair("John", "Doe", "Jane", "Smith");
        var otherPair = CreatePair("Alice", "Brown", "Bob", "White");

        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame { Id = "g1", TournamentId = "test-1", Round = 1, CourtNumber = 1, OpponentPair = otherPair, Status = GameStatus.Scheduled },
                        new PairGame { Id = "g2", TournamentId = "test-1", Round = 2, CourtNumber = 1, OpponentPair = otherPair, Status = GameStatus.Scheduled }
                    }
                },
                new TournamentPair { PairInfo = otherPair, Games = new List<PairGame>() }
            }
        };

        return (new List<Tournament> { tournament }, targetPair.Id);
    }

    private (List<Tournament> Tournaments, string PairId) CreateMultiTournamentSetup(params string[] ids)
    {
        var targetPair = CreatePair("John", "Doe", "Jane", "Smith");
        var otherPair = CreatePair("Alice", "Brown", "Bob", "White");

        var tournaments = ids.Select(id => new Tournament
        {
            Id = id,
            Name = $"Tournament {id}",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame { Id = $"{id}-g1", TournamentId = id, Round = 1, CourtNumber = 1, OpponentPair = otherPair, Status = GameStatus.Scheduled },
                        new PairGame { Id = $"{id}-g2", TournamentId = id, Round = 2, CourtNumber = 1, OpponentPair = otherPair, Status = GameStatus.Scheduled }
                    }
                },
                new TournamentPair { PairInfo = otherPair, Games = new List<PairGame>() }
            }
        }).ToList();

        return (tournaments, targetPair.Id);
    }
}
