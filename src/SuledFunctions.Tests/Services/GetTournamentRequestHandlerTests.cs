using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services.Handlers;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text.Json;

namespace SuledFunctions.Tests.Services;

public class GetTournamentRequestHandlerTests
{
    private readonly Mock<ITournamentService> _tournamentServiceMock;
    private readonly Mock<ILogger<GetTournamentRequestHandler>> _loggerMock;
    private readonly GetTournamentRequestHandler _handler;

    public GetTournamentRequestHandlerTests()
    {
        _tournamentServiceMock = new Mock<ITournamentService>();
        _loggerMock = new Mock<ILogger<GetTournamentRequestHandler>>();
        _handler = new GetTournamentRequestHandler(_tournamentServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenTournamentFound_ReturnsOkStatus()
    {
        var tournament = CreateTournament("t1");
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("t1")).ReturnsAsync(tournament);
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object, "t1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleAsync_WhenTournamentFound_ReturnsTournamentData()
    {
        var tournament = CreateTournament("t1");
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("t1")).ReturnsAsync(tournament);
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object, "t1");

        var content = await GetResponseContent(response);
        content.Should().NotBeNull();
        content!.RootElement.GetProperty("id").GetString().Should().Be("t1");
    }

    [Fact]
    public async Task HandleAsync_WhenTournamentNotFound_ReturnsNotFoundStatus()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("missing")).ReturnsAsync((Tournament?)null);
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object, "missing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenTournamentNotFound_ReturnsErrorMessage()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("missing")).ReturnsAsync((Tournament?)null);
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object, "missing");

        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("error").GetString().Should().Contain("missing");
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrows_ReturnsInternalServerError()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("t1")).ThrowsAsync(new Exception("DB error"));
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object, "t1");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task HandleAsync_LogsTournamentId()
    {
        var tournament = CreateTournament("t1");
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("t1")).ReturnsAsync(tournament);
        var requestMock = CreateMockRequest();

        await _handler.HandleAsync(requestMock.Object, "t1");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("t1")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PassesIdToService()
    {
        var tournament = CreateTournament("t42");
        _tournamentServiceMock.Setup(s => s.GetTournamentByIdAsync("t42")).ReturnsAsync(tournament);
        var requestMock = CreateMockRequest();

        await _handler.HandleAsync(requestMock.Object, "t42");

        _tournamentServiceMock.Verify(s => s.GetTournamentByIdAsync("t42"), Times.Once);
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

    private static Tournament CreateTournament(string id) => new()
    {
        Id = id,
        Name = "Test Tournament",
        Location = "Test City",
        Division = "Pro",
        StartDate = new DateTime(2025, 1, 15),
        Status = TournamentStatus.Upcoming
    };
}
