using System.ComponentModel.DataAnnotations;

namespace SuledFunctions.TelegramBot.Configuration;

public class TelegramBotSettings
{
    public const string SectionName = "TelegramBot";

    [Required]
    public string BotToken { get; set; } = string.Empty;
}
