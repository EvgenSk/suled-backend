namespace SuledFunctions.Models;

/// <summary>
/// Represents a single game in the tournament
/// </summary>
public record Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public int Round { get; set; }
    public int CourtNumber { get; set; }
    public Pair Pair1 { get; set; } = new();
    public Pair Pair2 { get; set; } = new();
    public DateTime? ScheduledTime { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Scheduled;
}

public enum GameStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}
