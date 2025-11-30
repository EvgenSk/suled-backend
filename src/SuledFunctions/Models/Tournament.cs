using System.Text.Json.Serialization;

namespace SuledFunctions.Models;

/// <summary>
/// Represents a tournament with all its games
/// </summary>
public record Tournament
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string BlobFileName { get; set; } = string.Empty;
    
    // Tournament metadata
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;

    public TimeSpan? Warmup { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;
    
    // Round scheduling information
    public List<TournamentRound> Rounds { get; set; } = new();
    
    // Pair-centered: Tournament contains pairs, each with their games
    public List<TournamentPair> Pairs { get; set; } = new();
    
    // DEPRECATED: Keep for backward compatibility with existing tests
    // DO NOT USE in production code - use Pairs collection instead
    // This property exists only for tests that initialize empty tournaments
    [JsonIgnore]
    [Obsolete("Use Pairs collection instead. This property is kept only for backward compatibility with tests.")]
    public List<Game> Games { get; set; } = new();
}

/// <summary>
/// Tournament status
/// </summary>
public enum TournamentStatus
{
    Upcoming,
    InProgress,
    Completed,
    Cancelled
}
