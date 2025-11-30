using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.TelegramBot.Services;

namespace SuledFunctions.TelegramBot.Functions;

/// <summary>
/// Cosmos DB Change Feed function to notify users of new tournaments
/// </summary>
public class NewTournamentNotificationFunction
{
    private readonly ILogger<NewTournamentNotificationFunction> _logger;
    private readonly ITelegramBotService _botService;
    private readonly ISubscriptionService _subscriptionService;

    public NewTournamentNotificationFunction(
        ILogger<NewTournamentNotificationFunction> logger,
        ITelegramBotService botService,
        ISubscriptionService subscriptionService)
    {
        _logger = logger;
        _botService = botService;
        _subscriptionService = subscriptionService;
    }

    [Function("NewTournamentNotification")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "%CosmosDbName%",
            containerName: "%CosmosContainerName%",
            Connection = "CosmosDbConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<Tournament> tournaments)
    {
        _logger.LogInformation("Processing {Count} new/updated tournaments", tournaments.Count);

        try
        {
            // Get all active subscriptions
            var subscriptions = await _subscriptionService.GetActiveSubscriptionsAsync();
            var chatIds = subscriptions.Select(s => s.ChatId).Distinct().ToList();

            if (!chatIds.Any())
            {
                _logger.LogInformation("No active subscriptions to notify");
                return;
            }

            foreach (var tournament in tournaments)
            {
                _logger.LogInformation("Notifying users about new tournament: {TournamentName}", tournament.Name);
                await _botService.SendNewTournamentNotificationAsync(tournament.Name, chatIds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending new tournament notifications");
        }
    }
}
