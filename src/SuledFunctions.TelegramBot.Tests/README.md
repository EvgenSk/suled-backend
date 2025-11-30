# Telegram Bot Tests

This project contains unit tests for the Suled Telegram Bot functionality.

## Test Structure

### Models Tests
- **SentNotificationTests**: Tests for the `SentNotification` model
  - Default value initialization
  - Custom value assignment
  - Different notification types
  - Record type behavior

### Services Tests
- **SubscriptionServiceTests**: Tests for user subscription management
  - Subscription creation and defaults
  - Custom values
  - Record type `with` syntax

- **PairServiceTests**: Tests for pair data retrieval
  - DTO property validation
  - Multiple pair scenarios

- **TelegramBotServiceTests**: Tests for bot message handling
  - Service initialization
  - Update handling
  - Notification sending

### Functions Tests
- **TelegramWebhookFunctionTests**: Tests for webhook HTTP endpoint
  - Function initialization
  - Attribute validation

- **NewTournamentNotificationFunctionTests**: Tests for new tournament notifications
  - Empty subscription handling
  - Active subscription notifications
  - Multiple tournament scenarios

- **GameNotificationTimerFunctionTests**: Tests for game reminder notifications
  - Timer execution with no subscriptions
  - Upcoming game notifications
  - Duplicate notification prevention
  - Timer trigger attribute validation

## Running Tests

```powershell
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~GameNotificationTimerFunctionTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~Run_WithUpcomingGame_ShouldSendNotification"
```

## Test Coverage

Run tests with coverage:
```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Generate HTML coverage report (requires ReportGenerator):
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:Html
```

## Dependencies

- **xUnit**: Testing framework
- **Moq**: Mocking framework for isolating dependencies
- **FluentAssertions**: Fluent assertion library for readable tests

## Test Patterns

### Arrange-Act-Assert
All tests follow the AAA pattern:
```csharp
// Arrange: Set up test data and mocks
var subscription = new UserSubscription { ... };

// Act: Execute the code under test
var result = await service.MethodAsync();

// Assert: Verify the results
result.Should().NotBeNull();
```

### Mocking
Services are mocked to isolate units under test:
```csharp
var mockService = new Mock<ISubscriptionService>();
mockService.Setup(x => x.GetActiveSubscriptionsAsync())
    .ReturnsAsync(new List<UserSubscription>());
```

### Theory Tests
Multiple scenarios tested with `[Theory]` and `[InlineData]`:
```csharp
[Theory]
[InlineData("NewTournament")]
[InlineData("UpcomingGame")]
public void Test_MultipleScenarios(string type) { ... }
```

## Adding New Tests

When adding new functionality:
1. Create corresponding test class in matching namespace
2. Follow existing test patterns (AAA, mocking)
3. Test happy path and edge cases
4. Verify attributes for Azure Functions
5. Run tests to ensure they pass

## CI/CD Integration

These tests should be run as part of your CI/CD pipeline:

```yaml
# GitHub Actions example
- name: Run Tests
  run: dotnet test --configuration Release --no-build --verbosity normal
```
