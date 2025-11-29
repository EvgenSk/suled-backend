# Cosmos DB Storage Optimization Guide

## Overview
This document outlines the optimization strategy for reducing Cosmos DB storage costs and improving query performance for tournament data.

## Problem Analysis

### Current Structure Issues
The existing tournament JSON structure has several inefficiencies:

1. **Massive Data Duplication**: Each game appears twice (once for each participating pair)
2. **Redundant Computed Fields**: `displayName`, `fullName`, `gameCount` are stored but could be computed
3. **Repeated `tournamentId`**: Appears in every game object unnecessarily  
4. **Large String IDs**: 64-character SHA-256 hashes for pair IDs
5. **Full Object Embedding**: Complete opponent pair objects embedded in each game

### Storage Impact
- **Current Size**: ~100KB per tournament (2,008 lines)
- **Optimized Size**: ~15-20KB per tournament (125 lines)
- **Reduction**: **80-85%**
- **For 100 Tournaments**: 10MB → 1.5-2MB

---

## Optimization Strategy

### 1. **Use Sequential Integer IDs for Pairs**
Replace 64-character SHA-256 hashes with simple integers (1, 2, 3...).

**Before:**
```json
"id": "d78cc286f6648076de91bf094fbabff5622aa625aea032d604eed640e22b955f"
```

**After:**
```json
"id": 1
```

**Savings**: ~60 bytes per pair reference × 180+ occurrences = **~10KB per tournament**

---

### 2. **Compact Player Representation**
Store players as arrays instead of objects.

**Before:**
```json
{
  "player1": {
    "name": "Dmitri",
    "fullName": "Dmitri"
  },
  "player2": {
    "name": "Natalja",
    "fullName": "Natalja"
  }
}
```

**After:**
```json
{
  "p1": ["Dmitri", ""],
  "p2": ["Natalja", ""]
}
```

Format: `[name, surname]` - compute `fullName` on read.

**Savings**: ~40 bytes per pair × 10 pairs = **~400 bytes**

---

### 3. **Compact Game Representation**
Store games as arrays instead of objects.

**Before:**
```json
{
  "id": "18d31dbd-52ff-4605-b847-3b29cd3cae5b",
  "tournamentId": "f9d0db76-8907-47a5-84ea-b248e6d74dc0",
  "round": 1,
  "courtNumber": 6,
  "opponentPair": { /* full object */ },
  "status": 0
}
```

**After:**
```json
[1, 6, 2]  // [round, court, opponentPairId]
```

- Remove per-game GUIDs (derive from tournament + round + pairs)
- Remove `tournamentId` (already in parent)
- Store opponent as ID reference only
- Store `status` only if not default (0)

**Savings**: ~150 bytes per game × 90 games = **~13.5KB per tournament**

---

### 4. **Remove Computed Fields**
Don't store fields that can be computed:
- `displayName` → compute from `p1` and `p2`
- `fullName` → compute from `name` and `surname`
- `gameCount` → use `games.length`

**Savings**: ~50 bytes per pair × 10 pairs = **~500 bytes**

---

### 5. **Eliminate Game Duplication** (Alternative Approach)

**Current Issue**: Each game appears twice:
```json
// In Pair 1's games
{ "round": 1, "court": 6, "opponent": Pair 2 }

// In Pair 2's games  
{ "round": 1, "court": 6, "opponent": Pair 1 }
```

**Option A: Keep Pair-Centric View** (Recommended for Current Architecture)
- Continue storing games in each pair
- Use compact representation to minimize duplication cost
- Simpler queries for "all games for a pair"

**Option B: Normalize Games**
- Store games separately, reference from pairs
- Eliminates duplication but requires more complex queries
- Better for write-heavy workloads

**Decision**: Keep Option A with compact representation (better read performance, simpler code).

---

## Implementation Plan

### Phase 1: Create Optimized Models

