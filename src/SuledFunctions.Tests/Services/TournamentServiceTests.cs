using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Configuration;
using SuledFunctions.Exceptions;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services;

namespace SuledFunctions.Tests.Services;

public class TournamentServiceTests
{
    private readonly Mock<ILogger<TournamentService>> _loggerMock;
    private readonly Mock<ITournamentRepository> _repositoryMock;
    private readonly IOptions<TournamentSettings> _settings;
    private readonly TournamentService _service;

    public TournamentServiceTests()
    {
        _loggerMock = new Mock<ILogger<TournamentService>>();
        _repositoryMock = new Mock<ITournamentRepository>();
        _settings = Options.Create(new TournamentSettings
        {
            MaxResultsDefault = 100,
            GameDurationMinutes = 15,
            BreakDurationMinutes = 5
        });
        
        _service = new TournamentService(_repositoryMock.Object, _settings, _loggerMock.Object);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithNoFilters_ReturnsAllTournaments()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var compactTournaments = tournaments.Select(TournamentCompactMapper.ToCompact).ToList();
        
        _repositoryMock.Setup(r => r.QueryAsync(It.IsAny<TournamentQuerySpec>(), default))
            .ReturnsAsync(compactTournaments);

        // Act
        var result = await _service.GetTournamentsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTournamentsAsync_WithDateRangeFilter_FiltersCorrectly()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var filtered = tournaments.Where(t => 
            t.StartDate >= new DateTime(2025, 2, 1) && 
            t.StartDate <= new DateTime(2025, 2, 28)).ToList();
        var compactTournaments = filtered.Select(TournamentCompactMapper.ToCompact).ToList();

        _repositoryMock.Setup(r => r.QueryAsync(It.IsAny<TournamentQuerySpec>(), default))
            .ReturnsAsync(compactTournaments);

        var startDateFrom = new DateTime(2025, 2, 1);
        var startDateTo = new DateTime(2025, 2, 28);

        // Act
        var result = await _service.GetTournamentsAsync(
            startDateFrom: startDateFrom, 
            startDateTo: startDateTo);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("February Tournament");
    }

    [Fact]
    public async Task GetTournamentsAsync_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        _repositoryMock.Setup(r => r.QueryAsync(It.IsAny<TournamentQuerySpec>(), default))
            .ReturnsAsync(new List<TournamentCompact>());

        // Act
        var result = await _service.GetTournamentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithValidId_ReturnsTournament()
    {
        // Arrange
        var tournament = CreateTestTournaments().First();
        var compactTournament = TournamentCompactMapper.ToCompact(tournament);
        
        _repositoryMock.Setup(r => r.GetByIdAsync(tournament.Id, default))
            .ReturnsAsync(compactTournament);

        // Act
        var result = await _service.GetTournamentByIdAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tournament.Id);
        result.Name.Should().Be(tournament.Name);
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), default))
            .ReturnsAsync((TournamentCompact?)null);

        // Act
        var result = await _service.GetTournamentByIdAsync("invalid-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTournamentByIdAsync_WithEmptyId_ThrowsValidationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _service.GetTournamentByIdAsync(""));
    }

    [Fact]
    public async Task GetTournamentsAsync_LogsResultCount()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var compactTournaments = tournaments.Select(TournamentCompactMapper.ToCompact).ToList();
        
        _repositoryMock.Setup(r => r.QueryAsync(It.IsAny<TournamentQuerySpec>(), default))
            .ReturnsAsync(compactTournaments);

        // Act
        await _service.GetTournamentsAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved") && v.ToString()!.Contains("tournaments")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // Helper methods
    private List<Tournament> CreateTestTournaments()
    {
        return new List<Tournament>
        {
            new Tournament
            {
                Id = "t1",
                Name = "January Tournament",
                BlobFileName = "jan.xlsx",
                Location = "New York",
                Division = "Pro",
                StartDate = new DateTime(2025, 1, 15),
                EndDate = new DateTime(2025, 1, 17),
                Status = TournamentStatus.InProgress,
                Pairs = new List<TournamentPair>
                {
                    new()
                    {
                        PairInfo = new Pair 
                        { 
                            Player1 = new Player { Name = "Alice" }, 
                            Player2 = new Player { Name = "Bob" } 
                        },
                        Games = new List<PairGame>()
                    }
                }
            },
            new Tournament
            {
                Id = "t2",
                Name = "February Tournament",
                BlobFileName = "feb.xlsx",
                Location = "York",
                Division = "Amateur",
                StartDate = new DateTime(2025, 2, 15),
                EndDate = new DateTime(2025, 2, 17),
                Status = TournamentStatus.Completed,
                Pairs = new List<TournamentPair>
                {
                    new()
                    {
                        PairInfo = new Pair 
                        { 
                            Player1 = new Player { Name = "Charlie" }, 
                            Player2 = new Player { Name = "David" } 
                        },
                        Games = new List<PairGame>()
                    }
                }
            },
            new Tournament
            {
                Id = "t3",
                Name = "March Tournament",
                BlobFileName = "mar.xlsx",
                Location = "Boston",
                Division = "Open",
                StartDate = new DateTime(2025, 3, 15),
                EndDate = new DateTime(2025, 3, 17),
                Status = TournamentStatus.Upcoming,
                Pairs = new List<TournamentPair>
                {
                    new()
                    {
                        PairInfo = new Pair 
                        { 
                            Player1 = new Player { Name = "Eve" }, 
                            Player2 = new Player { Name = "Frank" } 
                        },
                        Games = new List<PairGame>()
                    }
                }
            }
        };
    }
}
