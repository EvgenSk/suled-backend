# GitHub Copilot Instructions for Suled Backend

## 🚨 CRITICAL WORKFLOW RULE
**ALWAYS run tests IMMEDIATELY after making code changes, especially refactoring.**
- Command: `dotnet test --filter "FullyQualifiedName!~IntegrationTests"`
- Do NOT report work as complete until tests pass
- This is a mandatory step, not optional

## Testing Guidelines

### When Business Logic Changes
- **Always update tests** when modifying business logic, models, or service methods
- Ensure test assertions match the new expected behavior
- Update test data/mocks to reflect structural changes
- Add new test cases for new functionality or edge cases

### After Refactoring
- **CRITICAL: IMMEDIATELY run tests after ANY refactoring** - this is non-negotiable
- **REQUIRED STEP**: Run unit tests: `dotnet test --filter "FullyQualifiedName!~IntegrationTests"`
- **REQUIRED STEP**: Run integration tests (requires Docker): `dotnet test`
- **DO NOT** present work as complete until ALL tests pass
- Fix any failing tests before considering the refactoring complete
- Ensure all 87+ unit tests pass before committing
- **WORKFLOW**: Code change → Run tests → Fix failures → Verify passing → THEN report complete

### Test Maintenance
- Keep tests synchronized with code changes
- When models change (e.g., Tournament, Pair, Game), update all affected test files
- When adding new API endpoints, create corresponding test coverage
- Verify both positive and negative test scenarios

## Architecture Notes
- **Pair-Centered Data Structure**: Tournaments contain Pairs, each Pair contains their Games
- Models: `Tournament` → `TournamentPair` → `PairGame` (with OpponentPair)
- Always maintain this structure when making changes
