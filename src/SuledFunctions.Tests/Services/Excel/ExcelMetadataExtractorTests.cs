using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SuledFunctions.Models;
using SuledFunctions.Services.Excel;

namespace SuledFunctions.Tests.Services.Excel;

public class ExcelMetadataExtractorTests
{
    private readonly Mock<ILogger<ExcelMetadataExtractor>> _loggerMock;
    private readonly ExcelMetadataExtractor _extractor;

    public ExcelMetadataExtractorTests()
    {
        _loggerMock = new Mock<ILogger<ExcelMetadataExtractor>>();
        _extractor = new ExcelMetadataExtractor(_loggerMock.Object);
    }

    [Fact]
    public void ExtractFromFileName_WithValidPattern_ExtractsAllFields()
    {
        // Arrange
        var tournament = new Tournament();
        var fileName = "SummerChampionship_22-11-2025_Chicago_DivisionA.xlsx";

        // Act
        _extractor.ExtractFromFileName(tournament, fileName);

        // Assert
        tournament.Name.Should().Be("SummerChampionship");
        tournament.StartDate.Should().Be(new DateTime(2025, 11, 22));
        tournament.Location.Should().Be("Chicago");
        tournament.Division.Should().Be("DivisionA");
    }

    [Fact]
    public void ExtractFromFileName_WithNameAndDateOnly_ExtractsPartialData()
    {
        // Arrange
        var tournament = new Tournament();
        var fileName = "SpringTournament_15-03-2025.xlsx";

        // Act
        _extractor.ExtractFromFileName(tournament, fileName);

        // Assert
        tournament.Name.Should().Be("SpringTournament");
        tournament.StartDate.Should().Be(new DateTime(2025, 3, 15));
        tournament.Location.Should().BeEmpty();
        tournament.Division.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFromFileName_WithNameOnly_DoesNotExtract()
    {
        // Arrange
        var tournament = new Tournament { Name = "DefaultName" };
        var fileName = "SimpleTournament.xlsx";

        // Act
        _extractor.ExtractFromFileName(tournament, fileName);

        // Assert
        // When there's only one part (no underscore), nothing is extracted (requires at least 2 parts)
        tournament.Name.Should().Be("DefaultName");
        tournament.StartDate.Should().BeNull();
    }

    [Fact]
    public void ExtractFromFileName_WithInvalidDate_LogsWarningAndContinues()
    {
        // Arrange
        var tournament = new Tournament();
        var fileName = "Tournament_InvalidDate_Location.xlsx";

        // Act
        _extractor.ExtractFromFileName(tournament, fileName);

        // Assert
        tournament.Name.Should().Be("Tournament");
        tournament.StartDate.Should().BeNull();
        tournament.Location.Should().Be("Location");
    }

    [Fact]
    public void ExtractFromExcel_WithAllMetadata_ExtractsAllFields()
    {
        // Arrange
        var tournament = new Tournament();
        // Metadata is in 0-based columns 9 (label) and 10 (value)
        var rows = CreateMetadataRows(
            ("Tournament Name:", "Excel Tournament"),
            ("Location:", "New York"),
            ("Date:", "22.11.2025"),
            ("Division:", "Pro"),
            ("Start Time:", "09:00"),
            ("End Time:", "17:00"),
            ("Description:", "Annual championship"),
            ("Rules:", "Standard rules apply")
        );

        // Act
        _extractor.ExtractFromExcel(tournament, rows);

        // Assert
        tournament.Name.Should().Be("Excel Tournament");
        tournament.Location.Should().Be("New York");
        tournament.StartDate.Should().Be(new DateTime(2025, 11, 22));
        tournament.Division.Should().Be("Pro");
        tournament.StartTime.Should().Be(new TimeSpan(9, 0, 0));
        tournament.EndTime.Should().Be(new TimeSpan(17, 0, 0));
        tournament.Description.Should().Be("Annual championship");
        tournament.Rules.Should().Be("Standard rules apply");
    }

    [Fact]
    public void ExtractFromExcel_WithPartialMetadata_ExtractsAvailableFields()
    {
        // Arrange
        var tournament = new Tournament { Name = "Original Name" };
        var rows = CreateMetadataRows(("Location:", "Boston"));

        // Act
        _extractor.ExtractFromExcel(tournament, rows);

        // Assert
        tournament.Location.Should().Be("Boston");
        tournament.Name.Should().Be("Original Name"); // Should not change
    }

    [Fact]
    public void ExtractFromExcel_WithEmptyWorksheet_DoesNotCrash()
    {
        // Arrange
        var tournament = new Tournament { Name = "Test" };
        var rows = Array.Empty<string[]>();

        // Act
        var action = () => _extractor.ExtractFromExcel(tournament, rows);

        // Assert
        action.Should().NotThrow();
        tournament.Name.Should().Be("Test");
    }

    [Fact]
    public void ExtractFromExcel_WithCaseVariations_RecognizesLabels()
    {
        // Arrange
        var tournament = new Tournament();
        var rows = CreateMetadataRows(
            ("LOCATION:", "Seattle"),        // Uppercase
            ("category:", "Advanced")        // Lowercase synonym for division
        );

        // Act
        _extractor.ExtractFromExcel(tournament, rows);

        // Assert
        tournament.Location.Should().Be("Seattle");
        tournament.Division.Should().Be("Advanced");
    }

    [Fact]
    public void DetermineStatus_WithFutureStartDate_SetsUpcoming()
    {
        // Arrange
        var tournament = new Tournament
        {
            StartDate = DateTime.UtcNow.AddDays(10)
        };

        // Act
        _extractor.DetermineStatus(tournament);

        // Assert
        tournament.Status.Should().Be(TournamentStatus.Upcoming);
    }

    [Fact]
    public void DetermineStatus_WithPastEndDate_SetsCompleted()
    {
        // Arrange
        var tournament = new Tournament
        {
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(-5)
        };

        // Act
        _extractor.DetermineStatus(tournament);

        // Assert
        tournament.Status.Should().Be(TournamentStatus.Completed);
    }

    [Fact]
    public void DetermineStatus_WithCurrentDate_SetsInProgress()
    {
        // Arrange
        var tournament = new Tournament
        {
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(2)
        };

        // Act
        _extractor.DetermineStatus(tournament);

        // Assert
        tournament.Status.Should().Be(TournamentStatus.InProgress);
    }

    [Fact]
    public void DetermineStatus_WithNoStartDate_SetsUpcoming()
    {
        // Arrange
        var tournament = new Tournament
        {
            StartDate = null
        };

        // Act
        _extractor.DetermineStatus(tournament);

        // Assert
        tournament.Status.Should().Be(TournamentStatus.Upcoming);
    }

    [Fact]
    public void DetermineStatus_WithStartDateButNoEndDate_UsesStartDateForComparison()
    {
        // Arrange
        var tournament = new Tournament
        {
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = null // Should default to StartDate
        };

        // Act
        _extractor.DetermineStatus(tournament);

        // Assert
        tournament.Status.Should().Be(TournamentStatus.Completed);
    }

    /// <summary>
    /// Creates a string[][] where metadata labels are in column index 9 and values in column index 10.
    /// </summary>
    private static string[][] CreateMetadataRows(params (string label, string value)[] entries)
    {
        return entries.Select(e =>
        {
            var row = new string[11];
            row[9] = e.label;
            row[10] = e.value;
            return row;
        }).ToArray();
    }
}
