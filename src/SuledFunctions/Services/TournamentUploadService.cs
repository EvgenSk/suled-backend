using FluentValidation;
using SuledFunctions.Common;
using SuledFunctions.Exceptions;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services.Excel.Interfaces;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Validators;

namespace SuledFunctions.Services;

/// <summary>
/// Orchestrates parsing, compact conversion, and persistence of a tournament upload.
/// </summary>
public class TournamentUploadService(
    IExcelParserService excelParser,
    ITournamentRepository repository,
    IValidator<Stream> fileValidator) : ITournamentUploadService
{
    public async Task<TournamentUploadResult> UploadAsync(Stream stream, string fileName)
    {
        var validationResult = await fileValidator.ValidateAsync(stream);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new Exceptions.ValidationException("File validation failed", errors);
        }

        if (!FileUploadValidator.ValidateFileExtension(fileName))
            throw new Exceptions.ValidationException("fileName", Constants.ErrorMessages.InvalidFileType);

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
