# Telegram Bot Setup Guide

This guide explains how to set up and deploy the Suled Telegram bot.

## Prerequisites

- Azure subscription
- Telegram account
- Azure CLI installed
- .NET 8.0 SDK installed
- Existing Suled backend deployment (Cosmos DB, Functions)

## Step 1: Create Telegram Bot

1. Open Telegram and search for [@BotFather](https://t.me/botfather)
2. Send `/newbot` command
3. Follow the prompts:
   - Choose a name (e.g., "Suled Tournament Bot")
   - Choose a username (e.g., "suled_tournament_bot")
4. Save the bot token (looks like `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`)
5. **Important**: Keep this token secure - treat it like a password!

## Step 2: Add Project to Solution

```powershell
# Navigate to solution directory
cd D:\Projects\Software\Suled\suled-backend

# Add the Telegram Bot project to solution
dotnet sln add src/SuledFunctions.TelegramBot/SuledFunctions.TelegramBot.csproj
```

## Step 3: Configure Local Settings

```powershell
# Navigate to bot project
cd src/SuledFunctions.TelegramBot

# Copy example settings
cp local.settings.json.example local.settings.json

# Edit local.settings.json and set:
# - TelegramBotToken: Your bot token from BotFather
# - CosmosDbConnection: Your Cosmos DB connection string
```

Example `local.settings.json`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "TelegramBotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
    "CosmosDbConnection": "AccountEndpoint=https://...;AccountKey=...",
    "CosmosDbName": "TournamentDb",
    "CosmosContainerName": "Tournaments"
  }
}
```

## Step 4: Test Locally (Optional)

```powershell
# Build the project
dotnet build

# Run locally
func start
```

Note: Local testing requires Azure Storage Emulator or Azurite for timer triggers.

## Step 5: Deploy Azure Infrastructure

```powershell
# Navigate to infrastructure directory
cd ../../infra

# Login to Azure
az login

# Set variables
$resourceGroup = "suled-rg"
$botToken = "YOUR_BOT_TOKEN_HERE"  # From BotFather
$cosmosAccountName = "suled-cosmos"  # Your existing Cosmos DB account

# Deploy bot infrastructure
az deployment group create `
  --resource-group $resourceGroup `
  --template-file telegram-bot.bicep `
  --parameters `
    appName=suled `
    telegramBotToken=$botToken `
    cosmosDbAccountName=$cosmosAccountName
```

This will create:
- Storage account for the bot function
- App Service Plan (Consumption)
- Function App
- Two Cosmos DB containers (Subscriptions, Notifications)
- Application Insights for monitoring

## Step 6: Build and Deploy Function App

```powershell
# Navigate to bot project
cd ../src/SuledFunctions.TelegramBot

# Build in Release mode
dotnet build --configuration Release

# Publish the project
dotnet publish --configuration Release --output ./publish

# Deploy to Azure using Azure Functions Core Tools
cd publish
func azure functionapp publish suled-bot-func
```

Alternative deployment using VS Code:
1. Install "Azure Functions" extension
2. Right-click on the project
3. Select "Deploy to Function App"
4. Choose your subscription and function app

## Step 7: Set Up Telegram Webhook

After deployment, configure Telegram to send updates to your Azure Function:

```powershell
# Get the Function URL with key
# Go to Azure Portal > Function App > Functions > TelegramWebhook > Get Function URL
# Or use CLI:
$functionKey = az functionapp keys list `
  --name suled-bot-func `
  --resource-group suled-rg `
  --query "functionKeys.default" `
  --output tsv

$webhookUrl = "https://suled-bot-func.azurewebsites.net/api/telegram/webhook?code=$functionKey"

