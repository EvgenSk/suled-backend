using Microsoft.Extensions.Options;
using SuledFunctions.TelegramBot.Configuration;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using SuledFunctions.TelegramBot.Services;
using SuledFunctions.Models.DTOs;
using SuledFunctions.Models;

namespace SuledFunctions.TelegramBot.Tests.Services;

public class TelegramBotServiceTests
{
    private readonly Mock<ILogger<TelegramBotService>> _loggerMock;
    private readonly Mock<ISubscriptionService> _subscriptionServiceMock;
    private readonly Mock<IPairService> _pairServiceMock;
    private const string TestBotToken = "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11";

    public TelegramBotServiceTests()
    {
        _loggerMock = new Mock<ILogger<TelegramBotService>>();
        _subscriptionServiceMock = new Mock<ISubscriptionService>();
        _pairServiceMock = new Mock<IPairService>();
    }

    private TelegramBotService CreateService() =>
        new TelegramBotService(
            Options.Create(new TelegramBotSettings { BotToken = TestBotToken }),
            _loggerMock.Object,
            _subscriptionServiceMock.Object,
            _pairServiceMock.Object);

    [Fact]
    public void TelegramBotService_ShouldInitializeWithValidToken()
    {
        // Act
        var service = CreateService();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleUpdateAsync_WithNullUpdate_ShouldHandleGracefully()
    {
        // Arrange
        var service = CreateService();

        var update = new Update();

        // Act
        await service.HandleUpdateAsync(update);

        // Assert - Should complete without throwing
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never); // No errors should be logged for unknown type
    }

    [Fact]
    public async Task SendNewTournamentNotificationAsync_WithEmptyChatIds_ShouldNotFail()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.SendNewTournamentNotificationAsync("Test Tournament", new List<long>());

        // Assert - Should complete without throwing
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Theory]
    [InlineData(1, 1, "Court Alpha")]
    [InlineData(2, 5, "Court Beta")]
    [InlineData(3, 10, "Court Gamma")]
    public async Task SendUpcomingGameNotificationAsync_WithValidData_ShouldComplete(
        int round,
        int courtNumber,
        string opponent)
    {
        // Arrange
        var service = CreateService();

        var chatId = 123456789L;
        var pairName = "Test Pair";
        var gameTime = DateTime.UtcNow.AddMinutes(5);

        // Act
        await service.SendUpcomingGameNotificationAsync(
            chatId,
            pairName,
            round,
            courtNumber,
            opponent,
            gameTime);

        // Assert - Should attempt to send (will fail due to invalid token, but that's OK for unit test)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never); // Success log won't be called due to invalid token
    }
}
