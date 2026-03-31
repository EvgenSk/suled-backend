using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Models.Requests;
using SuledFunctions.Services.Handlers;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text.Json;

namespace SuledFunctions.Tests.Services;

public class GetTournamentsRequestHandlerTests
{
    private readonly Mock<ILogger<GetTournamentsRequestHandler>> _loggerMock;
    private readonly Mock<ITournamentService> _tournamentServiceMock;
    private readonly Mock<IValidator<GetTournamentsQuery>> _validatorMock;
    private readonly GetTournamentsRequestHandler _handler;

    public GetTournamentsRequestHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetTournamentsRequestHandler>>();
        _tournamentServiceMock = new Mock<ITournamentService>();
        _validatorMock = new Mock<IValidator<GetTournamentsQuery>>();

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<GetTournamentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new GetTournamentsRequestHandler(
            _tournamentServiceMock.Object, _validatorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNoParameters_ReturnsAllTournaments()
    {
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        content.RootElement.GetProperty("data").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_WithDateRangeFilter_PassesToService()
    {
        var tournaments = CreateTestTournaments().Take(1).ToList();
        var startDateFrom = new DateTime(2025, 1, 1);
        var startDateTo = new DateTime(2025, 1, 31);
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(startDateFrom, startDateTo, null, null, null, 100))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "startDateFrom", "2025-01-01" },
            { "startDateTo", "2025-01-31" }
        });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(startDateFrom, startDateTo, null, null, null, 100), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithLocationFilter_PassesToService()
    {
        var tournaments = CreateTestTournaments().Take(1).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, "New York", null, null, 100))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "location", "New York" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(null, null, "New York", null, null, 100), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDivisionFilter_PassesToService()
    {
        var tournaments = CreateTestTournaments().Take(1).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, "Pro", null, 100))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "division", "Pro" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(null, null, null, "Pro", null, 100), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithStatusFilter_PassesToService()
    {
        var tournaments = CreateTestTournaments().Take(1).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, TournamentStatus.InProgress, 100))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "status", "InProgress" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(null, null, null, null, TournamentStatus.InProgress, 100), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithMaxResults_PassesToService()
    {
        var tournaments = CreateTestTournaments().Take(2).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 50))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "maxResults", "50" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(null, null, null, null, null, 50), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithMaxResultsOver500_Caps500()
    {
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 500))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "maxResults", "1000" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(null, null, null, null, null, 500), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithAllFilters_PassesAllToService()
    {
        var tournaments = CreateTestTournaments().Take(1).ToList();
        var startDateFrom = new DateTime(2025, 1, 1);
        var startDateTo = new DateTime(2025, 12, 31);
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(startDateFrom, startDateTo, "New York", "Pro", TournamentStatus.InProgress, 10))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "startDateFrom", "2025-01-01" }, { "startDateTo", "2025-12-31" },
            { "location", "New York" }, { "division", "Pro" },
            { "status", "InProgress" }, { "maxResults", "10" }
        });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(startDateFrom, startDateTo, "New York", "Pro", TournamentStatus.InProgress, 10), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_IncludesRequiredFields()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(CreateTestTournaments());
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        var content = await GetResponseContent(response);
        var firstTournament = content!.RootElement.GetProperty("data")[0];
        firstTournament.TryGetProperty("id", out _).Should().BeTrue();
        firstTournament.TryGetProperty("name", out _).Should().BeTrue();
        firstTournament.TryGetProperty("location", out _).Should().BeTrue();
        firstTournament.TryGetProperty("division", out _).Should().BeTrue();
        firstTournament.TryGetProperty("startDate", out _).Should().BeTrue();
        firstTournament.TryGetProperty("status", out _).Should().BeTrue();
        firstTournament.TryGetProperty("gameCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_MapsWarmupPropertyInResponse()
    {
        var tournaments = new List<Tournament>
        {
            new Tournament { Id = "t1", Name = "With Warmup", Location = "City", Division = "Pro",
                StartDate = new DateTime(2025, 1, 15), Warmup = new TimeSpan(0, 5, 0), Status = TournamentStatus.Upcoming },
            new Tournament { Id = "t2", Name = "Without Warmup", Location = "City", Division = "Amateur",
                StartDate = new DateTime(2025, 2, 15), Warmup = null, Status = TournamentStatus.Upcoming }
        };
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        var content = await GetResponseContent(response);
        var tournamentsArray = content!.RootElement.GetProperty("data");
        tournamentsArray[0].TryGetProperty("warmup", out var warmupProp).Should().BeTrue();
        warmupProp.GetString().Should().Be("00:05:00");
        tournamentsArray[1].TryGetProperty("warmup", out var warmupProp2).Should().BeTrue();
        warmupProp2.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidDateFormat_IgnoresInvalidDate()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(CreateTestTournaments());
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "startDateFrom", "invalid-date" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidStatus_IgnoresInvalidStatus()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(CreateTestTournaments());
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "status", "InvalidStatus" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidMaxResults_UsesDefault()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(CreateTestTournaments());
        var requestMock = CreateMockRequest(new Dictionary<string, string> { { "maxResults", "not-a-number" } });

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(null, null, null, null, null, 100), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyResult_ReturnsEmptyList()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(new List<Tournament>());
        var requestMock = CreateMockRequest();

        var response = await _handler.HandleAsync(requestMock.Object);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_LogsRequest()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ReturnsAsync(CreateTestTournaments());
        var requestMock = CreateMockRequest();

        await _handler.HandleAsync(requestMock.Object);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing get tournaments request")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceThrowsException_ThrowsException()
    {
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(null, null, null, null, null, 100))
            .ThrowsAsync(new Exception("Database error"));
        var requestMock = CreateMockRequest();

        await Assert.ThrowsAsync<Exception>(async () => await _handler.HandleAsync(requestMock.Object));
    }

    private Mock<HttpRequestData> CreateMockRequest(Dictionary<string, string>? queryParams = null)
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

        var uriBuilder = new UriBuilder("http://localhost:7071/api/tournaments");
        if (queryParams != null && queryParams.Any())
        {
            var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
            foreach (var kvp in queryParams) query[kvp.Key] = kvp.Value;
            uriBuilder.Query = query.ToString();
        }
        requestMock.Setup(r => r.Url).Returns(uriBuilder.Uri);

        return requestMock;
    }

    private static async Task<JsonDocument?> GetResponseContent(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var content = await reader.ReadToEndAsync();
        return string.IsNullOrEmpty(content) ? null : JsonDocument.Parse(content);
    }

    private static List<Tournament> CreateTestTournaments() =>
    [
        new Tournament { Id = "t1", Name = "January Tournament", Location = "New York", Division = "Pro",
            StartDate = new DateTime(2025, 1, 15), EndDate = new DateTime(2025, 1, 17), Status = TournamentStatus.InProgress },
        new Tournament { Id = "t2", Name = "February Tournament", Location = "Boston", Division = "Amateur",
            StartDate = new DateTime(2025, 2, 15), EndDate = new DateTime(2025, 2, 17), Status = TournamentStatus.Upcoming },
        new Tournament { Id = "t3", Name = "March Tournament", Location = "Chicago", Division = "Open",
            StartDate = new DateTime(2025, 3, 15), EndDate = new DateTime(2025, 3, 17), Status = TournamentStatus.Completed }
    ];
}
