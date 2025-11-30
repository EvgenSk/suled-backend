# Integration Tests

This project contains integration tests for the Suled backend Azure Functions. These tests use real Azure service emulators (Azurite for Blob Storage and Cosmos DB Emulator) via Testcontainers to validate end-to-end functionality.

## Prerequisites

### For Testcontainers-based tests:
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (required for Testcontainers)
- Windows with WSL2 enabled (for Docker on Windows)

### For Local Cosmos DB Emulator tests:
- [Azure Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator) installed and running
- No Docker required

### General:
- .NET 8.0 SDK or later

**⚠️ Known Limitations:**
- **Cosmos DB Emulator (Testcontainers)**: The Linux Cosmos DB emulator has significant startup delays (2+ minutes) and may hang during initialization. Tests using Testcontainers are functional but extremely slow.
- **Cosmos DB Emulator (Local)**: For faster integration testing, use the locally running Cosmos DB Emulator. Tests are fast and reliable when the emulator is already running.
- **Blob Storage Tests**: Fast and reliable (~9 seconds for all 6 tests)

## Test Structure

```
SuledFunctions.IntegrationTests/
├── Infrastructure/          # Test fixtures and setup
│   ├── AzuriteFixture.cs   # Azure Blob Storage emulator
│   ├── CosmosDbFixture.cs  # Cosmos DB emulator
│   └── TestCollections.cs  # xUnit test collections
├── BlobStorage/            # Blob storage integration tests
│   └── BlobStorageIntegrationTests.cs
├── CosmosDb/               # Cosmos DB integration tests
│   └── CosmosDbIntegrationTests.cs
├── EndToEnd/               # Complete workflow tests
│   └── TournamentWorkflowTests.cs
└── Helpers/                # Test utilities
    └── ExcelTestHelper.cs  # Excel file generation
```

## Running Tests

### Quick Start: Local Cosmos DB Emulator Tests

**Recommended for fast iteration during development:**

1. Start the Cosmos DB Emulator (if not already running)
2. Run the setup script to create the database:
   ```powershell
   cd ..\..\scripts
   .\setup-cosmos-emulator.ps1
   ```

3. Run the local Cosmos DB tests:
   ```powershell
   dotnet test --filter "FullyQualifiedName~CosmosDbLocal"
   ```

These tests are **fast** (no container startup time) and connect to your locally running emulator at `https://localhost:8081`.

### All Integration Tests

```powershell
dotnet test
```

### Specific Test Category

```powershell
# Run only blob storage tests
dotnet test --filter "FullyQualifiedName~BlobStorage"

# Run only Cosmos DB tests (Testcontainers - slow)
dotnet test --filter "FullyQualifiedName~CosmosDb"

# Run only local Cosmos DB tests (fast - requires emulator running)
dotnet test --filter "FullyQualifiedName~CosmosDbLocal"

# Run only TournamentService integration tests
dotnet test --filter "FullyQualifiedName~TournamentServiceIntegration"

# Run only end-to-end tests
dotnet test --filter "FullyQualifiedName~EndToEnd"
```

### Single Test

```powershell
dotnet test --filter "FullyQualifiedName~TournamentWorkflowTests.EndToEnd_UploadAndParseTournament_ShouldCompleteSuccessfully"
```

## Local Cosmos DB Emulator Setup

**This is the recommended approach for integration tests during development.**

### First-Time Setup

1. **Install Azure Cosmos DB Emulator** (if not already installed):
   - Download from: https://aka.ms/cosmosdb-emulator
   - Or install via Chocolatey: `choco install azure-cosmosdb-emulator`

2. **Start the Emulator**:
   - Launch from Start menu: "Azure Cosmos DB Emulator"
   - Or via PowerShell: `Start-Process "C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"`
   - Wait for the emulator to fully start (system tray icon will show green)

3. **Create Test Database**:
   ```powershell
   cd scripts
   .\setup-cosmos-emulator.ps1
   ```
   
   This script creates the `TournamentDb` database and `Tournaments` container.

### Running Tests

Once the emulator is running, you can run tests repeatedly without any startup delay:

```powershell
# Run all local Cosmos DB tests
dotnet test --filter "FullyQualifiedName~CosmosDbLocal"

# Run specific test class
dotnet test --filter "FullyQualifiedName~TournamentServiceIntegrationTests"

# Run a specific test
dotnet test --filter "Name~GetTournamentByIdAsync_WithExistingTournament_ShouldReturnTournament"
```

### Benefits

- ⚡ **Fast**: No container startup time (tests run in ~13 seconds total)
- 🔄 **Reliable**: No Docker dependencies or network issues
- 💻 **Local**: Works offline
- 🔍 **Debuggable**: Can inspect data in Cosmos DB Emulator Data Explorer
- 🎯 **Real**: Uses the actual Cosmos DB SDK and query engine

### Data Explorer

View and manage test data at:
https://localhost:8081/_explorer/index.html

## How It Works

### Test Fixtures

Integration tests use xUnit's `ICollectionFixture` to share test infrastructure:

