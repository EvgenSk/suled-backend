using SuledFunctions.TelegramBot.Models;
using SuledFunctions.TelegramBot.Services;

namespace SuledFunctions.TelegramBot.Tests.Services;

public class SubscriptionServiceTests
{
    private readonly Mock<ILogger<SubscriptionService>> _loggerMock;

    public SubscriptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<SubscriptionService>>();
    }

    [Fact]
    public void UserSubscription_ShouldInitializeWithDefaultValues()
    {
        // Act
        var subscription = new UserSubscription();

        // Assert
        subscription.Id.Should().NotBeNullOrEmpty();
        subscription.ChatId.Should().Be(0);
        subscription.PairId.Should().BeEmpty();
        subscription.PairDisplayName.Should().BeEmpty();
        subscription.IsActive.Should().BeTrue();
        subscription.NotificationMinutesBefore.Should().Be(5);
        subscription.SubscribedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UserSubscription_ShouldAllowCustomValues()
    {
        // Arrange
        var chatId = 123456789L;
        var pairId = "pair-123";
        var displayName = "Player1 & Player2";

        // Act
        var subscription = new UserSubscription
        {
            ChatId = chatId,
            PairId = pairId,
            PairDisplayName = displayName,
            NotificationMinutesBefore = 10
        };

        // Assert
        subscription.ChatId.Should().Be(chatId);
        subscription.PairId.Should().Be(pairId);
        subscription.PairDisplayName.Should().Be(displayName);
        subscription.NotificationMinutesBefore.Should().Be(10);
    }

    [Fact]
    public void UserSubscription_RecordType_ShouldSupportWithSyntax()
    {
        // Arrange
        var original = new UserSubscription
        {
            ChatId = 123456789L,
            PairId = "pair-123",
            IsActive = true
        };

        // Act
        var modified = original with { IsActive = false };

        // Assert
        modified.ChatId.Should().Be(original.ChatId);
        modified.PairId.Should().Be(original.PairId);
        modified.IsActive.Should().BeFalse();
        original.IsActive.Should().BeTrue(); // Original unchanged
    }
}
