using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeOpenXml;
using SuledFunctions.Models;
using SuledFunctions.Services;

namespace SuledFunctions.Tests.Services;

public class ExcelParserServiceTests : IDisposable
{
    private readonly Mock<ILogger<ExcelParserService>> _loggerMock;
    private readonly ExcelParserService _service;

    public ExcelParserServiceTests()
    {
        // Configure EPPlus license for tests
        ExcelPackage.License.SetNonCommercialPersonal("Test");
        
        _loggerMock = new Mock<ILogger<ExcelParserService>>();
        _service = new ExcelParserService(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public async Task ParseTournamentAsync_WithValidExcel_ReturnsTournament()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test-tournament.xlsx");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("test-tournament");
        result.BlobFileName.Should().Be("test-tournament.xlsx");
        result.Games.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseTournamentAsync_WithEmptyWorksheet_ReturnsEmptyTournament()
    {
        // Arrange
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Empty");
        using var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;

        // Act
        var result = await _service.ParseTournamentAsync(stream, "empty.xlsx");

        // Assert
        result.Should().NotBeNull();
        result.Games.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseTournamentAsync_ParsesSingleGameCorrectly()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        var game = result.Games.Should().ContainSingle().Subject;
        game.Round.Should().Be(1);
        game.CourtNumber.Should().Be(1);
        game.Pair1.Player1.Name.Should().Be("John");
        game.Pair1.Player1.Surname.Should().Be("Doe");
        game.Pair1.Player2.Name.Should().Be("Jane");
        game.Pair1.Player2.Surname.Should().Be("Smith");
        game.Pair2.Player1.Name.Should().Be("Alice");
        game.Pair2.Player1.Surname.Should().Be("Brown");
        game.Pair2.Player2.Name.Should().Be("Bob");
        game.Pair2.Player2.Surname.Should().Be("White");
        game.Status.Should().Be(GameStatus.Scheduled);
    }

    [Fact]
    public async Task ParseTournamentAsync_HandlesMultipleRounds()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris"),
            ("Round 2", 1, "John Doe", "Bob White", "Jane Smith", "Alice Brown"),
            ("", 2, "Charlie Davis", "Grace Harris", "Diana Evans", "Frank Green")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        result.Games.Should().HaveCount(4);
        result.Games.Count(g => g.Round == 1).Should().Be(2);
        result.Games.Count(g => g.Round == 2).Should().Be(2);
    }

    [Fact]
    public async Task ParseTournamentAsync_SkipsEmptyRows()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 0, "", "", "", ""), // Empty row
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        result.Games.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseTournamentAsync_HandlesPlayerWithSingleName()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "Madonna", "Prince", "Cher", "Bono")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        var game = result.Games.Should().ContainSingle().Subject;
        game.Pair1.Player1.Name.Should().Be("Madonna");
        game.Pair1.Player1.Surname.Should().BeNullOrEmpty();
        game.Pair1.Player2.Name.Should().Be("Prince");
        game.Pair1.Player2.Surname.Should().BeNullOrEmpty();
        game.Pair2.Player1.Name.Should().Be("Cher");
        game.Pair2.Player1.Surname.Should().BeNullOrEmpty();
        game.Pair2.Player2.Name.Should().Be("Bono");
        game.Pair2.Player2.Surname.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ParseTournamentAsync_HandlesPlayerWithMultipleLastNames()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John von der Berg", "Jane van Smith", "Alice de la Cruz", "Bob O'Brien")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        var game = result.Games.Should().ContainSingle().Subject;
        game.Pair1.Player1.Name.Should().Be("John");
        game.Pair1.Player1.Surname.Should().Be("von der Berg");
        game.Pair1.Player2.Name.Should().Be("Jane");
        game.Pair1.Player2.Surname.Should().Be("van Smith");
        game.Pair2.Player1.Name.Should().Be("Alice");
        game.Pair2.Player1.Surname.Should().Be("de la Cruz");
        game.Pair2.Player2.Name.Should().Be("Bob");
        game.Pair2.Player2.Surname.Should().Be("O'Brien");
    }

    [Fact]
    public async Task ParseTournamentAsync_SkipsRowsWithInvalidCourtNumber()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 0, "Invalid Row", "Should Skip", "Test Data", "Ignore"), // Invalid court number
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        result.Games.Should().HaveCount(2);
        result.Games.Should().OnlyContain(g => g.CourtNumber > 0);
    }

    [Fact]
    public async Task ParseTournamentAsync_HandlesGermanRoundFormat()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Runde 3", 1, "Hans Schmidt", "Petra Mueller", "Klaus Wagner", "Anna Becker")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        var game = result.Games.Should().ContainSingle().Subject;
        game.Round.Should().Be(3);
    }

    [Fact]
    public async Task ParseTournamentAsync_SkipsRowsWithMissingPairs()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 2, "", "", "Charlie Davis", "Diana Evans"), // Missing Pair1
            ("", 3, "Frank Green", "Grace Harris", "", "") // Missing Pair2
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        result.Games.Should().ContainSingle();
    }

    [Fact]
    public async Task ParseTournamentAsync_LogsInformationOnSuccess()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White")
        );

        // Act
        await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Parsed") && v.ToString()!.Contains("games")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ParseTournamentAsync_AssignsUniqueTournamentId()
    {
        // Arrange
        using var stream = CreateTestExcelStream(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White")
        );

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        result.Id.Should().NotBeNullOrEmpty();
        result.Games.Should().OnlyContain(g => g.TournamentId == result.Id);
    }

    [Fact]
    public async Task ParseTournamentAsync_HandlesExtraWhitespace()
    {
        // Arrange
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Test");
        
        // Add header
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        
        // Add game with extra whitespace
        worksheet.Cells[2, 1].Value = "  Round   1  ";
        worksheet.Cells[2, 2].Value = " 1 ";
        worksheet.Cells[2, 3].Value = "  John   Doe  ";
        worksheet.Cells[2, 4].Value = "  Jane   Smith  ";
        worksheet.Cells[2, 7].Value = "  Alice   Brown  ";
        worksheet.Cells[2, 8].Value = "  Bob   White  ";

        using var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;

        // Act
        var result = await _service.ParseTournamentAsync(stream, "test.xlsx");

        // Assert
        var game = result.Games.Should().ContainSingle().Subject;
        game.Pair1.Player1.Name.Should().Be("John");
        game.Pair1.Player1.Surname.Should().Be("Doe");
        game.Pair1.Player2.Name.Should().Be("Jane");
        game.Pair1.Player2.Surname.Should().Be("Smith");
    }

    private MemoryStream CreateTestExcelStream(params (string round, int court, string p1_1, string p1_2, string p2_1, string p2_2)[] games)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Test");
        
        // Add header row
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        worksheet.Cells[1, 3].Value = "Player 1.1";
        worksheet.Cells[1, 4].Value = "Player 1.2";
        worksheet.Cells[1, 7].Value = "Player 2.1";
        worksheet.Cells[1, 8].Value = "Player 2.2";

        // Add game data
        for (int i = 0; i < games.Length; i++)
        {
            int row = i + 2;
            var game = games[i];
            
            if (!string.IsNullOrEmpty(game.round))
                worksheet.Cells[row, 1].Value = game.round;
            if (game.court > 0)
                worksheet.Cells[row, 2].Value = game.court;
            
            worksheet.Cells[row, 3].Value = game.p1_1;
            worksheet.Cells[row, 4].Value = game.p1_2;
            worksheet.Cells[row, 7].Value = game.p2_1;
            worksheet.Cells[row, 8].Value = game.p2_2;
        }

        var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
