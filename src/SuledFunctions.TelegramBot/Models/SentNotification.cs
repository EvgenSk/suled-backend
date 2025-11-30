namespace SuledFunctions.TelegramBot.Models;

/// <summary>
/// Tracks sent notifications to prevent duplicates
/// </summary>
public record SentNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public long ChatId { get; set; }
    public string GameId { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string NotificationType { get; set; } = string.Empty; // "NewTournament", "UpcomingGame"
}
