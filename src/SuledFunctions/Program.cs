using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuledFunctions.Configuration;
using SuledFunctions.Middleware;
using SuledFunctions.Repositories;
using SuledFunctions.Services;
using SuledFunctions.Services.Interfaces;
using SuledFunctions.Services.Handlers;
using SuledFunctions.Services.Excel;
using SuledFunctions.Services.Excel.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register global exception handling middleware
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();

// Configure JSON serialization to use camelCase for web API consistency
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Configure application settings with Options pattern
builder.Services.Configure<CosmosDbSettings>(options =>
{
    options.ConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnection") 
        ?? throw new InvalidOperationException("CosmosDbConnection setting is required");
    options.DatabaseName = Environment.GetEnvironmentVariable("CosmosDbName") 
        ?? throw new InvalidOperationException("CosmosDbName setting is required");
    options.ContainerName = Environment.GetEnvironmentVariable("CosmosContainerName") 
        ?? throw new InvalidOperationException("CosmosContainerName setting is required");
});

builder.Services.Configure<TournamentSettings>(
    builder.Configuration.GetSection(TournamentSettings.SectionName));

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

// Register repositories
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();

// Register application services
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IPairService, PairService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IRoundCalculationService, RoundCalculationService>();
builder.Services.AddScoped<IGamesService, GamesService>();
builder.Services.AddScoped<ITournamentUploadService, TournamentUploadService>();
builder.Services.AddScoped<IUploadTournamentRequestHandler, UploadTournamentRequestHandler>();
builder.Services.AddScoped<IGetTournamentsRequestHandler, GetTournamentsRequestHandler>();
builder.Services.AddScoped<IGetTournamentRequestHandler, GetTournamentRequestHandler>();
builder.Services.AddScoped<IGetGamesForPairRequestHandler, GetGamesForPairRequestHandler>();
builder.Services.AddScoped<IGetPairsRequestHandler, GetPairsRequestHandler>();

// Register Excel parsing components
builder.Services.AddScoped<IExcelMetadataExtractor, ExcelMetadataExtractor>();
builder.Services.AddScoped<IExcelGameParser, ExcelGameParser>();
builder.Services.AddScoped<IPairStructureConverter, PairStructureConverter>();

// Register FluentValidation validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
