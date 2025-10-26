namespace Suled.Contracts.DTOs;

/// <summary>
/// Data Transfer Object for Pair information in API responses
/// </summary>
public class PairDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Player1 { get; set; } = string.Empty;
    public string Player2 { get; set; } = string.Empty;
}
