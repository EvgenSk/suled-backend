using System.Security.Cryptography;
using System.Text;

namespace SuledFunctions.Models;

/// <summary>
/// Represents a pair of players
/// </summary>
public record Pair
{
    private string? _id;
    
    public string Id 
    { 
        get => _id ?? GenerateId();
        set => _id = value;
    }
    
    public Player Player1 { get; set; } = new();
    public Player Player2 { get; set; } = new();

    public string DisplayName => $"{Player1.FullName} & {Player2.FullName}";

    public override string ToString() => DisplayName;
    
    /// <summary>
    /// Generate a deterministic ID based on normalized player names.
    /// Players are sorted alphabetically to ensure "A & B" and "B & A" get the same ID.
    /// </summary>
    private string GenerateId()
    {
        // Normalize player names (trim, lowercase)
        var name1 = Player1.FullName.Trim().ToLowerInvariant();
        var name2 = Player2.FullName.Trim().ToLowerInvariant();
        
        // Sort alphabetically to ensure consistent ordering
        var names = new[] { name1, name2 }.OrderBy(n => n).ToArray();
        var combined = $"{names[0]}|{names[1]}";
        
        // Generate a deterministic hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        _id = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return _id;
    }
}
