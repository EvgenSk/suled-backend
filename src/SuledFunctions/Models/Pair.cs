namespace SuledFunctions.Models;

/// <summary>
/// Represents a pair of players
/// </summary>
public record Pair
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Player Player1 { get; set; } = new();
    public Player Player2 { get; set; } = new();

    public string DisplayName => $"{Player1.FullName} & {Player2.FullName}";

    public override string ToString() => DisplayName;
}
