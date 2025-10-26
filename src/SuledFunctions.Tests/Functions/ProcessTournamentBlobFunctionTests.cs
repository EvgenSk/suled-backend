using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.Functions;
using SuledFunctions.Models;
using SuledFunctions.Services;
using Xunit;

namespace SuledFunctions.Tests.Functions;

public class ProcessTournamentBlobFunctionTests
{
    private readonly Mock<ILogger<ProcessTournamentBlobFunction>> _loggerMock;
    private readonly Mock<IExcelParserService> _excelParserMock;
    private readonly ProcessTournamentBlobFunction _function;

    public ProcessTournamentBlobFunctionTests()
    {
        _loggerMock = new Mock<ILogger<ProcessTournamentBlobFunction>>();
        _excelParserMock = new Mock<IExcelParserService>();
        _function = new ProcessTournamentBlobFunction(_loggerMock.Object, _excelParserMock.Object);
    }

    [Fact]
    public async Task Run_WithValidBlob_ReturnsParsedTournament()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var tournament = CreateTestTournament();
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act
        var result = await _function.Run(blobStream, blobName);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(tournament);
    }

    [Fact]
    public async Task Run_WithValidBlob_CallsExcelParserWithCorrectParameters()
    {
        // Arrange
        var blobName = "my-tournament.xlsx";
        var tournament = CreateTestTournament();
        Stream? capturedStream = null;
        string? capturedFileName = null;

        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .Callback<Stream, string>((s, name) =>
            {
                capturedStream = s;
                capturedFileName = name;
            })
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act
        await _function.Run(blobStream, blobName);

        // Assert
        capturedStream.Should().NotBeNull();
        capturedStream.Should().BeSameAs(blobStream);
        capturedFileName.Should().Be(blobName);
    }

    [Fact]
    public async Task Run_LogsBlobProcessingStart()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var tournament = CreateTestTournament();
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act
        await _function.Run(blobStream, blobName);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Processing blob") && 
                    v.ToString()!.Contains(blobName) &&
                    v.ToString()!.Contains("100")), // Size
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_LogsSuccessfulProcessing()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var tournament = CreateTestTournament();
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act
        await _function.Run(blobStream, blobName);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Successfully processed tournament") && 
                    v.ToString()!.Contains(tournament.Id) &&
                    v.ToString()!.Contains(tournament.Games.Count.ToString())),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WhenParserThrowsException_LogsErrorAndRethrows()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var exception = new Exception("Parse error");
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ThrowsAsync(exception);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _function.Run(blobStream, blobName));
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Error processing blob") && 
                    v.ToString()!.Contains(blobName)),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithDifferentBlobSizes_ProcessesCorrectly()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var tournament = CreateTestTournament();
        var sizes = new[] { 0, 1, 1024, 1024 * 1024 };

        foreach (var size in sizes)
        {
            _loggerMock.Reset();
            _excelParserMock
                .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
                .ReturnsAsync(tournament);

            using var blobStream = new MemoryStream(new byte[size]);

            // Act
            var result = await _function.Run(blobStream, blobName);

            // Assert
            result.Should().NotBeNull();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Size: {size} bytes")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                $"Failed for size {size}");
        }
    }

    [Fact]
    public async Task Run_WithSpecialCharactersInBlobName_ProcessesCorrectly()
    {
        // Arrange
        var blobName = "tournament-2024_süled-test (1).xlsx";
        var tournament = CreateTestTournament();
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act
        var result = await _function.Run(blobStream, blobName);

        // Assert
        result.Should().NotBeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(blobName)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_ReturnsOutputForCosmosDbBinding()
    {
        // Arrange
        var blobName = "test-tournament.xlsx";
        var tournament = CreateTestTournament();
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream(new byte[100]);

        // Act
        var result = await _function.Run(blobStream, blobName);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(tournament.Id);
        result.Name.Should().Be(tournament.Name);
        result.Games.Should().HaveCount(tournament.Games.Count);
    }

    [Fact]
    public async Task Run_WithEmptyStream_ProcessesCorrectly()
    {
        // Arrange
        var blobName = "empty.xlsx";
        var tournament = new Tournament
        {
            Id = "empty-tournament",
            Name = "empty",
            BlobFileName = blobName,
            Games = new List<Game>()
        };
        
        _excelParserMock
            .Setup(x => x.ParseTournamentAsync(It.IsAny<Stream>(), blobName))
            .ReturnsAsync(tournament);

        using var blobStream = new MemoryStream();

        // Act
        var result = await _function.Run(blobStream, blobName);

        // Assert
        result.Should().NotBeNull();
        result.Games.Should().BeEmpty();
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
