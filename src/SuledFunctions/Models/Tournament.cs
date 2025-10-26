namespace SuledFunctions.Models;

/// <summary>
/// Represents a tournament with all its games
/// </summary>
public record Tournament
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string BlobFileName { get; set; } = string.Empty;
    public List<Game> Games { get; set; } = new();
}
