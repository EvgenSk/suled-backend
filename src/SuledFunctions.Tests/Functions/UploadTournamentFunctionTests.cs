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
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace SuledFunctions.Tests.Functions;

public class UploadTournamentFunctionTests
{
    private readonly Mock<ILogger<UploadTournamentFunction>> _loggerMock;
    private readonly Mock<IExcelParserService> _excelParserMock;
    private readonly Mock<CosmosClient> _cosmosClientMock;
    private readonly UploadTournamentFunction _function;

    public UploadTournamentFunctionTests()
    {
        _loggerMock = new Mock<ILogger<UploadTournamentFunction>>();
        _excelParserMock = new Mock<IExcelParserService>();
        _cosmosClientMock = new Mock<CosmosClient>();
        _function = new UploadTournamentFunction(_loggerMock.Object, _excelParserMock.Object);
        
        // Setup Cosmos DB mocks
        Environment.SetEnvironmentVariable("CosmosDbName", "TestDb");
        Environment.SetEnvironmentVariable("CosmosContainerName", "TestContainer");
        
        var databaseMock = new Mock<Database>();
        var containerMock = new Mock<Container>();
        var responseMock = new Mock<ItemResponse<Tournament>>();
        
        _cosmosClientMock.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(databaseMock.Object);
        databaseMock.Setup(d => d.GetContainer(It.IsAny<string>())).Returns(containerMock.Object);
        containerMock.Setup(c => c.CreateItemAsync(
            It.IsAny<Tournament>(),
            It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(responseMock.Object);
    }

    [Fact]
    public async Task Run_WithValidMultipartRequest_ReturnsCreatedStatus()
    {
        // Arrange
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        var requestMock = CreateMockMultipartRequest();

        // Act
        var result = await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Run_WithValidRequest_CallsExcelParser()
    {
        // Arrange
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        var requestMock = CreateMockMultipartRequest();

        // Act
        await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        _excelParserMock.Verify(
            x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithValidRequest_ReturnsCorrectResponseContent()
    {
        // Arrange
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        var requestMock = CreateMockMultipartRequest();

        // Act
        var result = await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        var content = await GetResponseContent(result);
        
        content!.RootElement.GetProperty("id").GetString().Should().Be(tournament.Id);
        content.RootElement.GetProperty("name").GetString().Should().Be(tournament.Name);
        content.RootElement.GetProperty("gameCount").GetInt32().Should().Be(tournament.Games.Count);
        content.RootElement.GetProperty("message").GetString().Should().Be("Tournament uploaded successfully");
    }

    [Fact]
    public async Task Run_WithNonMultipartRequest_ReturnsBadRequest()
    {
        // Arrange
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "application/json");
        requestMock.Setup(r => r.Headers).Returns(headers);

        // Act
        var result = await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await GetResponseContent(result);
        content!.RootElement.GetProperty("error").GetString().Should().Contain("multipart/form-data");
    }

    [Fact]
    public async Task Run_WithMissingContentType_ReturnsBadRequest()
    {
        // Arrange
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        requestMock.Setup(r => r.Headers).Returns(headers);

        // Act
        var result = await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Run_WhenParserThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Parse error"));

        var requestMock = CreateMockMultipartRequest();

        // Act
        var result = await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        var content = await GetResponseContent(result);
        content!.RootElement.GetProperty("error").GetString().Should().Contain("Failed to process tournament file");
    }

    [Fact]
    public async Task Run_LogsInformationOnStart()
    {
        // Arrange
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        var requestMock = CreateMockMultipartRequest();

        // Act
        await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing tournament upload request")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_LogsErrorOnException()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(exception);

        var requestMock = CreateMockMultipartRequest();

        // Act
        await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error uploading tournament")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithContentDispositionHeader_ExtractsFileName()
    {
        // Arrange
        var tournament = CreateTestTournament();
        var expectedFileName = "my-tournament.xlsx";
        string? capturedFileName = null;

        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, fileName) => capturedFileName = fileName)
            .ReturnsAsync(tournament);

        var requestMock = CreateMockMultipartRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "multipart/form-data; boundary=----WebKitFormBoundary");
        headers.Add("Content-Disposition", $"attachment; filename=\"{expectedFileName}\"");
        requestMock.Setup(r => r.Headers).Returns(headers);

        // Act
        await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        capturedFileName.Should().Be(expectedFileName);
    }

    [Fact]
    public async Task Run_WithoutContentDispositionHeader_UsesDefaultFileName()
    {
        // Arrange
        var tournament = CreateTestTournament();
        string? capturedFileName = null;

        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, fileName) => capturedFileName = fileName)
            .ReturnsAsync(tournament);

        var requestMock = CreateMockMultipartRequest();

        // Act
        await _function.Run(requestMock.Object, _cosmosClientMock.Object);

        // Assert
        capturedFileName.Should().Be("tournament.xlsx");
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
        
        var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        requestMock.Setup(r => r.Body).Returns(bodyStream);
        
        var responseStream = new MemoryStream();
        var responseMock = new Mock<HttpResponseData>(context.Object);
        responseMock.SetupProperty(r => r.StatusCode);
        responseMock.SetupProperty(r => r.Body, responseStream);
        responseMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        
        requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);
        
        return requestMock;
    }

    private Mock<HttpRequestData> CreateMockMultipartRequest()
    {
        var requestMock = CreateMockRequest();
        
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "multipart/form-data; boundary=----WebKitFormBoundary");
        requestMock.Setup(r => r.Headers).Returns(headers);
        
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

    private Tournament CreateTestTournament()
    {
        var player1 = new Player { Name = "John", Surname = "Doe" };
        var player2 = new Player { Name = "Jane", Surname = "Smith" };
        var pair1 = new Pair { Player1 = player1, Player2 = player2 };

        var player3 = new Player { Name = "Alice", Surname = "Brown" };
        var player4 = new Player { Name = "Bob", Surname = "White" };
        var pair2 = new Pair { Player1 = player3, Player2 = player4 };

        return new Tournament
        {
            Id = "test-tournament-1",
            Name = "Test Tournament",
            BlobFileName = "test.xlsx",
            Games = new List<Game>
            {
                new Game
                {
                    Id = "g1",
                    TournamentId = "test-tournament-1",
                    Pair1 = pair1,
                    Pair2 = pair2,
                    Round = 1,
                    CourtNumber = 1,
                    Status = GameStatus.Scheduled
                },
                new Game
                {
                    Id = "g2",
                    TournamentId = "test-tournament-1",
                    Pair1 = pair1,
                    Pair2 = pair2,
                    Round = 1,
                    CourtNumber = 2,
                    Status = GameStatus.Scheduled
                }
            }
        };
    }
}
