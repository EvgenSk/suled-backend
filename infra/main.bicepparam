using './main.bicep'

// appName will use default with uniqueString for globally unique names
param environment = 'dev'
param location = 'northeurope'

// CORS origins - update with your Static Web App URL
param allowedOrigins = [
  'https://jolly-field-005c56703.6.azurestaticapps.net'
  'http://localhost:5173'
  'http://localhost:5174'
]
