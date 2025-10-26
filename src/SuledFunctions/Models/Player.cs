namespace SuledFunctions.Models;

/// <summary>
/// Represents a player in the tournament
/// </summary>
public record Player
{
    public string Name { get; set; } = string.Empty;
    public string? Surname { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(Surname) 
        ? Name 
        : $"{Name} {Surname}";

    public override string ToString() => FullName;
}
