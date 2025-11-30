namespace SuledFunctions.Configuration;

/// <summary>
/// Configuration settings for tournament processing
/// </summary>
public class TournamentSettings
{
    public const string SectionName = "Tournament";

    /// <summary>
    /// Default game duration in minutes
    /// </summary>
    public int GameDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Default break duration between rounds in minutes
    /// </summary>
    public int BreakDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Default daily start time (e.g., "09:00")
    /// </summary>
    public string DailyStartTime { get; set; } = "09:00";

    /// <summary>
    /// Default daily end time (e.g., "18:00")
    /// </summary>
    public string DailyEndTime { get; set; } = "18:00";

    /// <summary>
    /// Maximum file upload size in bytes (10 MB default)
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum tournaments to return in list queries
    /// </summary>
    public int MaxResultsDefault { get; set; } = 100;
}
