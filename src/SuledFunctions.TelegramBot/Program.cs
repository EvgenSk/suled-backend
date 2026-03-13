using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuledFunctions.TelegramBot.Configuration;
using SuledFunctions.TelegramBot.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Bind settings from configuration (environment variables / appsettings)
        services.Configure<TelegramBotSettings>(config =>
        {
            config.BotToken = context.Configuration["TelegramBotToken"]
                ?? throw new InvalidOperationException("TelegramBotToken not configured");
        });

        services.Configure<TelegramBotCosmosDbSettings>(config =>
        {
            config.DatabaseName = context.Configuration["CosmosDbName"] ?? "TournamentDb";
            config.TournamentsContainerName = context.Configuration["CosmosContainerName"] ?? "Tournaments";
            config.SubscriptionsContainerName = context.Configuration["CosmosSubscriptionsContainerName"] ?? "Subscriptions";
            config.NotificationsContainerName = context.Configuration["CosmosNotificationsContainerName"] ?? "Notifications";
        });

        // Register Cosmos Client
        var cosmosConnectionString = context.Configuration["CosmosDbConnection"]
            ?? throw new InvalidOperationException("CosmosDbConnection not configured");
        services.AddSingleton(_ => new CosmosClient(cosmosConnectionString));

        // Register services
        services.AddSingleton<IPairService, PairService>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        services.AddSingleton<ITelegramBotService, TelegramBotService>();
    })
    .Build();

host.Run();
