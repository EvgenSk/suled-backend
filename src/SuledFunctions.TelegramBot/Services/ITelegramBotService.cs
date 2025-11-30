using Telegram.Bot.Types;

namespace SuledFunctions.TelegramBot.Services;

/// <summary>
/// Interface for Telegram bot operations
/// </summary>
public interface ITelegramBotService
{
    Task HandleUpdateAsync(Update update);
    Task SendNewTournamentNotificationAsync(string tournamentName, List<long> chatIds);
    Task SendUpcomingGameNotificationAsync(long chatId, string pairName, int round, int courtNumber, string opponent, DateTime gameTime);
}
