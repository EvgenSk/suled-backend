using System.Text.Json.Serialization;

namespace SuledFunctions.Models.Optimized;

/// <summary>
/// Compact pair representation optimized for Cosmos DB storage.
/// Reduces storage by 60-70% compared to full Pair model.
/// Format: [name, surname] for players, [round, court, opponentId] for games.
/// </summary>
public record PairCompact
{
    /// <summary>
    /// Sequential integer ID (1, 2, 3...) instead of 64-char hash.
    /// Saves ~60 bytes per pair reference.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Player 1 as array: [name, surname].
    /// Empty string for surname if not present.
    /// </summary>
    public string[] Player1 { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Player 2 as array: [name, surname].
    /// Empty string for surname if not present.
    /// </summary>
    public string[] Player2 { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Compact games representation.
    /// Each game is an array: [round, courtNumber, opponentPairId].
    /// Optional 4th element for status if not default (0 = Scheduled).
    /// Saves ~150 bytes per game.
    /// </summary>
    public List<int[]> Games { get; set; } = new();
    
    // Computed properties (not stored in DB)
    
    /// <summary>
    /// Display name computed from player names.
    /// Not stored - computed on read to save space.
    /// </summary>
    [JsonIgnore]
    public string DisplayName => $"{GetFullName(Player1)} & {GetFullName(Player2)}";
    
    /// <summary>
    /// Game count computed from games array length.
    /// Not stored - computed on read to save space.
    /// </summary>
    [JsonIgnore]
    public int GameCount => Games.Count;
    
    /// <summary>
    /// Get full name from player array [name, surname].
    /// Returns "Name" or "Name Surname" depending on whether surname is present.
    /// </summary>
    private static string GetFullName(string[] player)
    {
        if (player.Length < 2) return string.Empty;
        var surname = player[1];
        return string.IsNullOrWhiteSpace(surname) ? player[0] : $"{player[0]} {surname}";
    }
}
