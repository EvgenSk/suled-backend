using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SuledFunctions.Services.Handlers;

namespace SuledFunctions.Functions;

public class GetTournamentFunction(IGetTournamentRequestHandler requestHandler)
{
    [Function("GetTournament")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournament/{id}")]
        HttpRequestData req,
        string id) => requestHandler.HandleAsync(req, id);
}
