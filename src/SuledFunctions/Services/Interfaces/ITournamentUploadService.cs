namespace SuledFunctions.Services.Interfaces;

/// <summary>
/// Result of a successful tournament upload.
/// </summary>
public record TournamentUploadResult(string Id, string Name, int GameCount, int PairCount);

/// <summary>
/// Service responsible for parsing, converting, and persisting a tournament file.
/// </summary>
public interface ITournamentUploadService
{
    /// <summary>
    /// Parses the Excel stream, converts it to compact format, and saves it to the repository.
    /// </summary>
    Task<TournamentUploadResult> UploadAsync(Stream stream, string fileName);
}
