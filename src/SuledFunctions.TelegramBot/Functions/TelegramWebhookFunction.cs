using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SuledFunctions.TelegramBot.Services;
using System.Net;
using Telegram.Bot.Types;

namespace SuledFunctions.TelegramBot.Functions;

/// <summary>
/// HTTP Function to handle Telegram webhook updates
/// </summary>
public class TelegramWebhookFunction
{
    private readonly ILogger<TelegramWebhookFunction> _logger;
    private readonly ITelegramBotService _botService;

    public TelegramWebhookFunction(
        ILogger<TelegramWebhookFunction> logger,
        ITelegramBotService botService)
    {
        _logger = logger;
        _botService = botService;
    }

    [Function("TelegramWebhook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "telegram/webhook")]
        HttpRequestData req)
    {
        _logger.LogInformation("Received Telegram webhook update");

        try
        {
            var update = await req.ReadFromJsonAsync<Update>();
            
            if (update != null)
            {
                await _botService.HandleUpdateAsync(update);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram webhook");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            return response;
        }
    }
}
