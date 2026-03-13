using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuledFunctions.TelegramBot.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Requests;

namespace SuledFunctions.TelegramBot.Services;

/// <summary>
/// Service for handling Telegram bot operations
/// </summary>
public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPairService _pairService;

    public TelegramBotService(
        IOptions<TelegramBotSettings> settings,
        ILogger<TelegramBotService> logger,
        ISubscriptionService subscriptionService,
        IPairService pairService)
    {
        _logger = logger;
        _subscriptionService = subscriptionService;
        _pairService = pairService;
        _botClient = new TelegramBotClient(settings.Value.BotToken);
    }

    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await HandleMessageAsync(update.Message);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram update");
        }
    }

    private async Task HandleMessageAsync(Message message)
    {
        var chatId = message.Chat.Id;
        var text = message.Text!;

        _logger.LogInformation("Received message from {ChatId}: {Text}", chatId, text);

        switch (text)
        {
            case "/start":
                await SendWelcomeMessageAsync(chatId);
                break;

            case "/selectpair":
                await SendPairSelectionAsync(chatId);
                break;

            case "/mypair":
                await SendCurrentSubscriptionAsync(chatId);
                break;

            case "/unsubscribe":
                await UnsubscribeAsync(chatId);
                break;

            case "/help":
                await SendHelpMessageAsync(chatId);
                break;

            default:
                await _botClient.SendMessage(
                    chatId,
                    "Unknown command. Use /help to see available commands.");
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data!;

        _logger.LogInformation("Received callback from {ChatId}: {Data}", chatId, data);

        if (data.StartsWith("pair_"))
        {
            var pairId = data.Substring(5);
            await SubscribeToPairAsync(chatId, pairId);
        }

        // Answer callback query to remove loading state
        await _botClient.AnswerCallbackQuery(callbackQuery.Id);
    }

    private async Task SendWelcomeMessageAsync(long chatId)
    {
        var message = "🎾 Welcome to Suled Tournament Bot!\n\n" +
                     "I'll help you track your games and notify you before each match.\n\n" +
                     "Use /selectpair to choose your pair and start receiving notifications.\n" +
                     "Use /help to see all available commands.";

        await _botClient.SendMessage(chatId, message);
    }

    private async Task SendPairSelectionAsync(long chatId)
    {
        var pairs = await _pairService.GetAllPairsAsync();

        if (!pairs.Any())
        {
            await _botClient.SendMessage(
                chatId,
                "No pairs available yet. Please wait for a tournament to be uploaded.");
            return;
        }

        var keyboard = new InlineKeyboardMarkup(
            pairs.Select(p => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{p.DisplayName} ({p.Player1} & {p.Player2})",
                    $"pair_{p.Id}")
            }));

        await _botClient.SendMessage(
            chatId,
            "🎾 Select your pair:",
            replyMarkup: keyboard);
    }

    private async Task SubscribeToPairAsync(long chatId, string pairId)
    {
        var pair = await _pairService.GetPairByIdAsync(pairId);

        if (pair == null)
        {
            await _botClient.SendMessage(chatId, "Pair not found.");
            return;
        }

        await _subscriptionService.SubscribeAsync(chatId, pairId, pair.DisplayName);

        await _botClient.SendMessage(
            chatId,
            $"✅ You're now subscribed to:\n{pair.DisplayName}\n\n" +
            $"You'll receive notifications 5 minutes before each game.");
    }

    private async Task SendCurrentSubscriptionAsync(long chatId)
    {
        var subscription = await _subscriptionService.GetSubscriptionAsync(chatId);

        if (subscription == null || !subscription.IsActive)
        {
            await _botClient.SendMessage(
                chatId,
                "You're not subscribed to any pair. Use /selectpair to subscribe.");
            return;
        }

        await _botClient.SendMessage(
            chatId,
            $"🎾 Your current pair:\n{subscription.PairDisplayName}\n\n" +
            $"Notification time: {subscription.NotificationMinutesBefore} minutes before game");
    }

    private async Task UnsubscribeAsync(long chatId)
    {
        await _subscriptionService.UnsubscribeAsync(chatId);

        await _botClient.SendMessage(
            chatId,
            "✅ You've been unsubscribed. Use /selectpair to subscribe again.");
    }

    private async Task SendHelpMessageAsync(long chatId)
    {
        var message = "🎾 Suled Bot Commands:\n\n" +
                     "/start - Start using the bot\n" +
                     "/selectpair - Choose your pair\n" +
                     "/mypair - Show current subscription\n" +
                     "/unsubscribe - Stop notifications\n" +
                     "/help - Show this help message";

        await _botClient.SendMessage(chatId, message);
    }

    public async Task SendNewTournamentNotificationAsync(string tournamentName, List<long> chatIds)
    {
        var message = $"🆕 New Tournament!\n\n" +
                     $"Tournament: {tournamentName}\n\n" +
                     $"Use /selectpair to choose your pair and receive game notifications.";

        foreach (var chatId in chatIds)
        {
            try
            {
                await _botClient.SendMessage(chatId, message);
                _logger.LogInformation("Sent new tournament notification to {ChatId}", chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to {ChatId}", chatId);
            }
        }
    }

    public async Task SendUpcomingGameNotificationAsync(
        long chatId,
        string pairName,
        int round,
        int courtNumber,
        string opponent,
        DateTime gameTime)
    {
        var message = $"⏰ Game Starting Soon!\n\n" +
                     $"🎾 Your Pair: {pairName}\n" +
                     $"🔄 Round: {round}\n" +
                     $"🏟️ Court: {courtNumber}\n" +
                     $"👥 Opponent: {opponent}\n" +
                     $"⏱️ Time: {gameTime:HH:mm}\n\n" +
                     $"Good luck! 🍀";

        try
        {
            await _botClient.SendMessage(chatId, message);
            _logger.LogInformation("Sent game notification to {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send game notification to {ChatId}", chatId);
        }
    }
}
