# Suled Backend

Backend API for Suled tournament management system built with Azure Functions.

## Related Repositories

- **Mobile Apps**: [suled-mobile](../suled-mobile) - Android, iOS, and Watch apps
- **Main Repository**: [Suled](../Suled) - Original monorepo (deprecated)

## Project Structure

```
suled-backend/
├── .github/
│   └── workflows/          # CI/CD pipelines
│       └── backend-ci.yml  # Main CI/CD workflow
├── src/
│   ├── SuledFunctions/           # Azure Functions project
│   │   ├── Functions/            # HTTP triggers
│   │   ├── Models/               # Domain models
│   │   └── Services/             # Business logic
│   ├── SuledFunctions.Tests/     # Unit tests
│   └── SuledFunctions.Contracts/ # Shared API DTOs (NuGet package)
├── infra/
│   ├── main.bicep          # Infrastructure as Code
│   └── main.bicepparam     # Bicep parameters
├── docs/
│   └── api/                # API documentation
├── scripts/                # Deployment scripts
└── Suled.sln              # Solution file
```

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (for deployment)
- Visual Studio 2022 or VS Code with Azure Functions extension

### Local Development

1. **Clone the repository**
   ```powershell
   git clone <repository-url>
   cd suled-backend
   ```

2. **Restore dependencies**
   ```powershell
   dotnet restore
   ```

3. **Configure local settings**
   
   Copy `src/SuledFunctions/local.settings.json.example` to `src/SuledFunctions/local.settings.json` and update:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "CosmosDbConnectionString": "your-connection-string",
       "StorageAccountConnectionString": "your-connection-string"
     }
   }
   ```

4. **Run the Functions locally**
   ```powershell
   cd src/SuledFunctions
   func start
   ```

5. **Run tests**
   ```powershell
   dotnet test
   ```

## API Endpoints

### Pairs Management
- `GET /api/pairs` - Get all tournament pairs
- `GET /api/pairs/{pairId}/games` - Get games for a specific pair

### Tournament Management
- `POST /api/tournaments/upload` - Upload tournament Excel file
- Internal: Blob trigger processes uploaded tournaments

## Contracts Package

The `SuledFunctions.Contracts` project contains shared DTOs used by both backend and mobile applications:

```csharp
using Suled.Contracts.DTOs;

// Example usage
var game = new GameDto
{
    Id = "game-123",
    Round = 1,
    CourtNumber = 5,
    Pair1 = "Team A",
    Pair2 = "Team B"
};
```

### Publishing the Contracts Package

```powershell
cd src/SuledFunctions.Contracts
dotnet pack --configuration Release
dotnet nuget push bin/Release/Suled.Contracts.1.0.0.nupkg
```

## Deployment

### Azure Deployment via GitHub Actions

The CI/CD pipeline automatically deploys to Azure when pushing to:
- `develop` branch → Dev environment
- `main` branch → Production environment

Required GitHub Secrets:
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE_DEV`
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
- `AZURE_CREDENTIALS`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RG` (Resource Group name)

### Manual Deployment

```powershell
# Login to Azure
az login

# Deploy infrastructure
az deployment group create `
  --resource-group suled-rg `
  --template-file infra/main.bicep `
  --parameters infra/main.bicepparam

# Publish Functions
cd src/SuledFunctions
func azure functionapp publish <function-app-name>
```

## Testing

### Run all tests
```powershell
dotnet test
```

### Run with coverage
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

### Run specific test class
```powershell
dotnet test --filter "FullyQualifiedName~GetPairsFunctionTests"
```

## Architecture

### Technology Stack
- **Runtime**: .NET 8.0
- **Hosting**: Azure Functions (Consumption Plan)
- **Database**: Azure Cosmos DB
- **Storage**: Azure Blob Storage
- **Monitoring**: Application Insights

### Key Components

1. **Functions** - HTTP triggers for API endpoints
2. **Services** - Business logic and data processing
3. **Models** - Domain entities and data structures
4. **Contracts** - Shared DTOs for API communication

## Development Guidelines

### Code Style
- Follow C# coding conventions
- Use nullable reference types
- Enable all compiler warnings
- Keep functions focused and single-purpose

### Testing
- Write unit tests for all services
- Mock external dependencies (Cosmos DB, Blob Storage)
- Aim for >80% code coverage

### API Versioning
- Use route prefixes for versioning: `/api/v1/pairs`
- Maintain backward compatibility
- Document breaking changes in CHANGELOG.md

## Troubleshooting

### Common Issues

**Issue**: Functions won't start locally
- Ensure Azure Storage Emulator is running
- Check `local.settings.json` configuration

**Issue**: CosmosDB connection fails
- Verify connection string in settings
- Check firewall rules in Azure portal

**Issue**: Build errors after updates
- Run `dotnet clean` and `dotnet restore`
- Delete `bin` and `obj` folders

## Contributing

1. Create a feature branch from `develop`
2. Make your changes with tests
3. Submit a pull request
4. Ensure CI passes

## License

[Your License Here]

## Support

For issues or questions:
- Create an issue in this repository
- Contact: [your-email]
