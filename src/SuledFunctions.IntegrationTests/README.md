# Integration Tests

This project contains integration tests for the Suled backend Azure Functions. These tests use real Azure service emulators (Azurite for Blob Storage and Cosmos DB Emulator) via Testcontainers to validate end-to-end functionality.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) (required for Testcontainers)
- .NET 8.0 SDK or later
- Windows with WSL2 enabled (for Docker on Windows)

**⚠️ Known Limitations:**
- **Cosmos DB Emulator**: The Linux Cosmos DB emulator has significant startup delays (2+ minutes) and may hang during initialization. Tests are functional but extremely slow. For faster integration testing, consider using a real Azure Cosmos DB instance or mocking Cosmos DB operations.
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

### All Integration Tests

```powershell
dotnet test
```

### Specific Test Category

```powershell
# Run only blob storage tests
dotnet test --filter "FullyQualifiedName~BlobStorage"

# Run only Cosmos DB tests
dotnet test --filter "FullyQualifiedName~CosmosDb"

# Run only end-to-end tests
dotnet test --filter "FullyQualifiedName~EndToEnd"
```

### Single Test

```powershell
dotnet test --filter "FullyQualifiedName~TournamentWorkflowTests.EndToEnd_UploadAndParseTournament_ShouldCompleteSuccessfully"
```

## How It Works

### Test Fixtures

Integration tests use xUnit's `ICollectionFixture` to share test infrastructure:

- **AzuriteFixture**: Spins up Azurite container (local Azure Storage emulator)
- **CosmosDbFixture**: Spins up Cosmos DB emulator container
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

### CosmosDbIntegrationTests (8 tests)
- ✅ Create documents
- ✅ Read documents
- ✅ Query documents
- ✅ Update documents
- ✅ Delete documents
- ✅ Nested data structures
- ✅ Partition key queries

### TournamentWorkflowTests (7 tests)
- ✅ Upload Excel → Parse → Store → Retrieve
- ✅ Extract unique pairs from tournaments
- ✅ Filter games by pair ID
- ✅ Multiple concurrent tournaments
- ✅ Update tournament data
- ✅ Delete tournaments (cascade)

## Troubleshooting

### Docker Not Running

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

Typical execution times:
- **Setup**: 5-15 seconds (container startup)
- **Per test**: 50-500ms
- **Total suite**: ~30-60 seconds

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
