namespace SuledFunctions.TelegramBot.Models;

/// <summary>
/// Represents a user's subscription to pair notifications
/// </summary>
public record UserSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public long ChatId { get; set; }
    public string PairId { get; set; } = string.Empty;
    public string PairDisplayName { get; set; } = string.Empty;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public int NotificationMinutesBefore { get; set; } = 5;
}
