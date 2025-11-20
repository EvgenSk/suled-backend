# API Documentation

## Base URL

- **Development**: `https://suled-functions-dev.azurewebsites.net/api`
- **Production**: `https://suled-functions.azurewebsites.net/api`

## Authentication

Currently, the API does not require authentication. This will be added in a future version.

## Endpoints

---

### Get Tournaments

Retrieves a list of tournaments with optional filtering.

**Endpoint**: `GET /tournaments`

**Query Parameters**:
- `startDateFrom` (optional): Filter tournaments starting from this date (ISO 8601 format)
- `startDateTo` (optional): Filter tournaments starting before this date (ISO 8601 format)
- `location` (optional): Filter by location (case-insensitive partial match)
- `division` (optional): Filter by division (case-insensitive partial match)
- `status` (optional): Filter by status (`Upcoming`, `InProgress`, `Completed`, `Cancelled`)
- `maxResults` (optional): Maximum number of results to return (default: 100, max: 500)

**Request Examples**:
```bash
# Get all upcoming tournaments
curl "https://suled-functions.azurewebsites.net/api/tournaments?status=Upcoming"

# Get tournaments in November 2025
curl "https://suled-functions.azurewebsites.net/api/tournaments?startDateFrom=2025-11-01&startDateTo=2025-11-30"

# Get tournaments in Chicago, Division A
curl "https://suled-functions.azurewebsites.net/api/tournaments?location=Chicago&division=DivisionA"
```

**Response**: `200 OK`

```json
[
  {
    "id": "tournament-123",
    "name": "Summer Championship",
    "startDate": "2025-11-15T09:00:00Z",
    "endDate": "2025-11-15T18:00:00Z",
    "location": "Chicago",
    "division": "Division A",
    "description": "Annual summer championship tournament",
    "status": "Upcoming",
    "gameCount": 24,
    "createdDate": "2025-10-31T10:00:00Z"
  },
  {
    "id": "tournament-456",
    "name": "Winter Cup",
    "startDate": "2025-12-01T09:00:00Z",
    "endDate": "2025-12-01T17:00:00Z",
    "location": "New York",
    "division": "Division B",
    "description": "",
    "status": "Upcoming",
    "gameCount": 18,
    "createdDate": "2025-10-30T14:00:00Z"
  }
]
```

**Response Schema**: Array of `TournamentListDto`

| Field | Type | Description |
|-------|------|-------------|
| id | string | Unique tournament identifier |
| name | string | Tournament name |
| startDate | DateTime? | Tournament start date |
| endDate | DateTime? | Tournament end date |
| location | string | Tournament location |
| division | string | Tournament division/category |
| description | string | Tournament description |
| status | string | Status (Upcoming, InProgress, Completed, Cancelled) |
| gameCount | int | Number of games in tournament |
| createdDate | DateTime | When tournament was uploaded |

**Error Responses**:

- `400 Bad Request`: Invalid query parameters
  ```json
  {
    "error": "Invalid date format for startDateFrom parameter"
  }
  ```

---

### Get All Pairs

Retrieves all tournament pairs.

**Endpoint**: `GET /v1/pairs`

**Response**: `200 OK`

```json
[
  {
    "id": "pair-123",
    "displayName": "John Doe / Jane Smith",
    "player1": "John Doe",
    "player2": "Jane Smith"
  },
  {
    "id": "pair-456",
    "displayName": "Alice Johnson / Bob Williams",
    "player1": "Alice Johnson",
    "player2": "Bob Williams"
  }
]
```

**Response Schema**: Array of `PairDto`

| Field | Type | Description |
|-------|------|-------------|
| id | string | Unique pair identifier |
| displayName | string | Combined display name |
| player1 | string | First player name |
| player2 | string | Second player name |

---

### Get Games for Pair

Retrieves all games for a specific pair.

**Endpoint**: `GET /v1/pairs/{pairId}/games`

**Parameters**:
- `pairId` (path, required): The unique identifier of the pair

**Response**: `200 OK`

```json
[
  {
    "id": "game-789",
    "round": 1,
    "courtNumber": 5,
    "status": "scheduled",
    "scheduledTime": "2025-10-27T10:00:00Z",
    "pair1": "John Doe / Jane Smith",
    "pair2": "Alice Johnson / Bob Williams",
    "isOurGame": true
  },
  {
    "id": "game-790",
    "round": 2,
    "courtNumber": 3,
    "status": "completed",
    "scheduledTime": "2025-10-27T14:00:00Z",
    "pair1": "John Doe / Jane Smith",
    "pair2": "Charlie Brown / Diana Prince",
    "isOurGame": true
  }
]
```

**Response Schema**: Array of `GameDto`

