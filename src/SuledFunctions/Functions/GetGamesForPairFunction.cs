using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SuledFunctions.Services.Handlers;

namespace SuledFunctions.Functions;

public class GetGamesForPairFunction(IGetGamesForPairRequestHandler requestHandler)
{
    [Function("GetGamesForPair")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "games/pair/{pairId}")]
        HttpRequestData req,
        string pairId) => requestHandler.HandleAsync(req, pairId);
}
