using System.Text.Json.Serialization;

namespace SuledFunctions.Contracts.DTOs;

/// <summary>
/// DTO for tournament list item
/// </summary>
public record TournamentListDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; init; }
    
    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; init; }
    
    [JsonPropertyName("location")]
    public string Location { get; init; } = string.Empty;
    
    [JsonPropertyName("division")]
    public string Division { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
    
    [JsonPropertyName("warmup")]
    public TimeSpan? Warmup { get; init; }
    
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
    
    [JsonPropertyName("gameCount")]
    public int GameCount { get; init; }
    
    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; init; }
    
    [JsonPropertyName("rounds")]
    public List<TournamentRoundDto> Rounds { get; init; } = new();
}
