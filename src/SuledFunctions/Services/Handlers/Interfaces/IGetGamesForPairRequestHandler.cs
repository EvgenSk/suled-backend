using Microsoft.Azure.Functions.Worker.Http;

namespace SuledFunctions.Services.Handlers;

public interface IGetGamesForPairRequestHandler
{
    Task<HttpResponseData> HandleAsync(HttpRequestData req, string pairId);
}
