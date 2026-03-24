using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Orchestrates parsing, compact conversion, and persistence of a tournament upload.
/// </summary>
public class TournamentUploadService(
    IExcelParserService excelParser,
    ITournamentRepository repository) : ITournamentUploadService
{
    public async Task<TournamentUploadResult> UploadAsync(Stream stream, string fileName)
    {
        var tournament = await excelParser.ParseTournamentAsync(stream, fileName);

        var compact = TournamentCompactMapper.ToCompact(tournament);
        await repository.CreateAsync(compact);

        return new TournamentUploadResult(
            tournament.Id,
            tournament.Name,
            tournament.Pairs.Sum(p => p.Games.Count),
            tournament.Pairs.Count);
    }
}
