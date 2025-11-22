using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuledFunctions.Services;
using OfficeOpenXml;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure EPPlus license (EPPlus 8+)
// This must be set before any ExcelPackage instance is created
ExcelPackage.License.SetNonCommercialPersonal("EvgenSk"); // TODO: do it in some proper way

builder.ConfigureFunctionsWebApplication();

// Configure JSON serialization to use camelCase for web API consistency
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Register CosmosClient
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("CosmosDbConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("CosmosDbConnection setting is required");
    }
    
    var options = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };
    
    return new CosmosClient(connectionString, options);
});

// Register application services
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IPairService, PairService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();

// Register Excel parsing components
builder.Services.AddScoped<SuledFunctions.Services.Excel.IExcelMetadataExtractor, SuledFunctions.Services.Excel.ExcelMetadataExtractor>();
builder.Services.AddScoped<SuledFunctions.Services.Excel.IExcelGameParser, SuledFunctions.Services.Excel.ExcelGameParser>();
builder.Services.AddScoped<SuledFunctions.Services.Excel.IPairStructureConverter, SuledFunctions.Services.Excel.PairStructureConverter>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
