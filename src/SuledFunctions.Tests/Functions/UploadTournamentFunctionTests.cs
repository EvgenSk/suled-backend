using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Functions;
using SuledFunctions.Services.Handlers;
using SuledFunctions.Tests.Helpers;
using System.Text;

namespace SuledFunctions.Tests.Functions;

public class UploadTournamentFunctionTests
{
    [Fact]
    public async Task Run_DelegatesToRequestHandler()
    {
        var handlerMock = new Mock<IUploadTournamentRequestHandler>();
        var requestMock = CreateMockRequest();
        var expectedResponse = requestMock.Object.CreateResponse();
        handlerMock.Setup(h => h.HandleAsync(requestMock.Object)).ReturnsAsync(expectedResponse);

        var function = new UploadTournamentFunction(handlerMock.Object);
        var result = await function.Run(requestMock.Object);

        handlerMock.Verify(h => h.HandleAsync(requestMock.Object), Times.Once);
        Assert.Same(expectedResponse, result);
    }

    private static Mock<HttpRequestData> CreateMockRequest()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<ILoggerFactory, LoggerFactory>();
        serviceCollection.AddSingleton(Options.Create(new Microsoft.Azure.Functions.Worker.WorkerOptions
        {
            Serializer = new TestJsonSerializer()
        }));

        var context = new Mock<FunctionContext>();
        context.SetupProperty(c => c.InstanceServices, serviceCollection.BuildServiceProvider());

        var requestMock = new Mock<HttpRequestData>(context.Object);
        requestMock.Setup(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes("test")));

        var responseMock = new Mock<HttpResponseData>(context.Object);
        responseMock.SetupProperty(r => r.StatusCode);
        responseMock.SetupProperty(r => r.Body, new MemoryStream());
        responseMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

        return requestMock;
    }
}
