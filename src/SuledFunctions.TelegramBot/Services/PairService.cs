using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuledFunctions.Models;
using SuledFunctions.Models.DTOs;
using SuledFunctions.TelegramBot.Configuration;

namespace SuledFunctions.TelegramBot.Services;

/// <summary>
/// Service for pair-related operations
/// </summary>
public class PairService : IPairService
{
    private readonly Container _tournamentsContainer;
    private readonly ILogger<PairService> _logger;

    public PairService(
        CosmosClient cosmosClient,
        IOptions<TelegramBotCosmosDbSettings> settings,
        ILogger<PairService> logger)
    {
        _logger = logger;
        var s = settings.Value;
        _tournamentsContainer = cosmosClient.GetContainer(s.DatabaseName, s.TournamentsContainerName);
    }

    public async Task<List<PairDto>> GetAllPairsAsync()
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.Games != null");
        var iterator = _tournamentsContainer.GetItemQueryIterator<Tournament>(query);
        
        var tournaments = new List<Tournament>();
        while (iterator.HasMoreResults)
        {
            var results = await iterator.ReadNextAsync();
            tournaments.AddRange(results);
        }

        // Extract unique pairs from pair-centered structure
        var pairs = tournaments
            .Where(t => t.Pairs != null)
            .SelectMany(t => t.Pairs)
            .Select(tp => tp.PairInfo)
            .Where(p => p != null)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.DisplayName)
            .Select(p => new PairDto
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                Player1 = p.Player1.FullName,
                Player2 = p.Player2.FullName
            })
            .ToList();

        return pairs;
    }

    public async Task<PairDto?> GetPairByIdAsync(string pairId)
    {
        var allPairs = await GetAllPairsAsync();
        return allPairs.FirstOrDefault(p => p.Id == pairId);
    }
}
