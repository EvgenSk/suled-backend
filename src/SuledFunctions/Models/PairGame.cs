namespace SuledFunctions.Models;

/// <summary>
/// Represents a single game/match between two pairs (pair-centered view)
/// In pair-centered architecture, games are owned by pairs
/// </summary>
public record PairGame
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public int Round { get; set; }
    public int CourtNumber { get; set; }
    public Pair OpponentPair { get; set; } = new();
    public DateTime? ScheduledTime { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Scheduled;
}
