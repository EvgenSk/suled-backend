namespace SuledFunctions.Models;

/// <summary>
/// Represents a round in a tournament with timing information
/// </summary>
public record TournamentRound
{
    public int RoundNumber { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GameCount { get; set; }
}
