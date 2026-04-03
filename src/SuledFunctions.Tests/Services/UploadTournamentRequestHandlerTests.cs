using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Configuration;
using SuledFunctions.Services.Handlers;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Tests.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SuledFunctions.Tests.Services;

public class UploadTournamentRequestHandlerTests
{
    private readonly Mock<ILogger<UploadTournamentRequestHandler>> _loggerMock;
    private readonly Mock<ITournamentUploadService> _uploadServiceMock;
    private readonly IOptions<TournamentSettings> _settings;
    private readonly UploadTournamentRequestHandler _handler;

    public UploadTournamentRequestHandlerTests()
    {
        _loggerMock = new Mock<ILogger<UploadTournamentRequestHandler>>();
        _uploadServiceMock = new Mock<ITournamentUploadService>();
        _settings = Options.Create(new TournamentSettings
        {
            RequestTimeoutSeconds = 30,
            MaxUploadSizeBytes = 10 * 1024 * 1024
        });

        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new TournamentUploadResult("test-id", "Test Tournament", 5, 2));

        _handler = new UploadTournamentRequestHandler(
            _uploadServiceMock.Object,
            _settings,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidMultipartRequest_ReturnsCreatedStatus()
    {
        var requestMock = CreateMockMultipartRequest();

        var result = await _handler.HandleAsync(requestMock.Object);

        result.Should().NotBeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_CallsUploadService()
    {
        var requestMock = CreateMockMultipartRequest();

        await _handler.HandleAsync(requestMock.Object);

        _uploadServiceMock.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ReturnsCorrectResponseContent()
    {
        var uploadResult = new TournamentUploadResult("test-tournament-1", "Test Tournament", 10, 4);
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(uploadResult);

        var requestMock = CreateMockMultipartRequest();

        var result = await _handler.HandleAsync(requestMock.Object);

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
    public async Task HandleAsync_WithNonMultipartRequest_ThrowsValidationException()
    {
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", "application/json");
        requestMock.Setup(r => r.Headers).Returns(headers);

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(
            () => _handler.HandleAsync(requestMock.Object));
    }

    [Fact]
    public async Task HandleAsync_WithMissingContentType_ThrowsValidationException()
    {
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        requestMock.Setup(r => r.Headers).Returns(headers);

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(
            () => _handler.HandleAsync(requestMock.Object));
    }

    [Fact]
    public async Task HandleAsync_WhenUploadServiceThrowsException_ThrowsFileProcessingException()
    {
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Upload error"));

        var requestMock = CreateMockMultipartRequest();

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.FileProcessingException>(
            () => _handler.HandleAsync(requestMock.Object));
    }

    [Fact]
    public async Task HandleAsync_LogsInformationOnStart()
    {
        var requestMock = CreateMockMultipartRequest();

        await _handler.HandleAsync(requestMock.Object);

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
    public async Task HandleAsync_LogsErrorOnException()
    {
        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        var requestMock = CreateMockMultipartRequest();

        await Assert.ThrowsAsync<SuledFunctions.Exceptions.FileProcessingException>(
            () => _handler.HandleAsync(requestMock.Object));

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
    public async Task HandleAsync_WithContentDispositionHeader_ExtractsFileName()
    {
        var expectedFileName = "my-tournament.xlsx";
        string? capturedFileName = null;

        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, fn) => capturedFileName = fn)
            .ReturnsAsync(new TournamentUploadResult("id", "name", 0, 0));

        // Filename is in the multipart part's Content-Disposition
        var requestMock = CreateMockMultipartRequest(partFileName: expectedFileName);

        await _handler.HandleAsync(requestMock.Object);

        capturedFileName.Should().Be(expectedFileName);
    }

    [Fact]
    public async Task HandleAsync_WithoutContentDispositionHeader_UsesDefaultFileName()
    {
        string? capturedFileName = null;

        _uploadServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, fn) => capturedFileName = fn)
            .ReturnsAsync(new TournamentUploadResult("id", "name", 0, 0));

        // Part has no filename → falls back to request-level Content-Disposition → not present → default
        var requestMock = CreateMockMultipartRequest(partFileName: null);

        await _handler.HandleAsync(requestMock.Object);

        capturedFileName.Should().Be("tournament.xlsx");
    }

    private Mock<HttpRequestData> CreateMockRequest()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<ILoggerFactory, LoggerFactory>();
        serviceCollection.AddSingleton(Options.Create(new Microsoft.Azure.Functions.Worker.WorkerOptions
        {
            Serializer = new TestJsonSerializer()
        }));

        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, serviceCollection.BuildServiceProvider());

        var requestMock = new Mock<HttpRequestData>(context.Object);
        requestMock.Setup(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes("test content")));

        var responseMock = new Mock<HttpResponseData>(context.Object);
        responseMock.SetupProperty(r => r.StatusCode);
        responseMock.SetupProperty(r => r.Body, new MemoryStream());
        responseMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

        return requestMock;
    }

    private const string MultipartBoundary = "----WebKitFormBoundary";

    private Mock<HttpRequestData> CreateMockMultipartRequest(string? partFileName = "tournament.xlsx")
    {
        var requestMock = CreateMockRequest();
        var headers = new HttpHeadersCollection();
        headers.Add("Content-Type", $"multipart/form-data; boundary={MultipartBoundary}");
        requestMock.Setup(r => r.Headers).Returns(headers);
        requestMock.Setup(r => r.Body).Returns(
            BuildMultipartBody(MultipartBoundary, partFileName, Encoding.UTF8.GetBytes("test file content")));
        return requestMock;
    }

    private static MemoryStream BuildMultipartBody(string boundary, string? fileName, byte[] content)
    {
        var ms = new MemoryStream();
        var crlf = "\r\n"u8.ToArray();

        void WriteLine(string line)
        {
            var bytes = Encoding.UTF8.GetBytes(line);
            ms.Write(bytes);
            ms.Write(crlf);
        }

        WriteLine($"--{boundary}");
        if (fileName != null)
            WriteLine($"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"");
        else
            WriteLine("Content-Disposition: form-data; name=\"file\"");
        ms.Write(crlf); // blank line between headers and body
        ms.Write(content);
        ms.Write(crlf);
        WriteLine($"--{boundary}--");

        ms.Position = 0;
        return ms;
    }

    private static async Task<JsonDocument?> GetResponseContent(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var content = await reader.ReadToEndAsync();
        return string.IsNullOrEmpty(content) ? null : JsonDocument.Parse(content);
    }
}
