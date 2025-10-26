using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuledFunctions.Services;
using OfficeOpenXml;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure EPPlus license (EPPlus 8+)
// This must be set before any ExcelPackage instance is created
ExcelPackage.License.SetNonCommercialPersonal("EvgenSk"); // TODO: do it in some proper way

builder.ConfigureFunctionsWebApplication();

// Register application services
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IPairService, PairService>();
builder.Services.AddScoped<IGameService, GameService>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
