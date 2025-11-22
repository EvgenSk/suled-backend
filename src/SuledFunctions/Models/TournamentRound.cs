namespace SuledFunctions.Models;

/// <summary>
/// Represents a round in a tournament with timing information
/// </summary>
public record TournamentRound
{
    public int RoundNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int GameCount { get; set; }
}
