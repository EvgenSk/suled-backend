using SuledFunctions.Contracts.DTOs;
using SuledFunctions.Models;

namespace SuledFunctions.Models;

/// <summary>
/// Maps Tournament domain models to API contract DTOs.
/// </summary>
public static class TournamentDtoMapper
{
    public static TournamentListDto ToListDto(Tournament t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        StartDate = t.StartDate,
        EndDate = t.EndDate,
        StartTime = t.StartTime,
        EndTime = t.EndTime,
        Location = t.Location,
        Division = t.Division,
        Description = t.Description,
        Warmup = t.Warmup,
        Status = t.Status.ToString(),
        GameCount = t.Pairs?.Sum(p => p.Games?.Count ?? 0) ?? 0,
        CreatedDate = t.CreatedDate,
        Rounds = t.Rounds?.Select(r => new TournamentRoundDto
        {
            RoundNumber = r.RoundNumber,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            GameCount = r.GameCount
        }).ToList() ?? new List<TournamentRoundDto>()
    };
}
