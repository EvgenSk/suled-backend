using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SuledFunctions.Services.Handlers;

namespace SuledFunctions.Functions;

public class GetTournamentsFunction(IGetTournamentsRequestHandler requestHandler)
{
    [Function("GetTournaments")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournaments")]
        HttpRequestData req) => requestHandler.HandleAsync(req);
}
