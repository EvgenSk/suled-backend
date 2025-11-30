using SuledFunctions.TelegramBot.Services;
using SuledFunctions.Models.DTOs;

namespace SuledFunctions.TelegramBot.Tests.Services;

public class PairServiceTests
{
    private readonly Mock<ILogger<IPairService>> _loggerMock;

    public PairServiceTests()
    {
        _loggerMock = new Mock<ILogger<IPairService>>();
    }

    [Fact]
    public void PairDto_ShouldContainRequiredProperties()
    {
        // Arrange & Act
        var pairDto = new PairDto
        {
            Id = "pair-123",
            DisplayName = "Team Alpha",
            Player1 = "John Doe",
            Player2 = "Jane Smith"
        };

        // Assert
        pairDto.Id.Should().Be("pair-123");
        pairDto.DisplayName.Should().Be("Team Alpha");
        pairDto.Player1.Should().Be("John Doe");
        pairDto.Player2.Should().Be("Jane Smith");
    }

    [Theory]
    [InlineData("pair-1", "Alpha Team", "Player A", "Player B")]
    [InlineData("pair-2", "Beta Team", "Player C", "Player D")]
    [InlineData("pair-3", "Gamma Team", "Player E", "Player F")]
    public void PairDto_ShouldSupportMultiplePairs(string id, string displayName, string player1, string player2)
    {
        // Act
        var pairDto = new PairDto
        {
            Id = id,
            DisplayName = displayName,
            Player1 = player1,
            Player2 = player2
        };

        // Assert
        pairDto.Id.Should().Be(id);
        pairDto.DisplayName.Should().Be(displayName);
        pairDto.Player1.Should().Be(player1);
        pairDto.Player2.Should().Be(player2);
    }
}
