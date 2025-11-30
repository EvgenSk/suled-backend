using 'telegram-bot.bicep'

param appName = 'suled'
param location = 'eastus'
param telegramBotToken = '' // Set this via --parameters or secure parameter file
param cosmosDbAccountName = 'suled-cosmos'
