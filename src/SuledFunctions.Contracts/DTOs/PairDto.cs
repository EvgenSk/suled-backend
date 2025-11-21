using System.Text.Json.Serialization;

namespace Suled.Contracts.DTOs;

/// <summary>
/// Data Transfer Object for Pair information in API responses
/// </summary>
public class PairDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [JsonPropertyName("player1")]
    public string Player1 { get; set; } = string.Empty;
    
    [JsonPropertyName("player2")]
    public string Player2 { get; set; } = string.Empty;
    
    [JsonPropertyName("gameCount")]
    public int GameCount { get; set; }
}
