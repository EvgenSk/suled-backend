using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SuledFunctions.TelegramBot.Models;

namespace SuledFunctions.TelegramBot.Services;

/// <summary>
/// Service for managing user subscriptions in Cosmos DB
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly Container _subscriptionsContainer;
    private readonly Container _notificationsContainer;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        CosmosClient cosmosClient,
        ILogger<SubscriptionService> logger,
        string databaseName,
        string subscriptionsContainerName,
        string notificationsContainerName)
    {
        _logger = logger;
        _subscriptionsContainer = cosmosClient.GetContainer(databaseName, subscriptionsContainerName);
        _notificationsContainer = cosmosClient.GetContainer(databaseName, notificationsContainerName);
    }

    public async Task<UserSubscription?> GetSubscriptionAsync(long chatId)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.ChatId = @chatId AND c.IsActive = true")
                .WithParameter("@chatId", chatId);

            var iterator = _subscriptionsContainer.GetItemQueryIterator<UserSubscription>(query);
            var results = await iterator.ReadNextAsync();

            return results.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<UserSubscription>> GetActiveSubscriptionsAsync()
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.IsActive = true");
        var iterator = _subscriptionsContainer.GetItemQueryIterator<UserSubscription>(query);
        
        var subscriptions = new List<UserSubscription>();
        while (iterator.HasMoreResults)
        {
            var results = await iterator.ReadNextAsync();
            subscriptions.AddRange(results);
        }

        return subscriptions;
    }

    public async Task<List<UserSubscription>> GetSubscriptionsForPairAsync(string pairId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.PairId = @pairId AND c.IsActive = true")
            .WithParameter("@pairId", pairId);

        var iterator = _subscriptionsContainer.GetItemQueryIterator<UserSubscription>(query);
        
        var subscriptions = new List<UserSubscription>();
        while (iterator.HasMoreResults)
        {
            var results = await iterator.ReadNextAsync();
            subscriptions.AddRange(results);
        }

        return subscriptions;
    }

    public async Task SubscribeAsync(long chatId, string pairId, string pairDisplayName)
    {
        // Deactivate existing subscription
        var existing = await GetSubscriptionAsync(chatId);
        if (existing != null)
        {
            existing = existing with { IsActive = false };
            await _subscriptionsContainer.UpsertItemAsync(existing, new PartitionKey(existing.Id));
        }

        // Create new subscription
        var subscription = new UserSubscription
        {
            ChatId = chatId,
            PairId = pairId,
            PairDisplayName = pairDisplayName,
            IsActive = true
        };

        await _subscriptionsContainer.CreateItemAsync(subscription, new PartitionKey(subscription.Id));
        _logger.LogInformation("User {ChatId} subscribed to pair {PairId}", chatId, pairId);
    }

    public async Task UnsubscribeAsync(long chatId)
    {
        var subscription = await GetSubscriptionAsync(chatId);
        if (subscription != null)
        {
            subscription = subscription with { IsActive = false };
            await _subscriptionsContainer.UpsertItemAsync(subscription, new PartitionKey(subscription.Id));
            _logger.LogInformation("User {ChatId} unsubscribed", chatId);
        }
    }

    public async Task<bool> HasNotificationBeenSentAsync(long chatId, string gameId)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.ChatId = @chatId AND c.GameId = @gameId")
                .WithParameter("@chatId", chatId)
                .WithParameter("@gameId", gameId);

            var iterator = _notificationsContainer.GetItemQueryIterator<SentNotification>(query);
            var results = await iterator.ReadNextAsync();

            return results.Any();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task MarkNotificationSentAsync(long chatId, string gameId, string notificationType)
    {
        var notification = new SentNotification
        {
            ChatId = chatId,
            GameId = gameId,
            NotificationType = notificationType
        };

        await _notificationsContainer.CreateItemAsync(notification, new PartitionKey(notification.Id));
    }
}
