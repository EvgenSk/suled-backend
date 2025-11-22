using System.Text.Json.Serialization;

namespace SuledFunctions.Contracts.DTOs;

/// <summary>
/// DTO for tournament round information
/// </summary>
public record TournamentRoundDto
{
    [JsonPropertyName("roundNumber")]
    public int RoundNumber { get; init; }
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; init; }
    
    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; init; }
    
    [JsonPropertyName("gameCount")]
    public int GameCount { get; init; }
}
