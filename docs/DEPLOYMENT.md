# Suled Backend - Deployment Summary

## Deployment Completed Successfully ✅

**Date**: October 28, 2025  
**Environment**: Development  
**Region**: North Europe (Ireland)  
**Resource Group**: suled-rg-dev

---

## 📦 Deployed Resources

### Azure Function App
- **Name**: `suled-app-func`
- **URL**: https://suled-app-func.azurewebsites.net
- **Runtime**: .NET 8.0 (Isolated)
- **Plan**: Consumption (Y1)
- **Status**: ✅ Running

### Storage Account
- **Name**: `suledappst`
- **Type**: Standard LRS
- **Containers**:
  - `tournaments` - For tournament Excel file uploads

### Cosmos DB
- **Account**: `suled-app-cosmos`
- **Database**: `TournamentDb`
- **Container**: `Tournaments`
- **Partition Key**: `/id`
- **Throughput**: 400 RU/s

---

## 🌐 API Endpoints

### Get All Pairs
```
GET https://suled-app-func.azurewebsites.net/api/pairs
```
Returns all tournament pairs from Cosmos DB.

### Get Games for a Pair
```
GET https://suled-app-func.azurewebsites.net/api/games/pair/{pairId}
```
Returns all games for a specific pair ID.

### Upload Tournament
```
POST https://suled-app-func.azurewebsites.net/api/tournament/upload
Content-Type: multipart/form-data
```
Upload a tournament Excel file. Triggers automatic processing.

### Process Tournament Blob (Internal)
```
Blob Trigger: tournaments/{name}
```
Automatically processes uploaded tournament files.

---

## 🔑 Configuration

### Environment Variables (Set in Function App)
- `AzureWebJobsStorage` - Function app storage
- `CosmosDbConnection` - Cosmos DB connection string
- `CosmosDbName` - `TournamentDb`
- `CosmosContainerName` - `Tournaments`
- `FUNCTIONS_WORKER_RUNTIME` - `dotnet-isolated`
- `FUNCTIONS_EXTENSION_VERSION` - `~4`

---

## 🚀 Deployment Commands

### Quick Redeploy
```powershell
cd src/SuledFunctions
dotnet publish -c Release -o .\bin\Release\publish
func azure functionapp publish suled-app-func
```

### Full Infrastructure Update
```powershell
az deployment group create `
  --resource-group suled-rg-dev `
  --template-file ./infra/main.bicep `
  --parameters ./infra/main.bicepparam `
  --parameters location=northeurope
```

---

## 📊 Cost Estimate (Monthly)

Based on North Europe pricing:

| Resource | SKU | Estimated Cost |
|----------|-----|----------------|
| Function App | Consumption | ~€0-5 (first 1M executions free) |
| Storage Account | Standard LRS | ~€0.02/GB |
| Cosmos DB | 400 RU/s | ~€20/month |
| **Total** | | **~€20-25/month** |

*Costs will scale with actual usage*

---

## 🔧 Management

### View Logs
```powershell
func azure functionapp logstream suled-app-func
```

### View Application Insights
Navigate to Azure Portal → suled-app-func → Application Insights

### Clean Up Resources
```powershell
az group delete --name suled-rg-dev --yes
```

---

## ✅ Testing

### Test GetPairs Endpoint
```powershell
curl https://suled-app-func.azurewebsites.net/api/pairs
```

### Test Upload (requires file)
```powershell
curl -X POST https://suled-app-func.azurewebsites.net/api/tournament/upload `
  -F "file=@tournament.xlsx"
```

---

## 📝 Next Steps

1. ✅ Configure CI/CD pipeline with GitHub Actions
2. ✅ Set up monitoring and alerts
3. ✅ Add authentication/authorization
4. ✅ Configure custom domain (optional)
5. ✅ Deploy to production environment

---

## 🔗 Useful Links

- [Azure Portal](https://portal.azure.com)
- [Function App](https://portal.azure.com/#resource/subscriptions/b2418834-5858-4ad0-a26b-fd8cc36e21ca/resourceGroups/suled-rg-dev/providers/Microsoft.Web/sites/suled-app-func)
- [Cosmos DB](https://portal.azure.com/#resource/subscriptions/b2418834-5858-4ad0-a26b-fd8cc36e21ca/resourceGroups/suled-rg-dev/providers/Microsoft.DocumentDB/databaseAccounts/suled-app-cosmos)
- [Storage Account](https://portal.azure.com/#resource/subscriptions/b2418834-5858-4ad0-a26b-fd8cc36e21ca/resourceGroups/suled-rg-dev/providers/Microsoft.Storage/storageAccounts/suledappst)
