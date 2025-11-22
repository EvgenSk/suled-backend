using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeOpenXml;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel;

namespace SuledFunctions.Tests.Services.Excel;

public class ExcelGameParserTests : IDisposable
{
    private readonly Mock<ILogger<ExcelGameParser>> _loggerMock;
    private readonly ExcelGameParser _parser;

    public ExcelGameParserTests()
    {
        ExcelPackage.License.SetNonCommercialPersonal("Test");
        _loggerMock = new Mock<ILogger<ExcelGameParser>>();
        _parser = new ExcelGameParser(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void ParseGames_WithValidGames_ReturnsCorrectGames()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris")
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().HaveCount(2);
        games[0].TournamentId.Should().Be(tournamentId);
        games[0].Round.Should().Be(1);
        games[0].CourtNumber.Should().Be(1);
        games[0].Pair1.Player1.Name.Should().Be("John");
        games[0].Pair1.Player1.Surname.Should().Be("Doe");
        games[0].Pair1.Player2.Name.Should().Be("Jane");
        games[0].Pair1.Player2.Surname.Should().Be("Smith");
        games[0].Pair2.Player1.Name.Should().Be("Alice");
        games[0].Pair2.Player2.Name.Should().Be("Bob");
    }

    [Fact]
    public void ParseGames_WithMultipleRounds_TracksRoundNumbers()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("Round 2", 1, "John Doe", "Jane Smith", "Charlie Davis", "Diana Evans"),
            ("Round 3", 1, "Alice Brown", "Bob White", "Charlie Davis", "Diana Evans")
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().HaveCount(3);
        games[0].Round.Should().Be(1);
        games[1].Round.Should().Be(2);
        games[2].Round.Should().Be(3);
    }

    [Fact]
    public void ParseGames_WithGermanRoundFormat_ParsesCorrectly()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Runde 5", 1, "Hans Schmidt", "Petra Mueller", "Klaus Wagner", "Anna Becker")
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().ContainSingle();
        games[0].Round.Should().Be(5);
    }

    [Fact]
    public void ParseGames_WithEmptyWorksheet_ReturnsEmptyList()
    {
        // Arrange
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Empty");
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().BeEmpty();
    }

    [Fact]
    public void ParseGames_WithEmptyRows_SkipsEmptyRows()
    {
        // Arrange
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Test");
        
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        
        // Row 2: Valid game
        worksheet.Cells[2, 1].Value = "Round 1";
        worksheet.Cells[2, 2].Value = 1;
        worksheet.Cells[2, 3].Value = "John Doe";
        worksheet.Cells[2, 4].Value = "Jane Smith";
        worksheet.Cells[2, 7].Value = "Alice Brown";
        worksheet.Cells[2, 8].Value = "Bob White";
        
        // Row 3: Empty
        
        // Row 4: Valid game
        worksheet.Cells[4, 2].Value = 2;
        worksheet.Cells[4, 3].Value = "Charlie Davis";
        worksheet.Cells[4, 4].Value = "Diana Evans";
        worksheet.Cells[4, 7].Value = "Frank Green";
        worksheet.Cells[4, 8].Value = "Grace Harris";
        
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().HaveCount(2);
    }

    [Fact]
    public void ParseGames_WithMissingCourtNumber_SkipsRow()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 0, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris") // Court = 0 (invalid)
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().ContainSingle(); // Only first valid game
    }

    [Fact]
    public void ParseGames_WithMissingPairs_SkipsRow()
    {
        // Arrange
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Test");
        
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        
        // Row 2: Valid game
        worksheet.Cells[2, 1].Value = "Round 1";
        worksheet.Cells[2, 2].Value = 1;
        worksheet.Cells[2, 3].Value = "John Doe";
        worksheet.Cells[2, 4].Value = "Jane Smith";
        worksheet.Cells[2, 7].Value = "Alice Brown";
        worksheet.Cells[2, 8].Value = "Bob White";
        
        // Row 3: Missing Pair2
        worksheet.Cells[3, 2].Value = 2;
        worksheet.Cells[3, 3].Value = "Charlie Davis";
        worksheet.Cells[3, 4].Value = "Diana Evans";
        // Missing cells [3, 7] and [3, 8]
        
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().ContainSingle(); // Only first valid game
    }

    [Fact]
    public void ParseGames_WithExtraWhitespace_CleansPlayerNames()
    {
        // Arrange
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Test");
        
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        
        worksheet.Cells[2, 1].Value = "Round 1";
        worksheet.Cells[2, 2].Value = 1;
        worksheet.Cells[2, 3].Value = "  John   Doe  ";
        worksheet.Cells[2, 4].Value = "  Jane   Smith  ";
        worksheet.Cells[2, 7].Value = "  Alice   Brown  ";
        worksheet.Cells[2, 8].Value = "  Bob   White  ";
        
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().ContainSingle();
        var game = games[0];
        game.Pair1.Player1.Name.Should().Be("John");
        game.Pair1.Player1.Surname.Should().Be("Doe");
        game.Pair1.Player2.Name.Should().Be("Jane");
        game.Pair1.Player2.Surname.Should().Be("Smith");
    }

    [Fact]
    public void ParseGames_WithSingleNamePlayers_ParsesCorrectly()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Round 1", 1, "John", "Jane", "Alice", "Bob")
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().ContainSingle();
        var game = games[0];
        game.Pair1.Player1.Name.Should().Be("John");
        game.Pair1.Player1.Surname.Should().BeNullOrEmpty();
        game.Pair1.Player2.Name.Should().Be("Jane");
        game.Pair1.Player2.Surname.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ParseGames_AssignsScheduledStatus()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White")
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().ContainSingle();
        games[0].Status.Should().Be(GameStatus.Scheduled);
    }

    [Fact]
    public void ParseGames_GeneratesDeterministicPairIds()
    {
        // Arrange
        using var package = CreateTestPackage(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("Round 2", 1, "John Doe", "Jane Smith", "Charlie Davis", "Diana Evans") // Same pair1
        );
        var worksheet = package.Workbook.Worksheets[0];
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(worksheet, tournamentId);

        // Assert
        games.Should().HaveCount(2);
        games[0].Pair1.Id.Should().Be(games[1].Pair1.Id); // Same pair should have same ID
    }

    private ExcelPackage CreateTestPackage(params (string round, int court, string p1_1, string p1_2, string p2_1, string p2_2)[] games)
    {
        var package = new ExcelPackage();
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

        return package;
    }
}
