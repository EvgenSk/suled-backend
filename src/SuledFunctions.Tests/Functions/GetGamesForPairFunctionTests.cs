using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Functions;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text.Json;

namespace SuledFunctions.Tests.Functions;

public class GetGamesForPairFunctionTests
{
    private readonly Mock<IGamesService> _gamesServiceMock;
    private readonly Mock<ILogger<GetGamesForPairFunction>> _loggerMock;
    private readonly GetGamesForPairFunction _function;

    public GetGamesForPairFunctionTests()
    {
        _gamesServiceMock = new Mock<IGamesService>();
        _loggerMock = new Mock<ILogger<GetGamesForPairFunction>>();
        _function = new GetGamesForPairFunction(_gamesServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Run_WithValidPairId_ReturnsOkStatus()
    {
        var pairId = "pair-1";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(pairId))
            .ReturnsAsync(new[] { CreateGame("g1"), CreateGame("g2") });
        var requestMock = CreateMockRequest();

        var response = await _function.Run(requestMock.Object, pairId);

        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Run_WithValidPairId_ReturnsCorrectResponseShape()
    {
        var pairId = "pair-1";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(pairId))
            .ReturnsAsync(new[] { CreateGame("g1", round: 1), CreateGame("g2", round: 2) });
        var requestMock = CreateMockRequest();

        var response = await _function.Run(requestMock.Object, pairId);

        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("pairId").GetString().Should().Be(pairId);
        content.RootElement.GetProperty("totalGames").GetInt32().Should().Be(2);
        content.RootElement.GetProperty("games").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Run_WithNoGames_ReturnsEmptyList()
    {
        var pairId = "non-existent-pair";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(pairId))
            .ReturnsAsync(Enumerable.Empty<GameDto>());
        var requestMock = CreateMockRequest();

        var response = await _function.Run(requestMock.Object, pairId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalGames").GetInt32().Should().Be(0);
        content.RootElement.GetProperty("games").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Run_WhenServiceThrows_ReturnsInternalServerError()
    {
        var pairId = "pair-1";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(pairId))
            .ThrowsAsync(new Exception("Service failed"));
        var requestMock = CreateMockRequest();

        var response = await _function.Run(requestMock.Object, pairId);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Run_IncludesAllRequiredGameFields()
    {
        var pairId = "pair-1";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(pairId))
            .ReturnsAsync(new[] { CreateGame("g1") });
        var requestMock = CreateMockRequest();

        var response = await _function.Run(requestMock.Object, pairId);

        var content = await GetResponseContent(response);
        var game = content!.RootElement.GetProperty("games")[0];

        game.TryGetProperty("id", out _).Should().BeTrue();
        game.TryGetProperty("round", out _).Should().BeTrue();
        game.TryGetProperty("courtNumber", out _).Should().BeTrue();
        game.TryGetProperty("status", out _).Should().BeTrue();
        game.TryGetProperty("scheduledTime", out _).Should().BeTrue();
        game.TryGetProperty("pair1", out _).Should().BeTrue();
        game.TryGetProperty("pair2", out _).Should().BeTrue();
        game.TryGetProperty("isOurGame", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Run_LogsInformationWithPairId()
    {
        var pairId = "pair-1";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(pairId))
            .ReturnsAsync(Enumerable.Empty<GameDto>());
        var requestMock = CreateMockRequest();

        await _function.Run(requestMock.Object, pairId);

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
    public async Task Run_PassesPairIdToService()
    {
        var pairId = "pair-abc";
        _gamesServiceMock.Setup(s => s.GetGamesForPairAsync(It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<GameDto>());
        var requestMock = CreateMockRequest();

        await _function.Run(requestMock.Object, pairId);

        _gamesServiceMock.Verify(s => s.GetGamesForPairAsync(pairId), Times.Once);
    }

    private static GameDto CreateGame(string id, int round = 1, int courtNumber = 1) => new()
    {
        Id = id,
        Round = round,
        CourtNumber = courtNumber,
        Status = "Scheduled",
        Pair1 = "John Doe / Jane Smith",
        Pair2 = "Alice Brown / Bob White",
        IsOurGame = true
    };

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

    private async Task<JsonDocument?> GetResponseContent(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var content = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(content))
            return null;

        return JsonDocument.Parse(content);
    }
}