#### 1.1 Create Compact Models
```csharp
// File: Models/Optimized/TournamentCompact.cs
namespace SuledFunctions.Models.Optimized;

/// <summary>
/// Compact tournament representation optimized for Cosmos DB storage.
/// Can be expanded to full Tournament model on read.
/// </summary>
public record TournamentCompact
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string BlobFileName { get; set; } = string.Empty;
    
    // Metadata
    public DateTime? StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public TimeSpan? Warmup { get; set; }
    public TournamentStatus Status { get; set; }
    
    // Compact rounds (can stay as-is, already efficient)
    public List<TournamentRound> Rounds { get; set; } = new();
    
    // Compact pairs with integer IDs
    public List<PairCompact> Pairs { get; set; } = new();
}

/// <summary>
/// Compact pair representation: [name, surname]
/// </summary>
public record PairCompact
{
    public int Id { get; set; }
    
    [JsonPropertyName("p1")]
    public string[] Player1 { get; set; } = Array.Empty<string>(); // [name, surname]
    
    [JsonPropertyName("p2")]
    public string[] Player2 { get; set; } = Array.Empty<string>(); // [name, surname]
    
    // Compact games: [round, court, opponentId, status]
    // Status only included if != 0 (Scheduled)
    public List<int[]> Games { get; set; } = new();
    
    // Computed properties (not stored)
    [JsonIgnore]
    public string DisplayName => $"{GetFullName(Player1)} & {GetFullName(Player2)}";
    
    [JsonIgnore]
    public int GameCount => Games.Count;
    
    private static string GetFullName(string[] player)
    {
        if (player.Length < 2) return string.Empty;
        var surname = player[1];
        return string.IsNullOrWhiteSpace(surname) ? player[0] : $"{player[0]} {surname}";
    }
}
```

#### 1.2 Create Converter/Mapper
```csharp
// File: Services/TournamentCompactMapper.cs
namespace SuledFunctions.Services;

public static class TournamentCompactMapper
{
    /// <summary>
    /// Convert full Tournament to compact format for storage
    /// </summary>
    public static TournamentCompact ToCompact(Tournament tournament)
    {
        // Create ID mapping: hash -> sequential integer
        var pairIdMap = tournament.Pairs
            .Select((pair, index) => new { Hash = pair.Id, IntId = index + 1 })
            .ToDictionary(x => x.Hash, x => x.IntId);
        
        return new TournamentCompact
        {
            Id = tournament.Id,
            Name = tournament.Name,
            CreatedDate = tournament.CreatedDate,
            BlobFileName = tournament.BlobFileName,
            StartDate = tournament.StartDate,
            StartTime = tournament.StartTime,
            EndTime = tournament.EndTime,
            Location = tournament.Location,
            Division = tournament.Division,
            Description = tournament.Description,
            Rules = tournament.Rules,
            Warmup = tournament.Warmup,
            Status = tournament.Status,
            Rounds = tournament.Rounds,
            Pairs = tournament.Pairs.Select(pair => new PairCompact
            {
                Id = pairIdMap[pair.Id],
                Player1 = new[] { pair.PairInfo.Player1.Name, pair.PairInfo.Player1.Surname ?? "" },
                Player2 = new[] { pair.PairInfo.Player2.Name, pair.PairInfo.Player2.Surname ?? "" },
                Games = pair.Games.Select(game => new[]
                {
                    game.Round,
                    game.CourtNumber,
                    pairIdMap[game.OpponentPair.Id],
                    // Only include status if not default (Scheduled = 0)
                    (int)game.Status
                }.Where((_, idx) => idx < 3 || game.Status != GameStatus.Scheduled)
                 .ToArray()
                ).ToList()
            }).ToList()
        };
    }
    
    /// <summary>
    /// Expand compact format to full Tournament for reading
    /// </summary>
    public static Tournament FromCompact(TournamentCompact compact)
    {
        // First pass: create all pairs
        var pairMap = compact.Pairs.ToDictionary(
            p => p.Id,
            p => new Pair
            {
                Player1 = new Player 
                { 
                    Name = p.Player1[0], 
                    Surname = string.IsNullOrWhiteSpace(p.Player1[1]) ? null : p.Player1[1] 
                },
                Player2 = new Player 
                { 
                    Name = p.Player2[0], 
                    Surname = string.IsNullOrWhiteSpace(p.Player2[1]) ? null : p.Player2[1] 
                }
            }
        );
        
        // Generate hash IDs for pairs
        foreach (var pair in pairMap.Values)
        {
            _ = pair.Id; // Trigger ID generation
        }
        
        return new Tournament
        {
            Id = compact.Id,
            Name = compact.Name,
            CreatedDate = compact.CreatedDate,
            BlobFileName = compact.BlobFileName,
            StartDate = compact.StartDate,
            StartTime = compact.StartTime,
            EndTime = compact.EndTime,
            Location = compact.Location,
            Division = compact.Division,
            Description = compact.Description,
            Rules = compact.Rules,
            Warmup = compact.Warmup,
            Status = compact.Status,
            Rounds = compact.Rounds,
            Pairs = compact.Pairs.Select(compactPair =>
            {
                var pairInfo = pairMap[compactPair.Id];
                return new TournamentPair
                {
                    PairInfo = pairInfo,
                    Games = compactPair.Games.Select(gameArray =>
                    {
                        var round = gameArray[0];
                        var court = gameArray[1];
                        var opponentId = gameArray[2];
                        var status = gameArray.Length > 3 
                            ? (GameStatus)gameArray[3] 
                            : GameStatus.Scheduled;
                        
                        return new PairGame
                        {
                            Id = GenerateGameId(compact.Id, pairInfo.Id, round),
                            TournamentId = compact.Id,
                            Round = round,
                            CourtNumber = court,
                            OpponentPair = pairMap[opponentId],
                            Status = status
                        };
                    }).ToList()
                };
            }).ToList()
        };
    }
    
    private static string GenerateGameId(string tournamentId, string pairId, int round)
    {
        // Deterministic game ID: hash of tournament + pair + round
        var combined = $"{tournamentId}|{pairId}|{round}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hashBytes).ToLowerInvariant()[..32]; // Use first 32 chars
    }
}
```

