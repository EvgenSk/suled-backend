using Microsoft.Azure.Functions.Worker;
using SuledFunctions.Models;
using SuledFunctions.TelegramBot.Functions;
using SuledFunctions.TelegramBot.Services;
using SuledFunctions.TelegramBot.Models;

namespace SuledFunctions.TelegramBot.Tests.Functions;

public class NewTournamentNotificationFunctionTests
{
    private readonly Mock<ILogger<NewTournamentNotificationFunction>> _loggerMock;
    private readonly Mock<ITelegramBotService> _botServiceMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;

    public NewTournamentNotificationFunctionTests()
    {
        _loggerMock = new Mock<ILogger<NewTournamentNotificationFunction>>();
        _botServiceMock = new Mock<ITelegramBotService>();
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
    }

    [Fact]
    public void NewTournamentNotificationFunction_ShouldInitializeCorrectly()
    {
        // Act
        var function = new NewTournamentNotificationFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        // Assert
        function.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_WithNoSubscriptions_ShouldLogAndReturn()
    {
        // Arrange
        var function = new NewTournamentNotificationFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        var tournaments = new List<Tournament>
        {
            new Tournament { Id = "1", Name = "Test Tournament" }
        };

        _subscriptionServiceMock
            .Setup(x => x.GetActiveSubscriptionsAsync())
            .ReturnsAsync(new List<UserSubscription>());

        // Act
        await function.Run(tournaments);

        // Assert
        _subscriptionServiceMock.Verify(x => x.GetActiveSubscriptionsAsync(), Times.Once);
        _botServiceMock.Verify(
            x => x.SendNewTournamentNotificationAsync(It.IsAny<string>(), It.IsAny<List<long>>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WithActiveSubscriptions_ShouldSendNotifications()
    {
        // Arrange
        var function = new NewTournamentNotificationFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        var tournaments = new List<Tournament>
        {
            new Tournament { Id = "1", Name = "Test Tournament 1" },
            new Tournament { Id = "2", Name = "Test Tournament 2" }
        };

        var subscriptions = new List<UserSubscription>
        {
            new UserSubscription { ChatId = 123456L, IsActive = true },
            new UserSubscription { ChatId = 789012L, IsActive = true }
        };

        _subscriptionServiceMock
            .Setup(x => x.GetActiveSubscriptionsAsync())
            .ReturnsAsync(subscriptions);

        // Act
        await function.Run(tournaments);

        // Assert
        _botServiceMock.Verify(
            x => x.SendNewTournamentNotificationAsync(
                "Test Tournament 1",
                It.Is<List<long>>(list => list.Count == 2 && list.Contains(123456L) && list.Contains(789012L))),
            Times.Once);

        _botServiceMock.Verify(
            x => x.SendNewTournamentNotificationAsync(
                "Test Tournament 2",
                It.Is<List<long>>(list => list.Count == 2)),
            Times.Once);
    }

    [Fact]
    public void NewTournamentNotificationFunction_ShouldHaveFunctionAttribute()
    {
        // Arrange
        var method = typeof(NewTournamentNotificationFunction).GetMethod("Run");

        // Assert
        method.Should().NotBeNull();
        var functionAttribute = method!.GetCustomAttributes(typeof(FunctionAttribute), false)
            .FirstOrDefault() as FunctionAttribute;
        
        functionAttribute.Should().NotBeNull();
        functionAttribute!.Name.Should().Be("NewTournamentNotification");
    }
}
