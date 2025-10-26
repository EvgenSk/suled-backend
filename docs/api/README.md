# API Documentation

## Base URL

- **Development**: `https://suled-functions-dev.azurewebsites.net/api`
- **Production**: `https://suled-functions.azurewebsites.net/api`

## Authentication

Currently, the API does not require authentication. This will be added in a future version.

## Endpoints

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

**Endpoint**: `POST /v1/tournaments/upload`

**Content-Type**: `multipart/form-data`

**Parameters**:
- `file` (form-data, required): Excel file (.xlsx) containing tournament data
- `tournamentId` (form-data, optional): Tournament identifier (generated if not provided)

**Request Example**:
```bash
curl -X POST https://suled-functions.azurewebsites.net/api/v1/tournaments/upload \
  -F "file=@tournament.xlsx" \
  -F "tournamentId=tournament-2025"
```

**Response**: `202 Accepted`

```json
{
  "message": "Tournament upload initiated",
  "tournamentId": "tournament-2025",
  "blobUrl": "https://storage.blob.core.windows.net/tournaments/tournament-2025.xlsx"
}
```

**Response Schema**:

| Field | Type | Description |
|-------|------|-------------|
| message | string | Status message |
| tournamentId | string | Tournament identifier |
| blobUrl | string | Azure Blob Storage URL |

**Error Responses**:

- `400 Bad Request`: Invalid file format
  ```json
  {
    "error": "Invalid file format. Please upload an Excel file (.xlsx)"
  }
  ```

- `413 Payload Too Large`: File size exceeds limit (10MB)
  ```json
  {
    "error": "File size exceeds maximum limit of 10MB"
  }
  ```

---

## Data Models

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
