using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using SuledFunctions.Exceptions;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services;
using SuledFunctions.Services.Excel.Interfaces;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Tests.Services;

public class TournamentUploadServiceTests
{
    private readonly Mock<IExcelParserService> _excelParserMock;
    private readonly Mock<ITournamentRepository> _repositoryMock;
    private readonly Mock<IValidator<Stream>> _fileValidatorMock;
    private readonly TournamentUploadService _service;

    public TournamentUploadServiceTests()
    {
        _excelParserMock = new Mock<IExcelParserService>();
        _repositoryMock = new Mock<ITournamentRepository>();
        _fileValidatorMock = new Mock<IValidator<Stream>>();

        _fileValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new TournamentUploadService(_excelParserMock.Object, _repositoryMock.Object, _fileValidatorMock.Object);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<TournamentCompact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TournamentCompact t, CancellationToken _) => t);
    }

    [Fact]
    public async Task UploadAsync_CallsExcelParser_WithStreamAndFileName()
    {
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(p => p.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        using var stream = new MemoryStream();
        await _service.UploadAsync(stream, "test.xlsx");

        _excelParserMock.Verify(p => p.ParseTournamentAsync(stream, "test.xlsx"), Times.Once);
    }

    [Fact]
    public async Task UploadAsync_SavesCompactTournamentToRepository()
    {
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(p => p.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        using var stream = new MemoryStream();
        await _service.UploadAsync(stream, "test.xlsx");

        _repositoryMock.Verify(
            r => r.CreateAsync(It.Is<TournamentCompact>(c => c.Id == tournament.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_ReturnsResultWithCorrectFields()
    {
        var tournament = CreateTestTournament();
        _excelParserMock
            .Setup(p => p.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(tournament);

        using var stream = new MemoryStream();
        var result = await _service.UploadAsync(stream, "test.xlsx");

        result.Id.Should().Be(tournament.Id);
        result.Name.Should().Be(tournament.Name);
        result.PairCount.Should().Be(tournament.Pairs.Count);
        result.GameCount.Should().Be(tournament.Pairs.Sum(p => p.Games.Count));
    }

    [Fact]
    public async Task UploadAsync_PropagatesExceptionFromParser()
    {
        _excelParserMock
            .Setup(p => p.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Parse failed"));

        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<Exception>(() => _service.UploadAsync(stream, "test.xlsx"));
    }

    [Fact]
    public async Task UploadAsync_WithInvalidStream_ThrowsValidationException()
    {
        _fileValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(string.Empty, "File cannot be empty")
            }));

        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _service.UploadAsync(stream, "test.xlsx"));
    }

    [Fact]
    public async Task UploadAsync_WithInvalidExtension_ThrowsValidationException()
    {
        using var stream = new MemoryStream(new byte[1]);
        var ex = await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _service.UploadAsync(stream, "file.csv"));
        ex.Errors.Should().ContainKey("fileName");
    }

    [Fact]
    public async Task UploadAsync_WithInvalidStream_DoesNotCallParser()
    {
        _fileValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(string.Empty, "File cannot be empty")
            }));

        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<SuledFunctions.Exceptions.ValidationException>(() => _service.UploadAsync(stream, "test.xlsx"));

        _excelParserMock.Verify(p => p.ParseTournamentAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
    }

    private static Tournament CreateTestTournament()
    {
        var pair1 = new Pair
        {
            Player1 = new Player { Name = "John", Surname = "Doe" },
            Player2 = new Player { Name = "Jane", Surname = "Smith" }
        };
        _ = pair1.Id;

        var pair2 = new Pair
        {
            Player1 = new Player { Name = "Alice", Surname = "Brown" },
            Player2 = new Player { Name = "Bob", Surname = "White" }
        };
        _ = pair2.Id;

        return new Tournament
        {
            Id = "test-tournament-1",
            Name = "Test Tournament",
            BlobFileName = "test.xlsx",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = pair1,
                    Games = new List<PairGame>
                    {
                        new PairGame { Id = "g1", TournamentId = "test-tournament-1", Round = 1, CourtNumber = 1, OpponentPair = pair2 },
                        new PairGame { Id = "g2", TournamentId = "test-tournament-1", Round = 2, CourtNumber = 1, OpponentPair = pair2 }
                    }
                },
                new TournamentPair
                {
                    PairInfo = pair2,
                    Games = new List<PairGame>()
                }
            }
        };
    }
}
