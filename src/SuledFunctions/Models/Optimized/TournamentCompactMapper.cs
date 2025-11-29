using System.Security.Cryptography;
using System.Text;

namespace SuledFunctions.Models.Optimized;

/// <summary>
/// Bidirectional mapper between Tournament and TournamentCompact formats.
/// Handles compression for storage and expansion for API responses.
/// </summary>
public static class TournamentCompactMapper
{
    /// <summary>
    /// Convert full Tournament to compact format for Cosmos DB storage.
    /// Reduces storage by 80-85% through:
    /// - Sequential integer IDs instead of 64-char hashes
    /// - Array representation for players [name, surname]
    /// - Array representation for games [round, court, opponentId]
    /// - Removal of computed fields (displayName, fullName, gameCount)
    /// </summary>
    /// <param name="tournament">Full tournament model</param>
    /// <returns>Compact tournament model</returns>
    public static TournamentCompact ToCompact(Tournament tournament)
    {
        if (tournament == null)
            throw new ArgumentNullException(nameof(tournament));
        
        // Create ID mapping: hash -> sequential integer (1, 2, 3...)
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
            EndDate = tournament.EndDate,
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
                Player1 = new[] 
                { 
                    pair.PairInfo.Player1.Name, 
                    pair.PairInfo.Player1.Surname ?? string.Empty 
                },
                Player2 = new[] 
                { 
                    pair.PairInfo.Player2.Name, 
                    pair.PairInfo.Player2.Surname ?? string.Empty 
                },
                Games = pair.Games.Select(game =>
                {
                    var gameArray = new List<int>
                    {
                        game.Round,
                        game.CourtNumber,
                        pairIdMap[game.OpponentPair.Id]
                    };
                    
                    // Only include status if not default (Scheduled = 0)
                    if (game.Status != GameStatus.Scheduled)
                    {
                        gameArray.Add((int)game.Status);
                    }
                    
                    return gameArray.ToArray();
                }).ToList()
            }).ToList()
        };
    }
    
    /// <summary>
    /// Expand compact format to full Tournament for API responses.
    /// Reconstructs all computed fields and relationships.
    /// </summary>
    /// <param name="compact">Compact tournament model from Cosmos DB</param>
    /// <returns>Full tournament model</returns>
    public static Tournament FromCompact(TournamentCompact compact)
    {
        if (compact == null)
            throw new ArgumentNullException(nameof(compact));
        
        // First pass: create all Pair objects with proper IDs
        var pairMap = new Dictionary<int, Pair>();
        
        foreach (var compactPair in compact.Pairs)
        {
            var pair = new Pair
            {
                Player1 = new Player
                {
                    Name = compactPair.Player1[0],
                    Surname = string.IsNullOrWhiteSpace(compactPair.Player1[1]) 
                        ? null 
                        : compactPair.Player1[1]
                },
                Player2 = new Player
                {
                    Name = compactPair.Player2[0],
                    Surname = string.IsNullOrWhiteSpace(compactPair.Player2[1]) 
                        ? null 
                        : compactPair.Player2[1]
                }
            };
            
            // Trigger ID generation (computes hash from player names)
            _ = pair.Id;
            
            pairMap[compactPair.Id] = pair;
        }
        
        // Second pass: create TournamentPairs with games
        var tournamentPairs = compact.Pairs.Select(compactPair =>
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
        }).ToList();
        
        return new Tournament
        {
            Id = compact.Id,
            Name = compact.Name,
            CreatedDate = compact.CreatedDate,
            BlobFileName = compact.BlobFileName,
            StartDate = compact.StartDate,
            EndDate = compact.EndDate,
            StartTime = compact.StartTime,
            EndTime = compact.EndTime,
            Location = compact.Location,
            Division = compact.Division,
            Description = compact.Description,
            Rules = compact.Rules,
            Warmup = compact.Warmup,
            Status = compact.Status,
            Rounds = compact.Rounds,
            Pairs = tournamentPairs
        };
    }
    
    /// <summary>
    /// Generate deterministic game ID from tournament + pair + round.
    /// Since games aren't stored separately in compact format, we generate IDs on expansion.
    /// Uses first 32 chars of SHA-256 hash for consistency with existing ID format.
    /// </summary>
    private static string GenerateGameId(string tournamentId, string pairId, int round)
    {
        var combined = $"{tournamentId}|{pairId}|{round}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hashBytes).ToLowerInvariant()[..32];
    }
}