- **LocalCosmosDbFixture**: Connects to locally running Cosmos DB Emulator (fast, recommended)
- **AzuriteFixture**: Spins up Azurite container (local Azure Storage emulator)
- **CosmosDbFixture**: Spins up Cosmos DB emulator container (slow, not recommended)
- **TestCollections**: Defines test collections that share fixtures

### Test Execution Flow

1. **Setup** (once per test collection):
   - Testcontainers starts Docker containers
   - Fixtures initialize Azure service clients
   
2. **Test Execution**:
   - Tests use real Azure SDK clients
   - Operations hit actual emulated services
   
3. **Cleanup** (once per test collection):
   - Containers are stopped and removed
   - Resources are disposed

### Docker Requirements

**Important**: Docker Desktop must be running before executing tests!

The tests will automatically:
- Pull required Docker images (first run only)
- Start containers
- Configure connection strings
- Stop and remove containers after tests

## Test Coverage

### BlobStorageIntegrationTests (7 tests)
- ✅ Upload blobs
- ✅ Download blobs
- ✅ List blobs
- ✅ Delete blobs
- ✅ Blob metadata
- ✅ Large file uploads (5MB+)

### CosmosDbIntegrationTests (8 tests) - DISABLED (Slow Testcontainers)
- ✅ Create documents
- ✅ Read documents
- ✅ Query documents
- ✅ Update documents
- ✅ Delete documents
- ✅ Nested data structures
- ✅ Partition key queries

### CosmosDbLocalIntegrationTests (10 tests) - **RECOMMENDED**
Uses locally running Cosmos DB Emulator for fast execution:
- ✅ Create documents
- ✅ Read documents
- ✅ Query documents (multiple items, filtering)
- ✅ Update documents
- ✅ Delete documents
- ✅ Nested data structures (games, pairs, players)
- ✅ Partition key queries
- ✅ Metadata preservation
- ✅ Bulk operations

### TournamentServiceIntegrationTests (11 tests) - **RECOMMENDED**
Tests the TournamentService with real Cosmos DB operations:
- ✅ Get tournament by ID
- ✅ Get all tournaments
- ✅ Filter by date range
- ✅ Filter by location
- ✅ Filter by division
- ✅ Filter by status
- ✅ Multiple combined filters
- ✅ Max results limiting
- ✅ Ordering by start date
- ✅ Complex nested game data
- ✅ Handle non-existing tournaments

### TournamentWorkflowTests (7 tests) - DISABLED (Slow Testcontainers)
- ✅ Upload Excel → Parse → Store → Retrieve
- ✅ Extract unique pairs from tournaments
- ✅ Filter games by pair ID
- ✅ Multiple concurrent tournaments
- ✅ Update tournament data
- ✅ Delete tournaments (cascade)

## Troubleshooting

### Cosmos DB Emulator Not Running

```
Failed to connect to Cosmos DB Emulator
```

**Solution**: 
1. Start the Azure Cosmos DB Emulator from the Start menu, or
2. Run `Start-Process "C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"`
3. Wait for the emulator to fully start (check the system tray icon)
4. Run the setup script: `.\scripts\setup-cosmos-emulator.ps1`

### Docker Not Running (for Testcontainer tests)

```
Error: Docker endpoint not found
```

**Solution**: Start Docker Desktop

### Port Conflicts

```
Error: Port already in use
```

**Solution**: Stop other services using ports 10000-10002 (Azurite) or 8081 (Cosmos DB Emulator)

### Slow First Run

First execution downloads Docker images (~1-2GB total). Subsequent runs are much faster.

### Container Cleanup Issues

If containers don't stop properly:

```powershell
# List all containers
docker ps -a

# Stop and remove Testcontainers
docker stop $(docker ps -aq --filter "name=testcontainers")
docker rm $(docker ps -aq --filter "name=testcontainers")
```

## CI/CD Integration

Add to `.github/workflows/backend-ci.yml`:

```yaml
- name: Run Integration Tests
  run: |
    # Start Docker service (GitHub Actions)
    sudo systemctl start docker
    
    # Run integration tests
    dotnet test --filter "Category=Integration" --logger "trybx;verbosity=detailed"
```

## Performance

### Local Cosmos DB Emulator Tests (Recommended)
- **Setup**: <1 second (connection to running emulator)
- **Per test**: 50-200ms
- **CosmosDbLocalIntegrationTests (10 tests)**: ~5 seconds
- **TournamentServiceIntegrationTests (11 tests)**: ~8 seconds
- **Total**: ~13 seconds

### Testcontainers Tests (Slow - Not Recommended)
- **Setup**: 60-120 seconds (container startup + emulator initialization)
- **Per test**: 50-500ms
- **Total suite**: ~2-3 minutes

## Best Practices

1. **Isolation**: Each test should clean up its data
2. **Idempotency**: Tests should be repeatable
3. **Realistic Data**: Use data similar to production
4. **Error Scenarios**: Test failures and edge cases
5. **Performance**: Keep tests fast (<1 second per test)

## Next Steps

Consider adding:
- **Performance tests**: Load testing with large datasets
- **Chaos tests**: Network failures, timeouts
- **Security tests**: Authentication, authorization
- **Contract tests**: API schema validation
