namespace SuledFunctions.Contracts.DTOs;

/// <summary>
/// DTO for tournament list item
/// </summary>
public record TournamentListDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int GameCount { get; init; }
    public DateTime CreatedDate { get; init; }
}
