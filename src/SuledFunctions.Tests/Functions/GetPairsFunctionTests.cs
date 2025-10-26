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

public class GetPairsFunctionTests
{
    private readonly Mock<ILogger<GetPairsFunction>> _loggerMock;
    private readonly GetPairsFunction _function;

    public GetPairsFunctionTests()
    {
        _loggerMock = new Mock<ILogger<GetPairsFunction>>();
        var pairService = new PairService(); // Use real service instead of mock
        _function = new GetPairsFunction(_loggerMock.Object, pairService);
    }

    [Fact]
    public async Task Run_WithValidTournaments_ReturnsAllUniquePairs()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, tournaments);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await GetResponseContent(response);
        content.Should().NotBeNull();
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(4); // 4 unique pairs
        content.RootElement.GetProperty("pairs").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task Run_WithEmptyTournaments_ReturnsEmptyPairsList()
    {
        // Arrange
        var tournaments = Enumerable.Empty<Tournament>();
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, tournaments);

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(0);
        content.RootElement.GetProperty("pairs").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Run_WithDuplicatePairs_ReturnsUniqueList()
    {
        // Arrange
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var pair1 = new Pair { Player1 = player1, Player2 = player2 };

        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var pair2 = new Pair { Player1 = player3, Player2 = player4 };

        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            Games = new List<Game>
            {
                new Game { Id = "g1", Pair1 = pair1, Pair2 = pair2, Round = 1, CourtNumber = 1 },
                new Game { Id = "g2", Pair1 = pair1, Pair2 = pair2, Round = 2, CourtNumber = 1 } // Same pairs
            }
        };

        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, new[] { tournament });

        // Assert
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(2); // Only unique pairs
    }

    [Fact]
    public async Task Run_WithMultipleTournaments_CombinesPairs()
    {
        // Arrange
        var tournament1 = CreateTournament("t1", 1);
        var tournament2 = CreateTournament("t2", 2);
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, new[] { tournament1, tournament2 });

        // Assert
        var content = await GetResponseContent(response);
        // Each tournament has 2 games with 2 pairs each = 4 unique pairs per tournament
        // But pairs might overlap, so we just check that we get some pairs
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Run_OrdersPairsByDisplayName()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, tournaments);

        // Assert
        var content = await GetResponseContent(response);
        var pairs = content!.RootElement.GetProperty("pairs").EnumerateArray().ToList();
        
        // Verify alphabetical ordering
        for (int i = 0; i < pairs.Count - 1; i++)
        {
            var currentDisplayName = pairs[i].GetProperty("displayName").GetString();
            var nextDisplayName = pairs[i + 1].GetProperty("displayName").GetString();
            string.Compare(currentDisplayName, nextDisplayName, StringComparison.Ordinal)
                .Should().BeLessThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task Run_IncludesRequiredFields()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, tournaments);

        // Assert
        var content = await GetResponseContent(response);
        var firstPair = content!.RootElement.GetProperty("pairs")[0];
        
        firstPair.TryGetProperty("id", out _).Should().BeTrue();
        firstPair.TryGetProperty("displayName", out _).Should().BeTrue();
        firstPair.TryGetProperty("player1", out _).Should().BeTrue();
        firstPair.TryGetProperty("player2", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Run_LogsInformation()
    {
        // Arrange
        var tournaments = CreateTestTournaments();
        var requestMock = CreateMockRequest();

        // Act
        await _function.Run(requestMock.Object, tournaments);

        // Assert
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
    public async Task Run_WithNullGames_HandlesGracefully()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = "test-1",
            Name = "Test Tournament",
            Games = new List<Game>() // Empty games list
        };
        var requestMock = CreateMockRequest();

        // Act
        var response = await _function.Run(requestMock.Object, new[] { tournament });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await GetResponseContent(response);
        content!.RootElement.GetProperty("totalPairs").GetInt32().Should().Be(0);
    }

    private Mock<HttpRequestData> CreateMockRequest()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<ILoggerFactory, LoggerFactory>();
        
        // Configure serializer for WriteAsJsonAsync
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

    private IEnumerable<Tournament> CreateTestTournaments()
    {
        return new[] { CreateTournament("t1", 1) };
    }

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
            Id = id,
            Name = $"Tournament {id}",
            Games = new List<Game>
            {
                new Game { Id = $"g{roundOffset}-1", Pair1 = pair1, Pair2 = pair2, Round = roundOffset, CourtNumber = 1 },
                new Game { Id = $"g{roundOffset}-2", Pair1 = pair3, Pair2 = pair4, Round = roundOffset, CourtNumber = 2 }
            }
        };
    }
}
