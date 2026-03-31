using Microsoft.Azure.Functions.Worker.Http;

namespace SuledFunctions.Services.Handlers;

public interface IGetTournamentsRequestHandler
{
    Task<HttpResponseData> HandleAsync(HttpRequestData req);
}
