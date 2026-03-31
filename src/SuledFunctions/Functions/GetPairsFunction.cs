using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SuledFunctions.Services.Handlers;

namespace SuledFunctions.Functions;

public class GetPairsFunction(IGetPairsRequestHandler requestHandler)
{
    [Function("GetPairs")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pairs")]
        HttpRequestData req) => requestHandler.HandleAsync(req);
}
