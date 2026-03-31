using Microsoft.Azure.Functions.Worker.Http;

namespace SuledFunctions.Services.Handlers;

public interface IUploadTournamentRequestHandler
{
    Task<HttpResponseData> HandleAsync(HttpRequestData req);
}