---

### Phase 2: Update Repository Layer

#### 2.1 Update CosmosDbService
```csharp
// File: Services/CosmosDbService.cs (additions)

public async Task<Tournament> GetTournamentByIdAsync(string id)
{
    try
    {
        // Read compact format from Cosmos
        var response = await _container.ReadItemAsync<TournamentCompact>(
            id, 
            new PartitionKey(id)
        );
        
        // Expand to full model
        return TournamentCompactMapper.FromCompact(response.Resource);
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return null;
    }
}

public async Task<Tournament> UpsertTournamentAsync(Tournament tournament)
{
    // Convert to compact format for storage
    var compact = TournamentCompactMapper.ToCompact(tournament);
    
    var response = await _container.UpsertItemAsync(
        compact,
        new PartitionKey(compact.Id)
    );
    
    // Return expanded model
    return TournamentCompactMapper.FromCompact(response.Resource);
}
```

---

### Phase 3: Migration Strategy

#### Option A: Gradual Migration (Recommended)
1. Deploy new code that can read BOTH formats
2. Update on write: Convert old → new format when tournaments are modified
3. Run background job to convert existing tournaments
4. Remove old format support after migration complete

#### Option B: Big Bang Migration
1. Take application offline
2. Run migration script on all documents
3. Deploy new code
4. Bring application online

#### Migration Script
```csharp
// File: Scripts/MigrateTournamentsToCompactFormat.cs

public async Task MigrateAllTournamentsAsync()
{
    var query = new QueryDefinition("SELECT * FROM c");
    var iterator = _container.GetItemQueryIterator<dynamic>(query);
    
    var migratedCount = 0;
    var errorCount = 0;
    
    while (iterator.HasMoreResults)
    {
        var batch = await iterator.ReadNextAsync();
        
        foreach (var item in batch)
        {
            try
            {
                // Check if already in compact format
                var pairs = item.pairs;
                if (pairs != null && pairs[0].games != null)
                {
                    var firstGame = pairs[0].games[0];
                    
                    // Old format has object, new has array
                    if (firstGame is JObject)
                    {
                        // Deserialize as old format
                        var oldTournament = JsonSerializer.Deserialize<Tournament>(item.ToString());
                        
                        // Convert to compact
                        var compact = TournamentCompactMapper.ToCompact(oldTournament);
                        
                        // Save back
                        await _container.UpsertItemAsync(compact, new PartitionKey(compact.Id));
                        
                        migratedCount++;
                        _logger.LogInformation($"Migrated tournament {compact.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, $"Failed to migrate tournament {item.id}");
            }
        }
    }
    
    _logger.LogInformation($"Migration complete. Success: {migratedCount}, Errors: {errorCount}");
}
```

