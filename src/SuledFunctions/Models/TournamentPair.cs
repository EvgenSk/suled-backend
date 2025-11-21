namespace SuledFunctions.Models;

/// <summary>
/// Represents a pair with all their games (pair-centered model)
/// </summary>
public record TournamentPair
{
    public Pair PairInfo { get; set; } = new();
    public List<PairGame> Games { get; set; } = new();
    
    // Computed properties for convenience
    public string Id => PairInfo.Id;
    public string DisplayName => PairInfo.DisplayName;
    public int GameCount => Games.Count;
}
