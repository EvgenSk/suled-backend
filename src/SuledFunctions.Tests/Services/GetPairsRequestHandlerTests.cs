using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services;
using SuledFunctions.Services.Handlers;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text.Json;

namespace SuledFunctions.Tests.Services;

public class GetPairsRequestHandlerTests
{
    private readonly Mock<ILogger<GetPairsRequestHandler>> _loggerMock;
    private readonly Mock<ITournamentService> _tournamentServiceMock;
    private readonly GetPairsRequestHandler _handler;

    public GetPairsRequestHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetPairsRequestHandler>>();
        _tournamentServiceMock = new Mock<ITournamentService>();
        var pairService = new PairService();
        _handler = new GetPairsRequestHandler(_tournamentServiceMock.Object, pairService, _loggerMock.Object);
    }

    private void SetupTournaments(IEnumerable<Tournament> tournaments) =>
        _tournamentServiceMock
            .Setup(s => s.GetTournamentsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<TournamentStatus?>(), It.IsAny<int>()))
            .ReturnsAsync(tournaments.ToList());

    [Fact]
    public async Task HandleAsync_WithValidTournaments_ReturnsAllUniquePairs()
    {
        SetupTournaments(CreateTestTournaments());
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content.Should().NotBeNull();
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(4);
        content.RootElement.GetProperty("pairs").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyTournaments_ReturnsEmptyPairsList()
    {
        SetupTournaments(Enumerable.Empty<Tournament>());
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(0);
        content.RootElement.GetProperty("pairs").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicatePairs_ReturnsUniqueList()
    {
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var pair1 = new Pair { Player1 = player1, Player2 = player2 };
        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var pair2 = new Pair { Player1 = player3, Player2 = player4 };

        var tournament = new Tournament
        {
            Id = "test-1", Name = "Test Tournament",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair { PairInfo = pair1, Games = new List<PairGame>
                    { new PairGame { Id = "g1", OpponentPair = pair2, Round = 1, CourtNumber = 1 },
                      new PairGame { Id = "g2", OpponentPair = pair2, Round = 2, CourtNumber = 1 } } },
                new TournamentPair { PairInfo = pair2, Games = new List<PairGame>
                    { new PairGame { Id = "g1", OpponentPair = pair1, Round = 1, CourtNumber = 1 },
                      new PairGame { Id = "g2", OpponentPair = pair1, Round = 2, CourtNumber = 1 } } }
            }
        };

        SetupTournaments(new[] { tournament });
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleTournaments_CombinesPairs()
    {
        SetupTournaments(new[] { CreateTournament("t1", 1), CreateTournament("t2", 2) });
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HandleAsync_OrdersPairsByDisplayName()
    {
        SetupTournaments(CreateTestTournaments());
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        var content = await GetResponseContent(response);
        var pairs = content!.RootElement.GetProperty("pairs").EnumerateArray().ToList();
        for (int i = 0; i < pairs.Count - 1; i++)
        {
            var current = pairs[i].GetProperty("displayName").GetString();
            var next = pairs[i + 1].GetProperty("displayName").GetString();
            string.Compare(current, next, StringComparison.Ordinal).Should().BeLessThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task HandleAsync_IncludesRequiredFields()
    {
        SetupTournaments(CreateTestTournaments());
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        var content = await GetResponseContent(response);
        var firstPair = content!.RootElement.GetProperty("pairs")[0];
        firstPair.TryGetProperty("id", out _).Should().BeTrue();
        firstPair.TryGetProperty("displayName", out _).Should().BeTrue();
        firstPair.TryGetProperty("player1", out _).Should().BeTrue();
        firstPair.TryGetProperty("player2", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LogsInformation()
    {
        SetupTournaments(CreateTestTournaments());
        var requestMock = CreateMockRequest();

        await _handler.HandleAsync(requestMock.Object);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting all pairs")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNullGames_HandlesGracefully()
    {
        SetupTournaments(new[] { new Tournament { Id = "test-1", Name = "Test Tournament" } });
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(0);
    }

    private Mock<HttpRequestData> CreateMockRequest()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<ILoggerFactory, LoggerFactory>();
        var workerOptions = Options.Create(new Microsoft.Azure.Functions.Worker.WorkerOptions
        {
            Serializer = new TestJsonSerializer()
        });
        serviceCollection.AddSingleton(workerOptions);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, serviceProvider);

        var requestMock = new Mock<HttpRequestData>(context.Object);
        var responseStream = new MemoryStream();
        var responseMock = new Mock<HttpResponseData>(context.Object);
        responseMock.SetupProperty(r => r.StatusCode);
        responseMock.SetupProperty(r => r.Body, responseStream);
        responseMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

        return requestMock;
    }

    private static async Task<JsonDocument?> GetResponseContent(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var content = await reader.ReadToEndAsync();
        return string.IsNullOrEmpty(content) ? null : JsonDocument.Parse(content);
    }

    private IEnumerable<Tournament> CreateTestTournaments() => new[] { CreateTournament("t1", 1) };

    private Tournament CreateTournament(string id, int roundOffset)
    {
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var pair1 = new Pair { Player1 = player1, Player2 = player2 };
        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var pair2 = new Pair { Player1 = player3, Player2 = player4 };
        var player5 = new Player { Name = "Charlie", Surname = "Davis" };
        var player6 = new Player { Name = "Diana", Surname = "Evans" };
        var pair3 = new Pair { Player1 = player5, Player2 = player6 };
        var player7 = new Player { Name = "Frank", Surname = "Green" };
        var player8 = new Player { Name = "Grace", Surname = "Harris" };
        var pair4 = new Pair { Player1 = player7, Player2 = player8 };

        return new Tournament
        {
            Id = id, Name = $"Tournament {id}",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair { PairInfo = pair1, Games = new List<PairGame>
                    { new PairGame { Id = $"g{roundOffset}-1", OpponentPair = pair2, Round = roundOffset, CourtNumber = 1 } } },
                new TournamentPair { PairInfo = pair2, Games = new List<PairGame>
                    { new PairGame { Id = $"g{roundOffset}-1", OpponentPair = pair1, Round = roundOffset, CourtNumber = 1 } } },
                new TournamentPair { PairInfo = pair3, Games = new List<PairGame>
                    { new PairGame { Id = $"g{roundOffset}-2", OpponentPair = pair4, Round = roundOffset, CourtNumber = 2 } } },
                new TournamentPair { PairInfo = pair4, Games = new List<PairGame>
                    { new PairGame { Id = $"g{roundOffset}-2", OpponentPair = pair3, Round = roundOffset, CourtNumber = 2 } } }
            }
        };
    }
}
