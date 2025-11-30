using SuledFunctions.TelegramBot.Models;

namespace SuledFunctions.TelegramBot.Services;

/// <summary>
/// Interface for managing user subscriptions
/// </summary>
public interface ISubscriptionService
{
    Task<UserSubscription?> GetSubscriptionAsync(long chatId);
    Task<List<UserSubscription>> GetActiveSubscriptionsAsync();
    Task<List<UserSubscription>> GetSubscriptionsForPairAsync(string pairId);
    Task SubscribeAsync(long chatId, string pairId, string pairDisplayName);
    Task UnsubscribeAsync(long chatId);
    Task<bool> HasNotificationBeenSentAsync(long chatId, string gameId);
    Task MarkNotificationSentAsync(long chatId, string gameId, string notificationType);
}
