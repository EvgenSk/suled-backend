using Microsoft.Azure.Functions.Worker.Http;

namespace SuledFunctions.Services.Handlers;

public interface IGetTournamentRequestHandler
{
    Task<HttpResponseData> HandleAsync(HttpRequestData req, string id);
}
