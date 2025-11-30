using Microsoft.Azure.Functions.Worker;
using SuledFunctions.Models;
using SuledFunctions.TelegramBot.Functions;
using SuledFunctions.TelegramBot.Services;
using SuledFunctions.TelegramBot.Models;

namespace SuledFunctions.TelegramBot.Tests.Functions;

public class GameNotificationTimerFunctionTests
{
    private readonly Mock<ILogger<GameNotificationTimerFunction>> _loggerMock;
    private readonly Mock<ITelegramBotService> _botServiceMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;

    public GameNotificationTimerFunctionTests()
    {
        _loggerMock = new Mock<ILogger<GameNotificationTimerFunction>>();
        _botServiceMock = new Mock<ITelegramBotService>();
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
    }

    [Fact]
    public void GameNotificationTimerFunction_ShouldInitializeCorrectly()
    {
        // Act
        var function = new GameNotificationTimerFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        // Assert
        function.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_WithNoSubscriptions_ShouldReturnEarly()
    {
        // Arrange
        var function = new GameNotificationTimerFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        var tournaments = new List<Tournament>();
        var timerInfo = new TimerInfo();

        _subscriptionServiceMock
            .Setup(x => x.GetActiveSubscriptionsAsync())
            .ReturnsAsync(new List<UserSubscription>());

        // Act
        await function.Run(timerInfo, tournaments);

        // Assert
        _subscriptionServiceMock.Verify(x => x.GetActiveSubscriptionsAsync(), Times.Once);
        _botServiceMock.Verify(
            x => x.SendUpcomingGameNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WithUpcomingGame_ShouldSendNotification()
    {
        // Arrange
        var function = new GameNotificationTimerFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        var pairId = "pair-123";
        var gameId = "game-456";
        var scheduledTime = DateTime.UtcNow.AddMinutes(5);

        var targetPair = new Pair 
        { 
            Id = pairId, 
            Player1 = new Player { Name = "Team", Surname = "Alpha" },
            Player2 = new Player { Name = "Player", Surname = "A" }
        };

        var opponentPair = new Pair 
        { 
            Id = "pair-789", 
            Player1 = new Player { Name = "Team", Surname = "Beta" },
            Player2 = new Player { Name = "Player", Surname = "B" }
        };

        var tournament = new Tournament
        {
            Id = "tournament-1",
            Name = "Test Tournament",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame
                        {
                            Id = gameId,
                            TournamentId = "tournament-1",
                            Round = 1,
                            CourtNumber = 5,
                            ScheduledTime = scheduledTime,
                            Status = GameStatus.Scheduled,
                            OpponentPair = opponentPair
                        }
                    }
                }
            }
        };

        var subscription = new UserSubscription
        {
            ChatId = 123456L,
            PairId = pairId,
            PairDisplayName = "Team Alpha & Player A",
            IsActive = true,
            NotificationMinutesBefore = 5
        };

        _subscriptionServiceMock
            .Setup(x => x.GetActiveSubscriptionsAsync())
            .ReturnsAsync(new List<UserSubscription> { subscription });

        _subscriptionServiceMock
            .Setup(x => x.HasNotificationBeenSentAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var timerInfo = new TimerInfo();
        var tournaments = new List<Tournament> { tournament };

        // Act
        await function.Run(timerInfo, tournaments);

        // Assert
        _botServiceMock.Verify(
            x => x.SendUpcomingGameNotificationAsync(
                123456L,
                "Team Alpha & Player A",
                1,
                5,
                "Team Beta & Player B",
                scheduledTime),
            Times.Once);

        _subscriptionServiceMock.Verify(
            x => x.MarkNotificationSentAsync(123456L, gameId, "UpcomingGame"),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithAlreadySentNotification_ShouldNotSendAgain()
    {
        // Arrange
        var function = new GameNotificationTimerFunction(
            _loggerMock.Object,
            _botServiceMock.Object,
            _subscriptionServiceMock.Object);

        var pairId = "pair-123";
        var gameId = "game-456";
        var scheduledTime = DateTime.UtcNow.AddMinutes(5);

        var targetPair = new Pair 
        { 
            Id = pairId, 
            Player1 = new Player { Name = "Team", Surname = "Alpha" },
            Player2 = new Player { Name = "Player", Surname = "A" }
        };

        var opponentPair = new Pair 
        { 
            Id = "pair-789", 
            Player1 = new Player { Name = "Team", Surname = "Beta" },
            Player2 = new Player { Name = "Player", Surname = "B" }
        };

        var tournament = new Tournament
        {
            Id = "tournament-1",
            Name = "Test Tournament",
            Pairs = new List<TournamentPair>
            {
                new TournamentPair
                {
                    PairInfo = targetPair,
                    Games = new List<PairGame>
                    {
                        new PairGame
                        {
                            Id = gameId,
                            TournamentId = "tournament-1",
                            Round = 1,
                            CourtNumber = 5,
                            ScheduledTime = scheduledTime,
                            Status = GameStatus.Scheduled,
                            OpponentPair = opponentPair
                        }
                    }
                }
            }
        };

        var subscription = new UserSubscription
        {
            ChatId = 123456L,
            PairId = pairId,
            PairDisplayName = "Team Alpha & Player A",
            IsActive = true,
            NotificationMinutesBefore = 5
        };

        _subscriptionServiceMock
            .Setup(x => x.GetActiveSubscriptionsAsync())
            .ReturnsAsync(new List<UserSubscription> { subscription });

        _subscriptionServiceMock
            .Setup(x => x.HasNotificationBeenSentAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(true); // Already sent

        var timerInfo = new TimerInfo();
        var tournaments = new List<Tournament> { tournament };

        // Act
        await function.Run(timerInfo, tournaments);

        // Assert
        _botServiceMock.Verify(
            x => x.SendUpcomingGameNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);

        _subscriptionServiceMock.Verify(
            x => x.MarkNotificationSentAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void GameNotificationTimerFunction_ShouldHaveFunctionAttribute()
    {
        // Arrange
        var method = typeof(GameNotificationTimerFunction).GetMethod("Run");

        // Assert
        method.Should().NotBeNull();
        var functionAttribute = method!.GetCustomAttributes(typeof(FunctionAttribute), false)
            .FirstOrDefault() as FunctionAttribute;
        
        functionAttribute.Should().NotBeNull();
        functionAttribute!.Name.Should().Be("GameNotificationTimer");
    }
}

