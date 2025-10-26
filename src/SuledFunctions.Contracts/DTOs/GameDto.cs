namespace Suled.Contracts.DTOs;

/// <summary>
/// Data Transfer Object for Game information in API responses
/// </summary>
public class GameDto
{
    public string Id { get; set; } = string.Empty;
    public int Round { get; set; }
    public int CourtNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledTime { get; set; }
    public string Pair1 { get; set; } = string.Empty;
    public string Pair2 { get; set; } = string.Empty;
    public bool IsOurGame { get; set; }
}
