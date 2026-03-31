using Microsoft.Azure.Functions.Worker.Http;

namespace SuledFunctions.Services.Handlers;

public interface IGetPairsRequestHandler
{
    Task<HttpResponseData> HandleAsync(HttpRequestData req);
}
