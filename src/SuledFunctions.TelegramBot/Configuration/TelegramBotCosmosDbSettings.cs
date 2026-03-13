using System.ComponentModel.DataAnnotations;

namespace SuledFunctions.TelegramBot.Configuration;

public class TelegramBotCosmosDbSettings
{
    public const string SectionName = "TelegramBotCosmosDb";

    [Required]
    public string DatabaseName { get; set; } = string.Empty;

    [Required]
    public string TournamentsContainerName { get; set; } = string.Empty;

    [Required]
    public string SubscriptionsContainerName { get; set; } = string.Empty;

    [Required]
    public string NotificationsContainerName { get; set; } = string.Empty;
}
