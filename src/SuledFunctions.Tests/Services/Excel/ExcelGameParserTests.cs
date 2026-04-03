using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel;

namespace SuledFunctions.Tests.Services.Excel;

public class ExcelGameParserTests
{
    private readonly Mock<ILogger<ExcelGameParser>> _loggerMock;
    private readonly ExcelGameParser _parser;

    public ExcelGameParserTests()
    {
        _loggerMock = new Mock<ILogger<ExcelGameParser>>();
        _parser = new ExcelGameParser(_loggerMock.Object);
    }

    [Fact]
    public void ParseGames_WithValidGames_ReturnsCorrectGames()
    {
        // Arrange
        var rows = CreateTestRows(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 2, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris")
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

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
        var rows = CreateTestRows(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("Round 2", 1, "John Doe", "Jane Smith", "Charlie Davis", "Diana Evans"),
            ("Round 3", 1, "Alice Brown", "Bob White", "Charlie Davis", "Diana Evans")
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

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
        var rows = CreateTestRows(
            ("Runde 5", 1, "Hans Schmidt", "Petra Mueller", "Klaus Wagner", "Anna Becker")
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().ContainSingle();
        games[0].Round.Should().Be(5);
    }

    [Fact]
    public void ParseGames_WithEmptyWorksheet_ReturnsEmptyList()
    {
        // Arrange
        var rows = Array.Empty<string[]>();
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().BeEmpty();
    }

    [Fact]
    public void ParseGames_WithEmptyRows_SkipsEmptyRows()
    {
        // Arrange
        // Header row
        var rows = new string[][]
        {
            ["Round", "Court", "Player 1.1", "Player 1.2", "", "", "Player 2.1", "Player 2.2"],
            // Row 1: Valid game
            ["Round 1", "1", "John Doe", "Jane Smith", "", "", "Alice Brown", "Bob White"],
            // Row 2: Empty
            ["", "", "", "", "", "", "", ""],
            // Row 3: Valid game
            ["", "2", "Charlie Davis", "Diana Evans", "", "", "Frank Green", "Grace Harris"],
        };
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().HaveCount(2);
    }

    [Fact]
    public void ParseGames_WithMissingCourtNumber_SkipsRow()
    {
        // Arrange
        var rows = CreateTestRows(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("", 0, "Charlie Davis", "Diana Evans", "Frank Green", "Grace Harris") // Court = 0 (invalid)
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().ContainSingle(); // Only first valid game
    }

    [Fact]
    public void ParseGames_WithMissingPairs_SkipsRow()
    {
        // Arrange
        var rows = new string[][]
        {
            ["Round", "Court", "Player 1.1", "Player 1.2", "", "", "Player 2.1", "Player 2.2"],
            // Row 1: Valid game
            ["Round 1", "1", "John Doe", "Jane Smith", "", "", "Alice Brown", "Bob White"],
            // Row 2: Missing Pair2
            ["", "2", "Charlie Davis", "Diana Evans", "", "", "", ""],
        };
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().ContainSingle(); // Only first valid game
    }

    [Fact]
    public void ParseGames_WithExtraWhitespace_CleansPlayerNames()
    {
        // Arrange
        var rows = new string[][]
        {
            ["Round", "Court", "Player 1.1", "Player 1.2", "", "", "Player 2.1", "Player 2.2"],
            ["Round 1", "1", "  John   Doe  ", "  Jane   Smith  ", "", "", "  Alice   Brown  ", "  Bob   White  "],
        };
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

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
        var rows = CreateTestRows(
            ("Round 1", 1, "John", "Jane", "Alice", "Bob")
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

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
        var rows = CreateTestRows(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White")
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().ContainSingle();
        games[0].Status.Should().Be(GameStatus.Scheduled);
    }

    [Fact]
    public void ParseGames_GeneratesDeterministicPairIds()
    {
        // Arrange
        var rows = CreateTestRows(
            ("Round 1", 1, "John Doe", "Jane Smith", "Alice Brown", "Bob White"),
            ("Round 2", 1, "John Doe", "Jane Smith", "Charlie Davis", "Diana Evans") // Same pair1
        );
        var tournamentId = Guid.NewGuid().ToString();

        // Act
        var games = _parser.ParseGames(rows, tournamentId);

        // Assert
        games.Should().HaveCount(2);
        games[0].Pair1.Id.Should().Be(games[1].Pair1.Id); // Same pair should have same ID
    }

    /// <summary>
    /// Creates a string[][] (header + data rows) matching the expected column layout:
    /// col 0 = round, col 1 = court, col 2 = p1_1, col 3 = p1_2, col 6 = p2_1, col 7 = p2_2
    /// </summary>
    private static string[][] CreateTestRows(params (string round, int court, string p1_1, string p1_2, string p2_1, string p2_2)[] games)
    {
        var rowList = new List<string[]>();

        // Header row
        rowList.Add(["Round", "Court", "Player 1.1", "Player 1.2", "", "", "Player 2.1", "Player 2.2"]);

        foreach (var game in games)
        {
            var row = new string[8];
            row[0] = game.round;
            row[1] = game.court > 0 ? game.court.ToString() : "";
            row[2] = game.p1_1;
            row[3] = game.p1_2;
            row[4] = "";
            row[5] = "";
            row[6] = game.p2_1;
            row[7] = game.p2_2;
            rowList.Add(row);
        }

        return rowList.ToArray();
    }
}
