using System.Text.Json.Serialization;

namespace SuledFunctions.Models.Optimized;

/// <summary>
/// Compact tournament representation optimized for Cosmos DB storage.
/// Reduces storage by 80-85% compared to full Tournament model.
/// Can be expanded to full Tournament model on read using TournamentCompactMapper.
/// </summary>
public record TournamentCompact
{
    /// <summary>
    /// Tournament ID (GUID).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Partition key: year of the tournament start date (e.g., "2025").
    /// Co-locates all tournaments from the same year in one logical partition,
    /// eliminating cross-partition fan-outs for year-filtered list queries.
    /// </summary>
    [JsonPropertyName("pk")]
    public string Pk { get; set; } = string.Empty;
    
    /// <summary>
    /// Tournament name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// When the tournament was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }
    
    /// <summary>
    /// Original uploaded file name.
    /// </summary>
    public string BlobFileName { get; set; } = string.Empty;
    
    // Tournament metadata
    
    /// <summary>
    /// Tournament start date.
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Tournament end date.
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Daily start time for tournament rounds.
    /// </summary>
    public TimeSpan? StartTime { get; set; }
    
    /// <summary>
    /// Daily end time for tournament rounds.
    /// </summary>
    public TimeSpan? EndTime { get; set; }
    
    /// <summary>
    /// Tournament location/venue.
    /// </summary>
    public string Location { get; set; } = string.Empty;
    
    /// <summary>
    /// Division or group name.
    /// </summary>
    public string Division { get; set; } = string.Empty;
    
    /// <summary>
    /// Tournament description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Tournament rules (e.g., "15(16)").
    /// </summary>
    public string Rules { get; set; } = string.Empty;
    
    /// <summary>
    /// Warmup duration before games.
    /// </summary>
    public TimeSpan? Warmup { get; set; }
    
    /// <summary>
    /// Tournament status (0=Upcoming, 1=InProgress, 2=Completed, 3=Cancelled).
    /// </summary>
    public TournamentStatus Status { get; set; }
    
    /// <summary>
    /// Round scheduling information.
    /// Already efficient, kept as-is.
    /// </summary>
    public List<TournamentRound> Rounds { get; set; } = new();
    
    /// <summary>
    /// Compact pairs with integer IDs and compressed game data.
    /// This is where most storage savings occur (60-70% reduction).
    /// </summary>
    public List<PairCompact> Pairs { get; set; } = new();
}