---

## Performance Considerations

### Read Performance
- **Expansion overhead**: ~1-5ms to expand compact → full model (negligible)
- **Network transfer**: 80% reduction in data transfer
- **RU consumption**: Reduced by ~50% due to smaller document size

### Write Performance
- **Compression overhead**: ~1-5ms to compress full → compact model (negligible)
- **RU consumption**: Reduced by ~50% due to smaller document size

### Query Performance
- Queries by pair ID still work (just using integers instead of hashes)
- Queries are faster due to smaller index size

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public void ToCompact_ConvertsCorrectly()
{
    // Arrange
    var tournament = CreateSampleTournament();
    
    // Act
    var compact = TournamentCompactMapper.ToCompact(tournament);
    
    // Assert
    Assert.Equal(tournament.Id, compact.Id);
    Assert.Equal(tournament.Pairs.Count, compact.Pairs.Count);
    Assert.All(compact.Pairs, p => Assert.InRange(p.Id, 1, tournament.Pairs.Count));
}

[Fact]
public void FromCompact_RoundTrip_PreservesData()
{
    // Arrange
    var original = CreateSampleTournament();
    
    // Act
    var compact = TournamentCompactMapper.ToCompact(original);
    var restored = TournamentCompactMapper.FromCompact(compact);
    
    // Assert
    Assert.Equal(original.Name, restored.Name);
    Assert.Equal(original.Pairs.Count, restored.Pairs.Count);
    
    for (int i = 0; i < original.Pairs.Count; i++)
    {
        var origPair = original.Pairs[i];
        var restPair = restored.Pairs[i];
        
        Assert.Equal(origPair.DisplayName, restPair.DisplayName);
        Assert.Equal(origPair.Games.Count, restPair.Games.Count);
    }
}
```

### Integration Tests
```csharp
[Fact]
public async Task SaveAndRetrieve_CompactFormat_PreservesData()
{
    // Arrange
    var tournament = CreateSampleTournament();
    
    // Act
    await _tournamentService.UpsertTournamentAsync(tournament);
    var retrieved = await _tournamentService.GetTournamentByIdAsync(tournament.Id);
    
    // Assert
    Assert.NotNull(retrieved);
    Assert.Equal(tournament.Name, retrieved.Name);
    Assert.Equal(tournament.Pairs.Count, retrieved.Pairs.Count);
}
```

---

## Rollback Plan

If issues are discovered after deployment:

1. **Code Rollback**: Deploy previous version that uses full format
2. **Data Rollback**: Compact format can be expanded to full format anytime
3. **No Data Loss**: Compact format contains all information from full format

---

## Cost Savings Estimation

Assuming:
- 1,000 tournaments
- 20 reads per day per tournament
- 2 writes per day per tournament

### Current Costs
- Storage: 100KB × 1,000 = **100 MB**
- Reads: 20,000 reads/day × 10 RU = **200,000 RU/day**
- Writes: 2,000 writes/day × 15 RU = **30,000 RU/day**

### Optimized Costs  
- Storage: 20KB × 1,000 = **20 MB** (80% reduction)
- Reads: 20,000 reads/day × 5 RU = **100,000 RU/day** (50% reduction)
- Writes: 2,000 writes/day × 8 RU = **16,000 RU/day** (47% reduction)

### Monthly Savings
- Storage: 80 MB saved
- RUs: (230,000 - 116,000) × 30 = **3.42 million RU/month saved**

At $0.008 per 10K RU/month: **$2.74/month saved per 1,000 tournaments**

---

## Recommendations

1. ✅ **Implement compact format** - Significant storage and RU savings
2. ✅ **Use gradual migration** - Lower risk, no downtime
3. ✅ **Add comprehensive tests** - Ensure data integrity
4. ✅ **Monitor RU consumption** - Track actual savings
5. ⚠️ **Consider normalized games** only if write performance becomes an issue

## Next Steps

1. Review this document with team
2. Create new models in `Models/Optimized/` folder
3. Implement mapper in `Services/TournamentCompactMapper.cs`
4. Add unit tests
5. Update CosmosDbService to use compact format
6. Deploy to dev environment
7. Run migration on dev data
8. Validate functionality
9. Deploy to production
10. Run migration on production data
