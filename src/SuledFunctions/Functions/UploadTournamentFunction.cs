using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SuledFunctions.Services.Handlers;

namespace SuledFunctions.Functions;

public class UploadTournamentFunction(IUploadTournamentRequestHandler requestHandler)
{
    [Function("UploadTournament")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tournament/upload")]
        HttpRequestData req) => requestHandler.HandleAsync(req);
}
