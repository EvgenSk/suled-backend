using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuledFunctions.TelegramBot.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Get configuration
        var botToken = Environment.GetEnvironmentVariable("TelegramBotToken")
            ?? throw new InvalidOperationException("TelegramBotToken not configured");
        var cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnection")
            ?? throw new InvalidOperationException("CosmosDbConnection not configured");
        var databaseName = Environment.GetEnvironmentVariable("CosmosDbName") ?? "TournamentDb";
        var tournamentsContainer = Environment.GetEnvironmentVariable("CosmosContainerName") ?? "Tournaments";
        var subscriptionsContainer = "Subscriptions";
        var notificationsContainer = "Notifications";

        // Register Cosmos Client
        services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));

        // Register services
        services.AddSingleton<IPairService>(sp =>
            new PairService(
                sp.GetRequiredService<CosmosClient>(),
                sp.GetRequiredService<ILogger<PairService>>(),
                databaseName,
                tournamentsContainer));

        services.AddSingleton<ISubscriptionService>(sp =>
            new SubscriptionService(
                sp.GetRequiredService<CosmosClient>(),
                sp.GetRequiredService<ILogger<SubscriptionService>>(),
                databaseName,
                subscriptionsContainer,
                notificationsContainer));

        services.AddSingleton<ITelegramBotService>(sp =>
            new TelegramBotService(
                sp.GetRequiredService<ILogger<TelegramBotService>>(),
                sp.GetRequiredService<ISubscriptionService>(),
                sp.GetRequiredService<IPairService>(),
                botToken));
    })
    .Build();

host.Run();
