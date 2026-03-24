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
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SuledFunctions.Tests.Functions;

public class UploadTournamentFunctionTests
{
    private readonly Mock<ILogger<UploadTournamentFunction>> _loggerMock;
    private readonly Mock<ITournamentUploadService> _uploadServiceMock;
    private readonly Mock<IValidator<Stream>> _fileValidatorMock;
    private readonly IOptions<TournamentSettings> _settings;
    private readonly UploadTournamentFunction _function;

    public UploadTournamentFunctionTests()
    {
        _loggerMock = new Mock<ILogger<UploadTournamentFunction>>();
        _uploadServiceMock = new Mock<ITournamentUploadService>();
        _fileValidatorMock = new Mock<IValidator<Stream>>();
        _settings = Options.Create(new TournamentSettings
        {
            RequestTimeoutSeconds = 30,
            MaxUploadSizeBytes = 10 * 1024 * 1024
        });

        // Setup file validator to return valid by default
        _fileValidatorMock.Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Setup upload service to succeed by default
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new TournamentUploadResult("test-id", "Test Tournament", 5, 2));

        _function = new UploadTournamentFunction(
            _uploadServiceMock.Object,
            _settings,
            _fileValidatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Run_WithValidMultipartRequest_ReturnsCreatedStatus()
    {
        var requestMock = CreateMockMultipartRequest();

        var result = await _function.Run(requestMock.Object);

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Run_WithValidRequest_CallsUploadService()
    {
        var requestMock = CreateMockMultipartRequest();

        await _function.Run(requestMock.Object);

        _uploadServiceMock.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithValidRequest_ReturnsCorrectResponseContent()
    {
        var uploadResult = new TournamentUploadResult("test-tournament-1", "Test Tournament", 10, 4);
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(uploadResult);

        var requestMock = CreateMockMultipartRequest();

        var result = await _function.Run(requestMock.Object);

        var content = await GetResponseContent(result);
        var apiResponse = content!.RootElement;
        apiResponse.GetProperty("success").GetBoolean().Should().BeTrue();
        apiResponse.GetProperty("message").GetString().Should().Be("Tournament uploaded successfully");

        var data = apiResponse.GetProperty("data");
        data.GetProperty("id").GetString().Should().Be(uploadResult.Id);
        data.GetProperty("name").GetString().Should().Be(uploadResult.Name);
        data.GetProperty("gameCount").GetInt32().Should().Be(uploadResult.GameCount);
    }

    [Fact]
    public async Task Run_WithNonMultipartRequest_ThrowsValidationException()
    {
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "application/json");
        requestMock.Setup(r => r.Headers).Returns(headers);

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _function.Run(requestMock.Object));
    }

    [Fact]
    public async Task Run_WithMissingContentType_ThrowsValidationException()
    {
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        requestMock.Setup(r => r.Headers).Returns(headers);

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _function.Run(requestMock.Object));
    }

    [Fact]
    public async Task Run_WhenUploadServiceThrowsException_ThrowsFileProcessingException()
    {
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Upload error"));

        var requestMock = CreateMockMultipartRequest();

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.FileProcessingException>(() => _function.Run(requestMock.Object));
    }

    [Fact]
    public async Task Run_LogsInformationOnStart()
    {
        var requestMock = CreateMockMultipartRequest();

        await _function.Run(requestMock.Object);

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
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        var requestMock = CreateMockMultipartRequest();

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.FileProcessingException>(() => _function.Run(requestMock.Object));

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
        var expectedFileName = "my-tournament.xlsx";
        string? capturedFileName = null;

        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, fn) => capturedFileName = fn)
            .ReturnsAsync(new TournamentUploadResult("id", "name", 0, 0));

        var requestMock = CreateMockMultipartRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "multipart/form-data; boundary=----WebKitFormBoundary");
        headers.Add("Content-Disposition", $"attachment; filename=\"{expectedFileName}\"");
        requestMock.Setup(r => r.Headers).Returns(headers);

        await _function.Run(requestMock.Object);

        capturedFileName.Should().Be(expectedFileName);
    }

    [Fact]
    public async Task Run_WithoutContentDispositionHeader_UsesDefaultFileName()
    {
        string? capturedFileName = null;

        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, fn) => capturedFileName = fn)
            .ReturnsAsync(new TournamentUploadResult("id", "name", 0, 0));

        var requestMock = CreateMockMultipartRequest();

        await _function.Run(requestMock.Object);

        capturedFileName.Should().Be("tournament.xlsx");
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
}
