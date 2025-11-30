using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SuledFunctions.TelegramBot.Functions;
using SuledFunctions.TelegramBot.Services;
using System.Net;

namespace SuledFunctions.TelegramBot.Tests.Functions;

public class TelegramWebhookFunctionTests
{
    private readonly Mock<ILogger<TelegramWebhookFunction>> _loggerMock;
    private readonly Mock<ITelegramBotService> _botServiceMock;

    public TelegramWebhookFunctionTests()
    {
        _loggerMock = new Mock<ILogger<TelegramWebhookFunction>>();
        _botServiceMock = new Mock<ITelegramBotService>();
    }

    [Fact]
    public void TelegramWebhookFunction_ShouldInitializeCorrectly()
    {
        // Act
        var function = new TelegramWebhookFunction(
            _loggerMock.Object,
            _botServiceMock.Object);

        // Assert
        function.Should().NotBeNull();
    }

    [Fact]
    public void TelegramWebhookFunction_ShouldHaveFunctionAttribute()
    {
        // Arrange
        var method = typeof(TelegramWebhookFunction).GetMethod("Run");

        // Assert
        method.Should().NotBeNull();
        var functionAttribute = method!.GetCustomAttributes(typeof(FunctionAttribute), false)
            .FirstOrDefault() as FunctionAttribute;
        
        functionAttribute.Should().NotBeNull();
        functionAttribute!.Name.Should().Be("TelegramWebhook");
    }
}
