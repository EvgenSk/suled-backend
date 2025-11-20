using System.Text.Json.Serialization;

namespace Suled.Contracts.DTOs;

/// <summary>
/// Data Transfer Object for Game information in API responses
/// </summary>
public class GameDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("round")]
    public int Round { get; set; }
    
    [JsonPropertyName("courtNumber")]
    public int CourtNumber { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("scheduledTime")]
    public DateTime? ScheduledTime { get; set; }
    
    [JsonPropertyName("pair1")]
    public string Pair1 { get; set; } = string.Empty;
    
    [JsonPropertyName("pair2")]
    public string Pair2 { get; set; } = string.Empty;
    
    [JsonPropertyName("isOurGame")]
    public bool IsOurGame { get; set; }
}
