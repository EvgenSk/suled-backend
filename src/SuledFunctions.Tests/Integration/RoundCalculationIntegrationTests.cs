using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeOpenXml;
using SuledFunctions.Models;
using SuledFunctions.Services;
using SuledFunctions.Services.Excel;

namespace SuledFunctions.Tests.Integration;

/// <summary>
/// Integration tests for round calculation feature end-to-end
/// </summary>
public class RoundCalculationIntegrationTests : IDisposable
{
    public RoundCalculationIntegrationTests()
    {
        // Configure EPPlus license for tests
        ExcelPackage.License.SetNonCommercialPersonal("Test");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public async Task TournamentWorkflow_ParsesExcelAndCalculatesRounds()
    {
        // Arrange - Create a complete Excel file with metadata and games
        using var excelStream = CreateCompleteTestExcel();
        
        var metadataExtractor = new ExcelMetadataExtractor(
            new Mock<ILogger<ExcelMetadataExtractor>>().Object);
        var gameParser = new ExcelGameParser(
            new Mock<ILogger<ExcelGameParser>>().Object);
        var pairConverter = new PairStructureConverter();
        var roundCalculationService = new RoundCalculationService(
            new Mock<ILogger<RoundCalculationService>>().Object);
        
        var service = new ExcelParserService(
            metadataExtractor,
            gameParser,
            pairConverter,
            roundCalculationService,
            new Mock<ILogger<ExcelParserService>>().Object);

        // Act - Parse the tournament
        var tournament = await service.ParseTournamentAsync(excelStream, "tournament_22-11-2025_Chicago_DivisionA.xlsx");

        // Assert - Verify tournament metadata
        tournament.Should().NotBeNull();
        tournament.Name.Should().Be("Test Tournament"); // Extracted from metadata, not filename
        tournament.StartDate.Should().Be(new DateTime(2025, 11, 22));
        tournament.StartTime.Should().Be(new TimeSpan(9, 0, 0));
        tournament.EndTime.Should().Be(new TimeSpan(17, 0, 0));
        tournament.Location.Should().Be("Test Arena");
        tournament.Division.Should().Be("Division A");

        // Assert - Verify games were parsed
        tournament.Pairs.Should().NotBeEmpty();
        var totalGames = tournament.Pairs.Sum(p => p.Games.Count);
        totalGames.Should().BeGreaterThan(0);

        // Assert - Verify rounds were calculated
        tournament.Rounds.Should().NotBeEmpty();
        tournament.Rounds.Should().HaveCountGreaterThan(0);
        
        // Verify round structure
        foreach (var round in tournament.Rounds)
        {
            round.RoundNumber.Should().BeGreaterThan(0);
            round.StartTime.Should().BeBefore(round.EndTime);
            round.GameCount.Should().BeGreaterThan(0);
        }

        // Verify rounds are sequential
        for (int i = 1; i < tournament.Rounds.Count; i++)
        {
            var previousRound = tournament.Rounds[i - 1];
            var currentRound = tournament.Rounds[i];
            
            currentRound.StartTime.Should().BeOnOrAfter(previousRound.EndTime);
        }
    }

    [Fact]
    public async Task TournamentWorkflow_WithMultipleRounds_CalculatesCorrectTimings()
    {
        // 9:00–10:30 = 90 min, default warmup 5 min, 3 rounds, 2 breaks (10 min)
        // roundDuration = (90 - 5 - 10) / 3 = 25 min
        using var excelStream = CreateMultiRoundExcel(
            rounds: 3,
            gamesPerRound: 4,
            courts: 2,
            endTime: "10:30");
        
        var service = CreateParserService();

        // Act
        var tournament = await service.ParseTournamentAsync(excelStream, "multi-round-test.xlsx");

        // Assert
        tournament.Rounds.Should().HaveCount(3);
        
        // All rounds should have equal duration
        var round1Duration = tournament.Rounds[0].EndTime.ToTimeSpan() - tournament.Rounds[0].StartTime.ToTimeSpan();
        var round2Duration = tournament.Rounds[1].EndTime.ToTimeSpan() - tournament.Rounds[1].StartTime.ToTimeSpan();
        var round3Duration = tournament.Rounds[2].EndTime.ToTimeSpan() - tournament.Rounds[2].StartTime.ToTimeSpan();
        round1Duration.Should().Be(TimeSpan.FromMinutes(25));
        round2Duration.Should().Be(round1Duration);
        round3Duration.Should().Be(round1Duration);
        
        // 5-minute breaks between rounds
        var breakDuration = tournament.Rounds[1].StartTime.ToTimeSpan() - tournament.Rounds[0].EndTime.ToTimeSpan();
        breakDuration.Should().Be(TimeSpan.FromMinutes(5));
        
        var break2Duration = tournament.Rounds[2].StartTime.ToTimeSpan() - tournament.Rounds[1].EndTime.ToTimeSpan();
        break2Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task TournamentWorkflow_LastRoundEndsAtDeclaredEndTime()
    {
        // Verify that the scheduled rounds exactly fill the declared day window
        // 9:00–10:30 = 90 min, default warmup 5 min, 3 rounds, 2 breaks (10 min)
        // last round should end at exactly 10:30
        using var stream = CreateMultiRoundExcel(rounds: 3, gamesPerRound: 4, courts: 2, endTime: "10:30");
        var service = CreateParserService();

        var tournament = await service.ParseTournamentAsync(stream, "window-test.xlsx");

        tournament.Rounds.Should().HaveCount(3);
        var lastRoundEnd = tournament.Rounds.Last().EndTime.ToTimeSpan();
        var declaredEnd = new TimeSpan(10, 30, 0);
        Math.Abs((lastRoundEnd - declaredEnd).TotalSeconds).Should().BeLessThan(1);
    }

    [Fact]
    public async Task TournamentWorkflow_WithoutMetadata_UsesDefaults()
    {
        // Arrange - Excel with no metadata, only games
        using var stream = CreateMinimalExcel();
        var service = CreateParserService();

        // Act
        var tournament = await service.ParseTournamentAsync(stream, "minimal.xlsx");

        // Assert
        tournament.Rounds.Should().NotBeEmpty();
        // Default start 9:00 + default warmup 5 min → first round starts at 9:05
        tournament.Rounds[0].StartTime.Hour.Should().Be(9);
        tournament.Rounds[0].StartTime.Minute.Should().Be(5);
    }

    private ExcelParserService CreateParserService()
    {
        var metadataExtractor = new ExcelMetadataExtractor(
            new Mock<ILogger<ExcelMetadataExtractor>>().Object);
        var gameParser = new ExcelGameParser(
            new Mock<ILogger<ExcelGameParser>>().Object);
        var pairConverter = new PairStructureConverter();
        var roundCalculationService = new RoundCalculationService(
            new Mock<ILogger<RoundCalculationService>>().Object);
        
        return new ExcelParserService(
            metadataExtractor,
            gameParser,
            pairConverter,
            roundCalculationService,
            new Mock<ILogger<ExcelParserService>>().Object);
    }

    private MemoryStream CreateCompleteTestExcel()
    {
        var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Tournament");

        // Add metadata
        worksheet.Cells[1, 10].Value = "Tournament Name:";
        worksheet.Cells[1, 11].Value = "Test Tournament";
        worksheet.Cells[2, 10].Value = "Date:";
        worksheet.Cells[2, 11].Value = "22.11.2025";
        worksheet.Cells[3, 10].Value = "Start Time:";
        worksheet.Cells[3, 11].Value = "09:00";
        worksheet.Cells[4, 10].Value = "End Time:";
        worksheet.Cells[4, 11].Value = "17:00";
        worksheet.Cells[5, 10].Value = "Location:";
        worksheet.Cells[5, 11].Value = "Test Arena";
        worksheet.Cells[6, 10].Value = "Division:";
        worksheet.Cells[6, 11].Value = "Division A";

        // Add headers
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        worksheet.Cells[1, 3].Value = "Player 1.1";
        worksheet.Cells[1, 4].Value = "Player 1.2";
        worksheet.Cells[1, 7].Value = "Player 2.1";
        worksheet.Cells[1, 8].Value = "Player 2.2";

        // Add games for 2 rounds
        var games = new[]
        {
            ("Round 1", 1, "Alice", "Anderson", "Bob", "Brown"),
            ("Round 1", 2, "Charlie", "Clark", "David", "Davis"),
            ("Round 2", 1, "Alice", "Anderson", "Charlie", "Clark"),
            ("Round 2", 2, "Bob", "Brown", "David", "Davis")
        };

        for (int i = 0; i < games.Length; i++)
        {
            int row = i + 2;
            var game = games[i];
            worksheet.Cells[row, 1].Value = game.Item1;
            worksheet.Cells[row, 2].Value = game.Item2;
            worksheet.Cells[row, 3].Value = game.Item3;
            worksheet.Cells[row, 4].Value = game.Item4;
            worksheet.Cells[row, 7].Value = game.Item5;
            worksheet.Cells[row, 8].Value = game.Item6;
        }

        var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private MemoryStream CreateMultiRoundExcel(int rounds, int gamesPerRound, int courts, string? endTime = null)
    {
        var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Tournament");

        // Add metadata
        worksheet.Cells[1, 10].Value = "Date:";
        worksheet.Cells[1, 11].Value = "22.11.2025";
        worksheet.Cells[2, 10].Value = "Start Time:";
        worksheet.Cells[2, 11].Value = "09:00";
        if (endTime != null)
        {
            worksheet.Cells[3, 10].Value = "End Time:";
            worksheet.Cells[3, 11].Value = endTime;
        }

        // Add headers
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        worksheet.Cells[1, 3].Value = "Player 1.1";
        worksheet.Cells[1, 4].Value = "Player 1.2";
        worksheet.Cells[1, 7].Value = "Player 2.1";
        worksheet.Cells[1, 8].Value = "Player 2.2";

        int rowIndex = 2;
        int playerIndex = 1;

        for (int round = 1; round <= rounds; round++)
        {
            for (int game = 0; game < gamesPerRound; game++)
            {
                var court = (game % courts) + 1;
                worksheet.Cells[rowIndex, 1].Value = $"Round {round}";
                worksheet.Cells[rowIndex, 2].Value = court;
                worksheet.Cells[rowIndex, 3].Value = $"P{playerIndex}A";
                worksheet.Cells[rowIndex, 4].Value = $"P{playerIndex}B";
                worksheet.Cells[rowIndex, 7].Value = $"P{playerIndex + 1}A";
                worksheet.Cells[rowIndex, 8].Value = $"P{playerIndex + 1}B";
                rowIndex++;
                playerIndex += 2;
            }
        }

        var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private MemoryStream CreateMinimalExcel()
    {
        var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Tournament");

        // Headers only
        worksheet.Cells[1, 1].Value = "Round";
        worksheet.Cells[1, 2].Value = "Court";
        worksheet.Cells[1, 3].Value = "Player 1.1";
        worksheet.Cells[1, 4].Value = "Player 1.2";
        worksheet.Cells[1, 7].Value = "Player 2.1";
        worksheet.Cells[1, 8].Value = "Player 2.2";

        // One simple game
        worksheet.Cells[2, 1].Value = "Round 1";
        worksheet.Cells[2, 2].Value = 1;
        worksheet.Cells[2, 3].Value = "John";
        worksheet.Cells[2, 4].Value = "Doe";
        worksheet.Cells[2, 7].Value = "Jane";
        worksheet.Cells[2, 8].Value = "Smith";

        var stream = new MemoryStream();
        package.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
