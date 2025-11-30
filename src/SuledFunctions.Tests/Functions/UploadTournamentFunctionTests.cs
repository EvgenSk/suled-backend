using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Configuration;
using SuledFunctions.Functions;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SuledFunctions.Tests.Functions;

public class UploadTournamentFunctionTests
{
    private readonly Mock<ILogger<UploadTournamentFunction>> _loggerMock;
    private readonly Mock<IExcelParserService> _excelParserMock;
    private readonly Mock<ITournamentRepository> _repositoryMock;
    private readonly Mock<IValidator<Stream>> _fileValidatorMock;
    private readonly IOptions<TournamentSettings> _settings;
    private readonly UploadTournamentFunction _function;

    public UploadTournamentFunctionTests()
    {
        _loggerMock = new Mock<ILogger<UploadTournamentFunction>>();
        _excelParserMock = new Mock<IExcelParserService>();
        _repositoryMock = new Mock<ITournamentRepository>();
        _fileValidatorMock = new Mock<IValidator<Stream>>();
        _settings = Options.Create(new TournamentSettings
        {
            RequestTimeoutSeconds = 30,
            MaxUploadSizeBytes = 10 * 1024 * 1024
        });
        
        // Setup file validator to return valid by default
        _fileValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        
        _function = new UploadTournamentFunction(
            _excelParserMock.Object, 
            _repositoryMock.Object,
            _settings,
            _fileValidatorMock.Object,
            _loggerMock.Object);
        
        // Setup repository mock to succeed by default
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<TournamentCompact>(), default))
            .ReturnsAsync((TournamentCompact t, CancellationToken ct) => t);
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
        var result = await _function.Run(requestMock.Object);

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
        await _function.Run(requestMock.Object);

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
        var result = await _function.Run(requestMock.Object);

        // Assert
        var content = await GetResponseContent(result);
        
        // CreatedResponse wraps the data
        var apiResponse = content!.RootElement;
        apiResponse.GetProperty("success").GetBoolean().Should().BeTrue();
        apiResponse.GetProperty("message").GetString().Should().Be("Tournament uploaded successfully");
        
        var data = apiResponse.GetProperty("data");
        data.GetProperty("id").GetString().Should().Be(tournament.Id);
        data.GetProperty("name").GetString().Should().Be(tournament.Name);
        data.GetProperty("gameCount").GetInt32().Should().Be(tournament.Pairs.Sum(p => p.Games.Count));
    }

    [Fact]
    public async Task Run_WithNonMultipartRequest_ThrowsValidationException()
    {
        // Arrange
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "application/json");
        requestMock.Setup(r => r.Headers).Returns(headers);

        // Act
        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _function.Run(requestMock.Object));
    }

    [Fact]
    public async Task Run_WithMissingContentType_ThrowsValidationException()
    {
        // Arrange
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        requestMock.Setup(r => r.Headers).Returns(headers);

        // Act & Assert
        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _function.Run(requestMock.Object));
    }

    [Fact]
    public async Task Run_WhenParserThrowsException_ThrowsFileProcessingException()
    {
        // Arrange
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Parse error"));

        var requestMock = CreateMockMultipartRequest();

        // Act & Assert
        await Assert.ThrowsAsync<SuledFunctions.Exceptions.FileProcessingException>(() => _function.Run(requestMock.Object));
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
        await _function.Run(requestMock.Object);

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

        // Act & Assert
        await Assert.ThrowsAsync<SuledFunctions.Exceptions.FileProcessingException>(() => _function.Run(requestMock.Object));
        
        // Verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("parsing or saving tournament")),
                It.IsAny<Exception>(),
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
        await _function.Run(requestMock.Object);

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
        await _function.Run(requestMock.Object);

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
            BlobFileName = "test.xlsx"
        };
    }
}