| Field | Type | Description |
|-------|------|-------------|
| id | string | Unique game identifier |
| round | int | Round number |
| courtNumber | int | Court number where game is played |
| status | string | Game status (scheduled, in-progress, completed) |
| scheduledTime | DateTime? | Scheduled start time (nullable) |
| pair1 | string | First pair display name |
| pair2 | string | Second pair display name |
| isOurGame | boolean | Whether this game involves the selected pair |

**Error Responses**:

- `404 Not Found`: Pair not found
  ```json
  {
    "error": "Pair not found",
    "pairId": "invalid-id"
  }
  ```

---

### Upload Tournament

Uploads an Excel file containing tournament data.

**Endpoint**: `POST /tournament/upload`

**Content-Type**: `application/octet-stream` or `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`

**Filename Format** (Optional but Recommended):
To include metadata, use this filename pattern:
```
TournamentName_YYYY-MM-DD_Location_Division.xlsx
```

Examples:
- `SummerChampionship_2025-11-15_Chicago_DivisionA.xlsx`
- `WinterCup_2025-12-01_NewYork_DivisionB.xlsx`

**Metadata in Excel** (Optional):
You can also include metadata in the first few rows of the Excel file:
```
Row 1: Tournament Name: | Summer Championship
Row 2: Location:        | Chicago
Row 3: Date:            | 2025-11-15
Row 4: Division:        | Division A
Row 5: Description:     | Annual summer championship
```

**Request Example**:
```bash
# Upload with metadata in filename
curl -X POST https://suled-functions.azurewebsites.net/api/tournament/upload \
  -H "Content-Type: application/octet-stream" \
  --data-binary "@SummerChampionship_2025-11-15_Chicago_DivisionA.xlsx"
```

**Response**: `201 Created`

```json
{
  "id": "tournament-123",
  "name": "SummerChampionship",
  "gameCount": 24,
  "message": "Tournament uploaded successfully"
}
```

**Response Schema**:

| Field | Type | Description |
|-------|------|-------------|
| id | string | Generated tournament identifier |
| name | string | Tournament name |
| gameCount | int | Number of games parsed |
| message | string | Success message |

**Error Responses**:

- `400 Bad Request`: Invalid file format or empty file
  ```json
  {
    "error": "No file data received"
  }
  ```

- `408 Request Timeout`: File upload timeout
  ```json
  {
    "error": "Request timeout while reading file"
  }
  ```

---

## Data Models

### TournamentListDto

```csharp
public class TournamentListDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int GameCount { get; init; }
    public DateTime CreatedDate { get; init; }
}
```

### PairDto

```csharp
public class PairDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Player1 { get; set; } = string.Empty;
    public string Player2 { get; set; } = string.Empty;
}
```

### GameDto

```csharp
public class GameDto
{
    public string Id { get; set; } = string.Empty;
    public int Round { get; set; }
    public int CourtNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledTime { get; set; }
    public string Pair1 { get; set; } = string.Empty;
    public string Pair2 { get; set; } = string.Empty;
    public bool IsOurGame { get; set; }
}
```

---

## Status Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 202 | Accepted (async operation) |
| 400 | Bad Request |
| 404 | Not Found |
| 413 | Payload Too Large |
| 500 | Internal Server Error |

---

## Rate Limiting

Currently, no rate limiting is enforced. This will be added in future versions.

---

## Versioning

The API uses URL versioning:
- Current version: `v1`
- Example: `/v1/pairs`

When breaking changes are introduced, a new version will be released (e.g., `v2`), and `v1` will be maintained for backward compatibility.

---

## CORS

CORS is enabled for the following origins:
- `https://suled-mobile.app` (production mobile app)
- `http://localhost:*` (local development)

---

## Error Handling

All errors follow a consistent format:

```json
{
  "error": "Error message describing what went wrong",
  "details": "Additional details (optional)",
  "timestamp": "2025-10-27T10:00:00Z"
}
```

---

## Examples

### Example: Get all pairs and their games

```bash
# 1. Get all pairs
curl https://suled-functions.azurewebsites.net/api/v1/pairs

# 2. Get games for specific pair
curl https://suled-functions.azurewebsites.net/api/v1/pairs/pair-123/games
```

### Example: Upload tournament file

```bash
curl -X POST https://suled-functions.azurewebsites.net/api/v1/tournaments/upload \
  -F "file=@tournament.xlsx"
```

---

## Changelog

### v1.0.0 (2025-10-26)
- Initial API release
- Endpoints: GET /pairs, GET /pairs/{id}/games, POST /tournaments/upload
- Basic error handling
- No authentication

---

## Support

For API issues or questions:
- Create an issue in the [backend repository](https://github.com/yourusername/suled-backend)
- Contact: your-email@example.com