# Set webhook using Telegram API
$botToken = "YOUR_BOT_TOKEN"
Invoke-RestMethod -Uri "https://api.telegram.org/bot$botToken/setWebhook?url=$webhookUrl"
```

Verify webhook is set:
```powershell
Invoke-RestMethod -Uri "https://api.telegram.org/bot$botToken/getWebhookInfo"
```

Expected response:
```json
{
  "ok": true,
  "result": {
    "url": "https://suled-bot-func.azurewebsites.net/api/telegram/webhook",
    "has_custom_certificate": false,
    "pending_update_count": 0
  }
}
```

## Step 8: Test the Bot

1. Open Telegram
2. Search for your bot by username (e.g., @suled_tournament_bot)
3. Click "Start" or send `/start` command
4. You should receive a welcome message
5. Try `/help` to see available commands

## Bot Commands

- `/start` - Start using the bot and see welcome message
- `/selectpair` - Choose your pair to receive notifications
- `/mypair` - View your current subscription
- `/unsubscribe` - Stop receiving notifications
- `/help` - Show available commands

## Architecture

### Components

1. **TelegramWebhookFunction** (HTTP Trigger)
   - Receives updates from Telegram
   - Handles user commands and callbacks
   - Route: `/api/telegram/webhook`

2. **NewTournamentNotificationFunction** (Cosmos DB Trigger)
   - Monitors Tournaments container for new documents
   - Sends notifications to all subscribed users
   - Uses Change Feed to detect new tournaments

3. **GameNotificationTimerFunction** (Timer Trigger)
   - Runs every minute
   - Checks for games starting in 5 minutes
   - Sends notifications to subscribed users

### Data Storage

Three Cosmos DB containers:

- **Tournaments** (existing): Tournament and game data
- **Subscriptions** (new): User pair subscriptions
- **Notifications** (new): Sent notification tracking

## Notification Logic

### New Tournament Notifications
- Triggered when a new tournament is added to Cosmos DB
- All active subscribers receive notification
- Users can then select their pair

### Upcoming Game Notifications
- Timer checks every minute for upcoming games
- Notifications sent 5 minutes before game time
- Includes: Round, Court, Opponent, Time
- Duplicates prevented via Notifications tracking

## Monitoring and Troubleshooting

### View Logs in Azure Portal

1. Go to Function App in Azure Portal
2. Select "Log stream" or "Monitor"
3. View real-time logs and execution history

### Check Application Insights

```powershell
# Query recent errors
az monitor app-insights query `
  --app suled-bot-insights `
  --resource-group suled-rg `
  --analytics-query "traces | where severityLevel > 2 | take 50"
```

### Common Issues

**Bot not responding to commands:**
- Verify webhook is set correctly: `getWebhookInfo`
- Check Function App logs for errors
- Ensure Function App is running (not stopped)
- Verify TelegramBotToken is correct

**No pairs showing in /selectpair:**
- Verify tournaments exist in Cosmos DB
- Check that games have Pair1 and Pair2 populated
- Review PairService logs in Application Insights

**Notifications not being sent:**
- Verify timer function is enabled
- Check that games have `ScheduledTime` set
- Verify subscriptions exist and are active
- Check Notifications container for duplicates

**Webhook errors:**
```powershell
# Delete webhook and reset
Invoke-RestMethod -Uri "https://api.telegram.org/bot$botToken/deleteWebhook"

# Set webhook again
Invoke-RestMethod -Uri "https://api.telegram.org/bot$botToken/setWebhook?url=$webhookUrl"
```

## Security

- **Bot Token**: Stored as secure parameter in Bicep, never committed to source control
- **Function Authorization**: Uses function-level keys for webhook
- **HTTPS Only**: All communication encrypted
- **Cosmos DB**: Connection string in app settings (managed by Azure)
- **Application Insights**: Secure telemetry collection

## Cost Estimates

**Monthly costs (approximate):**
- Function App (Consumption): $0-5 (depends on usage)
- Storage Account: $1-2
- Cosmos DB (400 RU/s × 2 containers): ~$25
- Application Insights: $0-5 (depends on telemetry volume)

**Total: ~$26-37/month**

Tips to reduce costs:
- Use shared throughput on Cosmos DB database
- Adjust RU/s based on actual usage
- Configure Application Insights sampling

## Updating the Bot

```powershell
# Pull latest code
git pull

# Rebuild and redeploy
cd src/SuledFunctions.TelegramBot
dotnet publish --configuration Release --output ./publish
cd publish
func azure functionapp publish suled-bot-func
```

## Environment Variables Reference

| Variable | Description | Example |
|----------|-------------|---------|
| `TelegramBotToken` | Bot token from BotFather | `123456789:ABC...` |
| `CosmosDbConnection` | Cosmos DB connection string | `AccountEndpoint=https://...` |
| `CosmosDbName` | Database name | `TournamentDb` |
| `CosmosContainerName` | Tournaments container | `Tournaments` |
| `AzureWebJobsStorage` | Function storage | Connection string |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Telemetry | Connection string |

## Next Steps

1. **Customize Notifications**
   - Modify notification timing in `GameNotificationTimerFunction`
   - Adjust message templates in `TelegramBotService`

2. **Add Features**
   - Tournament standings view
   - Game result updates
   - Player statistics
   - Admin commands

3. **Set Up Alerts**
   - Configure Azure Monitor alerts for failures
   - Set up budget alerts for cost monitoring

4. **Documentation**
   - Create user guide for tournament participants
   - Document admin procedures

## Support

For issues or questions:
- Check Application Insights logs
- Review Azure Function execution history
- Consult Telegram Bot API documentation: https://core.telegram.org/bots/api
