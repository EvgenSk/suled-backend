# Tournament Metadata Feature - Implementation Summary

## Overview
Successfully implemented tournament metadata storage and querying functionality using **Option 1: Cosmos DB Metadata** approach.

## What Was Implemented

### 1. Enhanced Data Model
**File**: `src/SuledFunctions/Models/Tournament.cs`

Added metadata fields to `Tournament` model:
- `StartDate` (DateTime?) - Tournament start date
- `EndDate` (DateTime?) - Tournament end date  
- `Location` (string) - Tournament location
- `Division` (string) - Tournament division/category
- `Description` (string) - Tournament description
- `Status` (TournamentStatus) - Current status

Created `TournamentStatus` enum:
- `Upcoming` - Tournament hasn't started yet
- `InProgress` - Tournament is currently running
- `Completed` - Tournament has finished
- `Cancelled` - Tournament was cancelled

### 2. Metadata Extraction
**File**: `src/SuledFunctions/Services/ExcelParserService.cs`

Implemented two methods for extracting metadata:

#### a) From Filename Pattern
Expected format: `TournamentName_YYYY-MM-DD_Location_Division.xlsx`

Examples:
```
SummerChampionship_2025-11-15_Chicago_DivisionA.xlsx
WinterCup_2025-12-01_NewYork_DivisionB.xlsx
```

#### b) From Excel Content (Optional)
Looks for metadata in first 5 rows:
```
Row 1: Tournament Name: | Summer Championship
Row 2: Location:        | Chicago
Row 3: Date:            | 2025-11-15
Row 4: Division:        | Division A
Row 5: Description:     | Annual summer championship
```

#### c) Auto-Status Determination
Automatically sets tournament status based on dates:
- Future start date → `Upcoming`
- Current date between start and end → `InProgress`
- Past end date → `Completed`

### 3. Tournament Service
**Files**: 
- `src/SuledFunctions/Services/ITournamentService.cs`
- `src/SuledFunctions/Services/TournamentService.cs`

Created service for querying tournaments with filters:
- Date range (`startDateFrom`, `startDateTo`)
- Location (partial match, case-insensitive)
- Division (partial match, case-insensitive)
- Status
- Max results (default 100, cap at 500)

### 4. New API Endpoint
**File**: `src/SuledFunctions/Functions/GetTournamentsFunction.cs`

**Endpoint**: `GET /api/tournaments`

**Query Parameters**:
- `startDateFrom` - Filter tournaments starting from this date
- `startDateTo` - Filter tournaments starting before this date
- `location` - Filter by location
- `division` - Filter by division
- `status` - Filter by status (Upcoming/InProgress/Completed/Cancelled)
- `maxResults` - Limit results (default 100, max 500)

**Response**: Array of `TournamentListDto`

### 5. DTO Contract
**File**: `src/SuledFunctions.Contracts/DTOs/TournamentListDto.cs`

Created data transfer object for API responses with all tournament metadata.

### 6. Dependency Injection
**File**: `src/SuledFunctions/Program.cs`

Registered `ITournamentService` in DI container.

### 7. API Documentation
**File**: `docs/api/README.md`

Updated API documentation with:
- New `/api/tournaments` endpoint details
- Query parameter descriptions
- Request/response examples
- Data model definitions

## Example Usage

### 1. Upload Tournament with Metadata in Filename
```bash
curl -X POST http://localhost:7071/api/tournament/upload \
  -H "Content-Type: application/octet-stream" \
  --data-binary "@SummerChampionship_2025-11-15_Chicago_DivisionA.xlsx"
```

### 2. Get All Upcoming Tournaments
```bash
curl "http://localhost:7071/api/tournaments?status=Upcoming"
```

### 3. Get Tournaments in November 2025
```bash
curl "http://localhost:7071/api/tournaments?startDateFrom=2025-11-01&startDateTo=2025-11-30"
```

### 4. Get Tournaments in Specific Location
```bash
curl "http://localhost:7071/api/tournaments?location=Chicago"
```

### 5. Get Division A Tournaments
```bash
curl "http://localhost:7071/api/tournaments?division=DivisionA"
```

### 6. Combine Multiple Filters
```bash
curl "http://localhost:7071/api/tournaments?location=Chicago&division=DivisionA&status=Upcoming"
```

## Response Example
```json
[
  {
    "id": "tournament-123",
    "name": "SummerChampionship",
    "startDate": "2025-11-15T09:00:00Z",
    "endDate": "2025-11-15T18:00:00Z",
    "location": "Chicago",
    "division": "Division A",
    "description": "",
    "status": "Upcoming",
    "gameCount": 24,
    "createdDate": "2025-10-31T10:00:00Z"
  }
]
```

## Benefits of This Approach

✅ **Cosmos DB Native** - Uses existing infrastructure, no new services needed  
✅ **Rich Querying** - Filter by date, location, division, status  
✅ **Flexible Metadata** - Easy to add more fields in the future  
✅ **Multiple Input Methods** - Filename pattern OR Excel content  
✅ **Auto Status** - Intelligent status determination based on dates  
✅ **Clean API** - RESTful design with query parameters  
✅ **Type-Safe** - Strongly-typed DTOs and models  

## Next Steps (Optional Enhancements)

1. **Add Pagination** - For tournaments list with many results
2. **Add Sorting** - Allow sorting by different fields (date, name, etc.)
3. **Add Full-Text Search** - Search tournament names and descriptions
4. **Add Tournament Details Endpoint** - `GET /api/tournaments/{id}` for single tournament
5. **Add Update Endpoint** - `PUT /api/tournaments/{id}` to update metadata
6. **Add Authentication** - Protect endpoints with Azure AD or API keys
7. **Add Caching** - Cache tournament list for better performance

## Testing Recommendations

1. Test uploading tournaments with different filename formats
2. Test uploading tournaments with Excel metadata
3. Test querying with different filter combinations
4. Test edge cases (no results, invalid dates, etc.)
5. Test with large number of tournaments (performance)

## Files Modified/Created

### Created:
- `src/SuledFunctions/Services/ITournamentService.cs`
- `src/SuledFunctions/Services/TournamentService.cs`
- `src/SuledFunctions/Functions/GetTournamentsFunction.cs`
- `src/SuledFunctions.Contracts/DTOs/TournamentListDto.cs`

### Modified:
- `src/SuledFunctions/Models/Tournament.cs`
- `src/SuledFunctions/Services/ExcelParserService.cs`
- `src/SuledFunctions/Program.cs`
- `docs/api/README.md`

## Build Status
✅ Project builds successfully  
✅ All functions start correctly  
✅ New endpoint registered: `GET http://localhost:7071/api/tournaments`
