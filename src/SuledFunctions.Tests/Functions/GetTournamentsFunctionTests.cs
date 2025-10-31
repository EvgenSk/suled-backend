using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Functions;
using SuledFunctions.Models;
using SuledFunctions.Services;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text.Json;

namespace SuledFunctions.Tests.Functions;

public class GetTournamentsFunctionTests
{
    private readonly Mock<ILogger<GetTournamentsFunction>> _loggerMock;
    private readonly Mock<ITournamentService> _tournamentServiceMock;
    private readonly GetTournamentsFunction _function;

    public GetTournamentsFunctionTests()
    {
        _loggerMock = new Mock<ILogger<GetTournamentsFunction>>();
        _tournamentServiceMock = new Mock<ITournamentService>();
        _function = new GetTournamentsFunction(_loggerMock.Object, _tournamentServiceMock.Object);
    }

    [Fact]
    public async Task Run_WithNoParameters_ReturnsAllTournaments()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await GetResponseContent(response);
        content.Should().NotBeNull();
        var tournamentsArray = content!.RootElement;
        tournamentsArray.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Run_WithDateRangeFilter_PassesToService()
    {
        // Arrange
        var tournaments = CreateTestTournaments().Take(1).ToList();
        var startDateFrom = new DateTime(2025, 1, 1);
        var startDateTo = new DateTime(2025, 1, 31);

        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                startDateFrom, startDateTo, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "startDateFrom", "2025-01-01" },
            { "startDateTo", "2025-01-31" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            startDateFrom, startDateTo, null, null, null, 100), Times.Once);
    }

    [Fact]
    public async Task Run_WithLocationFilter_PassesToService()
    {
        // Arrange
        var tournaments = CreateTestTournaments().Take(1).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                null, null, "New York", null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "location", "New York" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            null, null, "New York", null, null, 100), Times.Once);
    }

    [Fact]
    public async Task Run_WithDivisionFilter_PassesToService()
    {
        // Arrange
        var tournaments = CreateTestTournaments().Take(1).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                null, null, null, "Pro", null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "division", "Pro" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            null, null, null, "Pro", null, 100), Times.Once);
    }

    [Fact]
    public async Task Run_WithStatusFilter_PassesToService()
    {
        // Arrange
        var tournaments = CreateTestTournaments().Take(1).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                null, null, null, null, TournamentStatus.InProgress, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "status", "InProgress" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            null, null, null, null, TournamentStatus.InProgress, 100), Times.Once);
    }

    [Fact]
    public async Task Run_WithMaxResults_PassesToService()
    {
        // Arrange
        var tournaments = CreateTestTournaments().Take(2).ToList();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                null, null, null, null, null, 50))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "maxResults", "50" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            null, null, null, null, null, 50), Times.Once);
    }

    [Fact]
    public async Task Run_WithMaxResultsOver500_Caps500()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                null, null, null, null, null, 500))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "maxResults", "1000" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            null, null, null, null, null, 500), Times.Once);
    }

    [Fact]
    public async Task Run_WithAllFilters_PassesAllToService()
    {
        // Arrange
        var tournaments = CreateTestTournaments().Take(1).ToList();
        var startDateFrom = new DateTime(2025, 1, 1);
        var startDateTo = new DateTime(2025, 12, 31);

        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
                startDateFrom, startDateTo, "New York", "Pro", TournamentStatus.InProgress, 10))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "startDateFrom", "2025-01-01" },
            { "startDateTo", "2025-12-31" },
            { "location", "New York" },
            { "division", "Pro" },
            { "status", "InProgress" },
            { "maxResults", "10" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            startDateFrom, startDateTo, "New York", "Pro", TournamentStatus.InProgress, 10), Times.Once);
    }

    [Fact]
    public async Task Run_IncludesRequiredFields()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        var content = await GetResponseContent(response);
        var firstTournament = content!.RootElement[0];

        firstTournament.TryGetProperty("id", out _).Should().BeTrue();
        firstTournament.TryGetProperty("name", out _).Should().BeTrue();
        firstTournament.TryGetProperty("location", out _).Should().BeTrue();
        firstTournament.TryGetProperty("division", out _).Should().BeTrue();
        firstTournament.TryGetProperty("startDate", out _).Should().BeTrue();
        firstTournament.TryGetProperty("status", out _).Should().BeTrue();
        firstTournament.TryGetProperty("gameCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithInvalidDateFormat_IgnoresInvalidDate()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "startDateFrom", "invalid-date" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert - Should still return OK, just ignore invalid date
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Run_WithInvalidStatus_IgnoresInvalidStatus()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "status", "InvalidStatus" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert - Should still return OK, just ignore invalid status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Run_WithInvalidMaxResults_UsesDefault()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest(new Dictionary<string, string>
        {
            { "maxResults", "not-a-number" }
        });

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert - Should use default 100
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _tournamentServiceMock.Verify(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100), Times.Once);
    }

    [Fact]
    public async Task Run_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(new List<Tournament>());

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Run_LogsRequest()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ReturnsAsync(tournaments);

        var requestMock = CreateMockRequest();

        // Act
        await _function.Run(requestMock.Object);

        // Assert
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
    public async Task Run_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _tournamentServiceMock.Setup(s => s.GetTournamentsAsync(
            null, null, null, null, null, 100))
            .ThrowsAsync(new Exception("Database error"));

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("error").GetString()
            .Should().Contain("An error occurred while retrieving tournaments");
    }

    // Helper methods
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
        
        // Setup URL with query parameters
        var uriBuilder = new UriBuilder("http://localhost:7071/api/tournaments");
        if (queryParams != null && queryParams.Any())
        {
            var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
            foreach (var kvp in queryParams)
            {
                query[kvp.Key] = kvp.Value;
            }
            uriBuilder.Query = query.ToString();
        }
        
        requestMock.Setup(r => r.Url).Returns(uriBuilder.Uri);
        
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

    private List<Tournament> CreateTestTournaments()
    {
        return new List<Tournament>
        {
            new Tournament
            {
                Id = "t1",
                Name = "January Tournament",
                Location = "New York",
                Division = "Pro",
                StartDate = new DateTime(2025, 1, 15),
                EndDate = new DateTime(2025, 1, 17),
                Status = TournamentStatus.InProgress,
                Games = new List<Game> { new Game(), new Game() }
            },
            new Tournament
            {
                Id = "t2",
                Name = "February Tournament",
                Location = "Boston",
                Division = "Amateur",
                StartDate = new DateTime(2025, 2, 15),
                EndDate = new DateTime(2025, 2, 17),
                Status = TournamentStatus.Upcoming,
                Games = new List<Game> { new Game() }
            },
            new Tournament
            {
                Id = "t3",
                Name = "March Tournament",
                Location = "Chicago",
                Division = "Open",
                StartDate = new DateTime(2025, 3, 15),
                EndDate = new DateTime(2025, 3, 17),
                Status = TournamentStatus.Completed,
                Games = new List<Game> { new Game(), new Game(), new Game() }
            }
        };
    }
}
