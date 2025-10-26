using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.Functions;
using SuledFunctions.Models;
using SuledFunctions.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SuledFunctions.Tests.Functions;

public class GetGamesForPairFunctionTests
{
    private readonly Mock<ILogger<GetGamesForPairFunction>> _loggerMock;
    private readonly GetGamesForPairFunction _function;

    public GetGamesForPairFunctionTests()
    {
        _loggerMock = new Mock<ILogger<GetGamesForPairFunction>>();
        var gameServiceMock = new Mock<IGameService>();
        _function = new GetGamesForPairFunction(_loggerMock.Object, gameServiceMock.Object);
    }

    [Fact]
    public async Task Run_WithValidPairId_ReturnsMatchingGames()
    {
        // Arrange
        var pairId = "pair-1";
        var tournaments = CreateTestTournamentsWithPair(pairId);
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, tournaments);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("pairId").GetString().Should().Be(pairId);
        content.RootElement.GetProperty("totalGames").GetInt32().Should().Be(2);
        content.RootElement.GetProperty("games").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Run_WithNonExistentPairId_ReturnsEmptyList()
    {
        // Arrange
        var pairId = "non-existent-pair";
        var tournaments = CreateTestTournamentsWithPair("different-pair");
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, tournaments);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalGames").GetInt32().Should().Be(0);
        content.RootElement.GetProperty("games").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Run_OrdersGamesByRoundThenCourtNumber()
    {
        // Arrange
        var pairId = "pair-1";
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var targetPair = new Pair { Id = pairId, Player1 = player1, Player2 = player2 };

        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var otherPair = new Pair { Id = "pair-2", Player1 = player3, Player2 = player4 };

        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            Games = new List<Game>
            {
                new Game { Id = "g3", Pair1 = targetPair, Pair2 = otherPair, Round = 2, CourtNumber = 2 },
                new Game { Id = "g1", Pair1 = targetPair, Pair2 = otherPair, Round = 1, CourtNumber = 1 },
                new Game { Id = "g4", Pair1 = otherPair, Pair2 = targetPair, Round = 2, CourtNumber = 3 },
                new Game { Id = "g2", Pair1 = targetPair, Pair2 = otherPair, Round = 1, CourtNumber = 2 }
            }
        };

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, new[] { tournament });

        // Assert
        var content = await GetResponseContent(response);
        var games = content!.RootElement.GetProperty("games").EnumerateArray().ToList();
        
        games.Should().HaveCount(4);
        
        // Verify ordering: Round 1 Court 1, Round 1 Court 2, Round 2 Court 2, Round 2 Court 3
        games[0].GetProperty("Round").GetInt32().Should().Be(1);
        games[0].GetProperty("CourtNumber").GetInt32().Should().Be(1);
        
        games[1].GetProperty("Round").GetInt32().Should().Be(1);
        games[1].GetProperty("CourtNumber").GetInt32().Should().Be(2);
        
        games[2].GetProperty("Round").GetInt32().Should().Be(2);
        games[2].GetProperty("CourtNumber").GetInt32().Should().Be(2);
        
        games[3].GetProperty("Round").GetInt32().Should().Be(2);
        games[3].GetProperty("CourtNumber").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Run_IncludesIsOurGameFlag_WhenPairIsPair1()
    {
        // Arrange
        var pairId = "pair-1";
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var targetPair = new Pair { Id = pairId, Player1 = player1, Player2 = player2 };

        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var otherPair = new Pair { Id = "pair-2", Player1 = player3, Player2 = player4 };

        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            Games = new List<Game>
            {
                new Game { Id = "g1", Pair1 = targetPair, Pair2 = otherPair, Round = 1, CourtNumber = 1 }
            }
        };

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, new[] { tournament });

        // Assert
        var content = await GetResponseContent(response);
        var game = content!.RootElement.GetProperty("games")[0];
        game.GetProperty("IsOurGame").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Run_IncludesIsOurGameFlag_WhenPairIsPair2()
    {
        // Arrange
        var pairId = "pair-1";
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var targetPair = new Pair { Id = pairId, Player1 = player1, Player2 = player2 };

        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var otherPair = new Pair { Id = "pair-2", Player1 = player3, Player2 = player4 };

        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            Games = new List<Game>
            {
                new Game { Id = "g1", Pair1 = otherPair, Pair2 = targetPair, Round = 1, CourtNumber = 1 }
            }
        };

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, new[] { tournament });

        // Assert
        var content = await GetResponseContent(response);
        var game = content!.RootElement.GetProperty("games")[0];
        game.GetProperty("IsOurGame").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Run_IncludesAllRequiredGameFields()
    {
        // Arrange
        var pairId = "pair-1";
        var tournaments = CreateTestTournamentsWithPair(pairId);
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, tournaments);

        // Assert
        var content = await GetResponseContent(response);
        var game = content!.RootElement.GetProperty("games")[0];
        
        game.TryGetProperty("Id", out _).Should().BeTrue();
        game.TryGetProperty("Round", out _).Should().BeTrue();
        game.TryGetProperty("CourtNumber", out _).Should().BeTrue();
        game.TryGetProperty("Status", out _).Should().BeTrue();
        game.TryGetProperty("ScheduledTime", out _).Should().BeTrue();
        game.TryGetProperty("Pair1", out _).Should().BeTrue();
        game.TryGetProperty("Pair2", out _).Should().BeTrue();
        game.TryGetProperty("IsOurGame", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Run_LogsInformationWithPairId()
    {
        // Arrange
        var pairId = "pair-1";
        var tournaments = CreateTestTournamentsWithPair(pairId);
        var requestMock = CreateMockRequest();

        // Act
        await _function.Run(requestMock.Object, pairId, tournaments);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting games for pair") && v.ToString()!.Contains(pairId)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithEmptyTournaments_ReturnsEmptyList()
    {
        // Arrange
        var pairId = "pair-1";
        var tournaments = Enumerable.Empty<Tournament>();
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, tournaments);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalGames").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Run_WithMultipleTournaments_ReturnsAllMatchingGames()
    {
        // Arrange
        var pairId = "pair-1";
        var tournament1 = CreateSingleTournamentWithPair(pairId, "t1");
        var tournament2 = CreateSingleTournamentWithPair(pairId, "t2");
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, pairId, new[] { tournament1, tournament2 });

        // Assert
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalGames").GetInt32().Should().Be(4); // 2 games per tournament
    }

    private Mock<HttpRequestData> CreateMockRequest()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<ILoggerFactory, LoggerFactory>();
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        
        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, serviceProvider);
        
        var requestMock = new Mock<HttpRequestData>(context.Object);
        
        var responseStream = new MemoryStream();
        var responseMock = new Mock<HttpResponseData>(context.Object);
        responseMock.SetupProperty(r => r.StatusCode);
        responseMock.SetupProperty(r => r.Body, responseStream);
        responseMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        
        // Mock WriteAsJsonAsync to manually serialize JSON
        responseMock.Setup(r => r.WriteAsJsonAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns<object, CancellationToken>(async (obj, ct) =>
            {
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                var bytes = Encoding.UTF8.GetBytes(json);
                await responseStream.WriteAsync(bytes, 0, bytes.Length, ct);
            });
        
        requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);
        
        return requestMock;
    }

    private async Task<JsonDocument?> GetResponseContent(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var content = await reader.ReadToEndAsync();
        
        if (string.IsNullOrEmpty(content))
            return null;
        
        return JsonDocument.Parse(content);
    }

    private IEnumerable<Tournament> CreateTestTournamentsWithPair(string pairId)
    {
        return new[] { CreateSingleTournamentWithPair(pairId, "test-1") };
    }

    private Tournament CreateSingleTournamentWithPair(string pairId, string tournamentId)
    {
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var targetPair = new Pair { Id = pairId, Player1 = player1, Player2 = player2 };

        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var otherPair = new Pair { Id = "other-pair", Player1 = player3, Player2 = player4 };

        return new Tournament
        {
            Id = tournamentId,
            Name = $"Tournament {tournamentId}",
            Games = new List<Game>
            {
                new Game { Id = $"{tournamentId}-g1", Pair1 = targetPair, Pair2 = otherPair, Round = 1, CourtNumber = 1, Status = GameStatus.Scheduled },
                new Game { Id = $"{tournamentId}-g2", Pair1 = otherPair, Pair2 = targetPair, Round = 2, CourtNumber = 1, Status = GameStatus.Scheduled }
            }
        };
    }
}
