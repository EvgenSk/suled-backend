using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.TelegramBot.Models;
using SuledFunctions.TelegramBot.Services;

namespace SuledFunctions.TelegramBot.Functions;

/// <summary>
/// Timer function to check for upcoming games and send notifications
/// Runs every minute to check for games starting in 5 minutes
/// </summary>
public class GameNotificationTimerFunction
{
    private readonly ILogger<GameNotificationTimerFunction> _logger;
    private readonly ITelegramBotService _botService;
    private readonly ISubscriptionService _subscriptionService;

    public GameNotificationTimerFunction(
        ILogger<GameNotificationTimerFunction> logger,
        ITelegramBotService botService,
        ISubscriptionService subscriptionService)
    {
        _logger = logger;
        _botService = botService;
        _subscriptionService = subscriptionService;
    }

    [Function("GameNotificationTimer")]
    public async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo, // Every minute
        [CosmosDBInput(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            SqlQuery = "SELECT * FROM c WHERE c.Games != null")]
        IEnumerable<Tournament> tournaments)
    {
        _logger.LogInformation("Checking for upcoming games to notify users");

        try
        {
            var now = DateTime.UtcNow;
            var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();

            if (!subscriptions.Any())
            {
                _logger.LogInformation("No active subscriptions");
                return;
            }

            foreach (var subscription in subscriptions)
            {
                await ProcessSubscriptionNotificationsAsync(
                    subscription,
                    tournaments,
                    now);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing game notifications");
        }
    }

    private async Task ProcessSubscriptionNotificationsAsync(
        UserSubscription subscription,
        IEnumerable<Tournament> tournaments,
        DateTime now)
    {
        var upcomingGames = tournaments
            .Where(t => t.Games != null)
            .SelectMany(t => t.Games!)
            .Where(g => 
                (g.Pair1?.Id == subscription.PairId || g.Pair2?.Id == subscription.PairId) &&
                g.ScheduledTime.HasValue &&
                g.Status == GameStatus.Scheduled)
            .Where(g =>
            {
                var timeUntilGame = g.ScheduledTime!.Value - now;
                return timeUntilGame.TotalMinutes > 0 && 
                       timeUntilGame.TotalMinutes <= subscription.NotificationMinutesBefore + 1; // +1 minute buffer
            })
            .ToList();

        foreach (var game in upcomingGames)
        {
            // Check if notification already sent
            if (await _subscriptionService.HasNotificationBeenSentAsync(subscription.ChatId, game.Id))
            {
                continue;
            }

            var (userPair, opponent) = game.Pair1?.Id == subscription.PairId 
                ? (game.Pair1, game.Pair2?.DisplayName ?? "Unknown")
                : (game.Pair2, game.Pair1?.DisplayName ?? "Unknown");

            await _botService.SendUpcomingGameNotificationAsync(
                subscription.ChatId,
                userPair?.DisplayName ?? subscription.PairDisplayName,
                game.Round,
                game.CourtNumber,
                opponent,
                game.ScheduledTime!.Value);

            // Mark as sent
            await _subscriptionService.MarkNotificationSentAsync(
                subscription.ChatId,
                game.Id,
                "UpcomingGame");

            _logger.LogInformation(
                "Sent game notification to {ChatId} for game {GameId}",
                subscription.ChatId,
                game.Id);
        }
    }
}
