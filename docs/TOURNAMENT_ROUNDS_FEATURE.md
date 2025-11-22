# Tournament Rounds Feature Implementation

## Overview
Added comprehensive round scheduling information to tournaments, calculating when each round starts and finishes based on tournament metadata (start/end dates and times).

## Backend Changes

### 1. New Models
- **`TournamentRound.cs`**: Core model representing a tournament round
  - `RoundNumber`: The sequential round number
  - `StartTime`: When the round begins
  - `EndTime`: When the round concludes
  - `GameCount`: Number of unique games in the round

- **`TournamentRoundDto.cs`**: Data transfer object for API responses

### 2. Tournament Model Enhancement
- Added `Rounds` property to `Tournament.cs` to store calculated round information
- Rounds are automatically populated during tournament parsing

### 3. Round Calculation Service
- **`RoundCalculationService.cs`**: Intelligent service that calculates round schedules
  - Uses tournament start/end dates and daily time windows
  - Accounts for multiple courts running games in parallel
  - Adds breaks between rounds (5 minutes default)
  - Handles multi-day tournaments
  - Defaults:
    - Game duration: 15 minutes
    - Break between rounds: 5 minutes
    - Daily window: 9:00 AM - 6:00 PM

### 4. Integration Points
- **`ExcelParserService.cs`**: Now calculates rounds after parsing games
- **`Program.cs`**: Registered `IRoundCalculationService` in DI container
- **`GetTournamentsFunction.cs`**: Returns rounds in tournament list DTOs
- **`GetTournamentFunction.cs`**: Returns full tournament with rounds

### 5. Updated DTOs
- **`TournamentListDto.cs`**: Added `Rounds` collection

## Frontend Changes

### 1. Type Definitions
- **`types/index.ts`**: Added `TournamentRound` interface
  - `roundNumber`: number
  - `startTime`: string (ISO datetime)
  - `endTime`: string (ISO datetime)
  - `gameCount`: number
- Updated `Tournament` interface to include `rounds: TournamentRound[]`

### 2. New Component
- **`RoundsDisplay.vue`**: Visual component for displaying tournament schedule
  - Grid layout showing all rounds
  - Displays round number, game count, start/end times
  - Responsive design with hover effects
  - Auto-formats datetime using new formatting utility

### 3. Enhanced Composable
- **`useFormatting.ts`**: Added `formatDateTime()` function
  - Formats date and time together in readable format
  - Used throughout the rounds display

### 4. UI Integration
- **`TournamentDetailView.vue`**: 
  - Added `RoundsDisplay` component at the top
  - Shows complete schedule before pair selection
  
- **`TournamentList.vue`**: 
  - Added round count badge to tournament cards
  - Shows "X rounds" alongside games count

## Calculation Logic

The round calculation algorithm works as follows:

1. **Extract all rounds**: Gets unique round numbers from all games
2. **Determine tournament timing**: Uses start date/time or defaults
3. **For each round**:
   - Count unique games (accounting for pair-centered duplication)
   - Determine maximum court usage
   - Calculate parallel game execution (games per court)
   - Compute round duration based on game time × games per court
   - Add break time before next round
4. **Handle day boundaries**: 
   - Moves to next day if end time exceeded
   - Respects tournament end date

## Example Calculation

Given:
- 4 unique games in Round 1 across 2 courts
- Start time: 9:00 AM
- Game duration: 15 minutes
- Break: 5 minutes

Calculation:
- Games per court: 4 / 2 = 2 games sequentially
- Round duration: 2 × 15 = 30 minutes
- Round 1: 9:00 AM - 9:30 AM
- Round 2 starts: 9:35 AM (after 5-minute break)

## Benefits

1. **User Visibility**: Players can see complete tournament schedule
2. **Planning**: Helps with logistics and timing expectations
3. **Realistic Estimates**: Accounts for parallel court usage
4. **Flexibility**: Handles single and multi-day tournaments
5. **Extensibility**: Easy to adjust defaults or add configuration

## Future Enhancements

Potential improvements:
- Configurable game duration per tournament
- Custom break times between rounds
- Integration with actual game scheduling
- Round-based filtering in tournament views
- Export schedule to calendar formats
- Real-time updates as games progress

## Testing Recommendations

1. Upload tournament Excel files with various round counts
2. Verify round calculations appear in both list and detail views
3. Test with tournaments that span multiple days
4. Validate datetime formatting across timezones
5. Check responsive design on mobile devices

## Files Modified

### Backend (9 files)
- `src/SuledFunctions/Models/Tournament.cs`
- `src/SuledFunctions/Models/TournamentRound.cs` (new)
- `src/SuledFunctions/Services/RoundCalculationService.cs` (new)
- `src/SuledFunctions/Services/Interfaces/IRoundCalculationService.cs` (new)
- `src/SuledFunctions/Services/ExcelParserService.cs`
- `src/SuledFunctions/Program.cs`
- `src/SuledFunctions.Contracts/DTOs/TournamentRoundDto.cs` (new)
- `src/SuledFunctions.Contracts/DTOs/TournamentListDto.cs`
- `src/SuledFunctions/Functions/GetTournamentsFunction.cs`

### Frontend (5 files)
- `src/types/index.ts`
- `src/composables/useFormatting.ts`
- `src/components/RoundsDisplay.vue` (new)
- `src/components/TournamentList.vue`
- `src/views/TournamentDetailView.vue`

---

**Status**: ✅ Implementation Complete
**Date**: November 22, 2025
