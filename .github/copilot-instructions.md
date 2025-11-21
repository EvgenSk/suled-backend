# GitHub Copilot Instructions for Suled Backend

## Testing Guidelines

### When Business Logic Changes
- **Always update tests** when modifying business logic, models, or service methods
- Ensure test assertions match the new expected behavior
- Update test data/mocks to reflect structural changes
- Add new test cases for new functionality or edge cases

### After Refactoring
- **Always run tests** after completing any refactoring work
- Run unit tests: `dotnet test --filter "FullyQualifiedName!~IntegrationTests"`
- Run integration tests (requires Docker): `dotnet test`
- Fix any failing tests before considering the refactoring complete
- Ensure all 87+ unit tests pass before committing

### Test Maintenance
- Keep tests synchronized with code changes
- When models change (e.g., Tournament, Pair, Game), update all affected test files
- When adding new API endpoints, create corresponding test coverage
- Verify both positive and negative test scenarios

## Architecture Notes
- **Pair-Centered Data Structure**: Tournaments contain Pairs, each Pair contains their Games
- Models: `Tournament` → `TournamentPair` → `PairGame` (with OpponentPair)
- Always maintain this structure when making changes
