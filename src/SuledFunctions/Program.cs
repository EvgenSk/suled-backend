using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuledFunctions.Services;
using OfficeOpenXml;
using Microsoft.Azure.Cosmos;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure EPPlus license (EPPlus 8+)
// This must be set before any ExcelPackage instance is created
ExcelPackage.License.SetNonCommercialPersonal("EvgenSk"); // TODO: do it in some proper way

builder.ConfigureFunctionsWebApplication();

// Register CosmosClient
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("CosmosDbConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("CosmosDbConnection setting is required");
    }
    return new CosmosClient(connectionString);
});

// Register application services
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IPairService, PairService>();
builder.Services.AddScoped<IGameService, GameService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
