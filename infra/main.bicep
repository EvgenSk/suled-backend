// Main infrastructure file for Suled Tournament Management System
// Deploys Azure Functions, Storage Account, and Cosmos DB

@description('The Azure region where resources will be deployed')
param location string = resourceGroup().location

@description('Name of the application')
param appName string = 'suled-${uniqueString(resourceGroup().id)}'

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

// Storage Account for Function App and Blobs
resource storageAccount 'Microsoft.Storage/storageAccounts@2025-01-01' = {
  name: '${replace(appName, '-', '')}st'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }

  // Blob service for tournament files
  resource blobService 'blobServices' = {
    name: 'default'
    
    // Container for tournament Excel files
    resource tournamentsContainer 'containers' = {
      name: 'tournaments'
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

// Cosmos DB Account for storing tournament data
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2025-04-15' = {
  name: '${appName}-cosmos'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    disableKeyBasedMetadataWriteAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

// Cosmos DB Database
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2025-04-15' = {
  parent: cosmosDbAccount
  name: 'TournamentDb'
  properties: {
    resource: {
      id: 'TournamentDb'
    }
  }
}

// Cosmos DB Container for Tournaments
resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2025-04-15' = {
  parent: cosmosDb
  name: 'Tournaments'
  properties: {
    resource: {
      id: 'Tournaments'
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
    }
    options: {
      throughput: 400
    }
  }
}

// App Service Plan for Function App
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${appName}-func'
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower('${appName}-func')
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'CosmosDbConnection'
          value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'CosmosDbName'
          value: cosmosDb.name
        }
        {
          name: 'CosmosContainerName'
          value: cosmosContainer.name
        }
      ]
      netFrameworkVersion: 'v8.0'
    }
    httpsOnly: true
  }
}

// Outputs
output functionAppName string = functionApp.name
output storageAccountName string = storageAccount.name
output cosmosDbAccountName string = cosmosDbAccount.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
