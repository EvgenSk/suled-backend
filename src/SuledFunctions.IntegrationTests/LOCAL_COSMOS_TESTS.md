# Local Cosmos DB Integration Tests - Quick Start

## Prerequisites

1. **Install Cosmos DB Emulator** (one-time setup):
   - Download: https://aka.ms/cosmosdb-emulator
   - Or: `choco install azure-cosmosdb-emulator`

2. **Start the Emulator**:
   ```powershell
   Start-Process "C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"
   ```
   Wait for the system tray icon to show green (emulator is ready)

3. **Create Database** (one-time or when you need fresh setup):
   ```powershell
   cd suled-backend\scripts
   .\setup-cosmos-emulator.ps1
   ```

## Running Tests

### All Local Cosmos DB Tests (Recommended)
```powershell
cd suled-backend\src\SuledFunctions.IntegrationTests
dotnet test --filter "Collection=LocalCosmosDb"
```

**Expected Runtime**: ~13 seconds for 21 tests

### Individual Test Classes

#### CRUD Operations Tests (10 tests)
```powershell
dotnet test --filter "FullyQualifiedName~CosmosDbLocalIntegrationTests"
```
Tests basic Cosmos DB operations: create, read, update, delete, query, etc.

#### TournamentService Tests (11 tests)
```powershell
dotnet test --filter "FullyQualifiedName~TournamentServiceIntegrationTests"
```
Tests the TournamentService with filtering, querying, and complex data.

### Specific Test
```powershell
dotnet test --filter "Name~GetTournamentByIdAsync_WithExistingTournament_ShouldReturnTournament"
```

## Test Coverage

### CosmosDbLocalIntegrationTests (10 tests)
✅ Create tournaments
✅ Read tournaments by ID
✅ Query multiple tournaments with filters
✅ Update existing tournaments
✅ Delete tournaments
✅ Store complex nested data (games, pairs, players)
✅ Partition key queries
✅ Preserve tournament metadata
✅ Bulk operations

### TournamentServiceIntegrationTests (11 tests)
✅ Get tournament by ID (exists and not exists)
✅ Get all tournaments
✅ Filter by date range (startDateFrom, startDateTo)
✅ Filter by location (case-insensitive)
✅ Filter by division (case-insensitive)
✅ Filter by status
✅ Multiple combined filters
✅ Limit results (maxResults)
✅ Order by start date (descending)
✅ Complex nested game data preservation

## Benefits

⚡ **Fast**: No container startup - tests run in ~13 seconds total
🔄 **Reliable**: No Docker dependencies
💻 **Local**: Works offline
🔍 **Debuggable**: View data at https://localhost:8081/_explorer/index.html
🎯 **Real**: Uses actual Cosmos DB SDK and query engine

## Troubleshooting

### Emulator Not Running
```
Failed to connect to Cosmos DB Emulator
```
**Fix**: Start the emulator (see Prerequisites above)

### Database Not Created
```
Database 'TournamentDb' not found
```
**Fix**: Run `.\scripts\setup-cosmos-emulator.ps1`

### Port Already in Use
```
Port 8081 is already in use
```
**Fix**: Stop other instances of the emulator or close applications using port 8081

### Tests Failing with Connection Issues
**Fix**: 
1. Restart the Cosmos DB Emulator
2. Wait 30 seconds for full initialization
3. Run tests again

## Data Explorer

View and inspect test data during debugging:
https://localhost:8081/_explorer/index.html

Test databases are prefixed with `SuledTestDb` and `TournamentDb`.

## Cleaning Up

Tests automatically clean up after themselves, but if you want to manually remove test data:

```powershell
# Open Data Explorer
Start-Process "https://localhost:8081/_explorer/index.html"

# Delete test databases manually, or use the cleanup script if needed
```

## CI/CD Integration

For CI/CD pipelines, ensure:
1. Cosmos DB Emulator is installed on build agents
2. Emulator is started before running tests
3. Allow 30-60 seconds for emulator initialization
4. Run tests with appropriate timeout settings

Example (GitHub Actions):
```yaml
- name: Start Cosmos DB Emulator
  run: |
    Start-Process "C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"
    Start-Sleep -Seconds 60

- name: Setup Cosmos DB
  run: .\scripts\setup-cosmos-emulator.ps1

- name: Run Integration Tests
  run: dotnet test --filter "Collection=LocalCosmosDb"
```
