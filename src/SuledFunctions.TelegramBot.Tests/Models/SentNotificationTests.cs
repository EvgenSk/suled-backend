using SuledFunctions.TelegramBot.Models;

namespace SuledFunctions.TelegramBot.Tests.Models;

public class SentNotificationTests
{
    [Fact]
    public void SentNotification_ShouldInitializeWithDefaultValues()
    {
        // Act
        var notification = new SentNotification();

        // Assert
        notification.Id.Should().NotBeNullOrEmpty();
        notification.ChatId.Should().Be(0);
        notification.GameId.Should().BeEmpty();
        notification.NotificationType.Should().BeEmpty();
        notification.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SentNotification_ShouldAllowCustomValues()
    {
        // Arrange
        var chatId = 987654321L;
        var gameId = "game-456";
        var notificationType = "UpcomingGame";

        // Act
        var notification = new SentNotification
        {
            ChatId = chatId,
            GameId = gameId,
            NotificationType = notificationType
        };

        // Assert
        notification.ChatId.Should().Be(chatId);
        notification.GameId.Should().Be(gameId);
        notification.NotificationType.Should().Be(notificationType);
    }

    [Theory]
    [InlineData("NewTournament")]
    [InlineData("UpcomingGame")]
    [InlineData("GameResult")]
    public void SentNotification_ShouldSupportDifferentNotificationTypes(string notificationType)
    {
        // Act
        var notification = new SentNotification
        {
            NotificationType = notificationType
        };

        // Assert
        notification.NotificationType.Should().Be(notificationType);
    }

    [Fact]
    public void SentNotification_RecordType_ShouldSupportWithSyntax()
    {
        // Arrange
        var original = new SentNotification
        {
            ChatId = 123456789L,
            GameId = "game-123",
            NotificationType = "UpcomingGame"
        };

        // Act
        var modified = original with { NotificationType = "GameResult" };

        // Assert
        modified.ChatId.Should().Be(original.ChatId);
        modified.GameId.Should().Be(original.GameId);
        modified.NotificationType.Should().Be("GameResult");
        original.NotificationType.Should().Be("UpcomingGame"); // Original unchanged
    }
}
